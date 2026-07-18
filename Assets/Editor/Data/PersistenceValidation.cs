using System;
using System.Collections.Generic;
using System.IO;
using Architecture;
using Architecture.Data.GameSave;
using Architecture.Data.Persistence;
using Architecture.Data.Settings;
using UnityEditor;
using UnityEngine;
using ProjectSettingsService = Architecture.Data.Settings.SettingsService;

namespace Editor.Data
{
    public static class PersistenceValidation
    {
        private const string MenuPath = "Tools/Template/Data/Run Persistence Checks";
        private static int _assertionCount;

        [MenuItem(MenuPath)]
        private static async void Run()
        {
            _assertionCount = 0;
            try
            {
                await VerifySettingsAsync();
                VerifyGameSave();
                VerifyPhysicalAtomicWrite();
                Debug.Log($"[PersistenceValidation] PASS ({_assertionCount} assertions).");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PersistenceValidation] FAIL: {exception.Message}\n{exception}");
            }
        }

        private static async Cysharp.Threading.Tasks.UniTask VerifySettingsAsync()
        {
            var store = new MemoryFileStore();
            var paths = new PersistencePaths("settings-memory");
            var repository = new JsonSettingsRepository(store, paths);
            var service = new ProjectSettingsService(repository);

            var initialization = await service.InitializeAsync();
            Assert(initialization.Status == SettingsInitializationStatus.CreatedDefaults, "Missing settings create defaults.");
            Assert(service.IsFirstLaunch, "Created defaults mark first launch.");
            Assert(service.Current == SettingsSnapshot.Default, "Default snapshot is stable.");
            Assert(store.Exists(paths.SettingsPath), "Default settings are persisted.");

            Assert(service.SetMusicVolume(42).IsSuccess, "Music volume update succeeds.");
            Assert(service.SetSfxVolume(37).IsSuccess, "SFX volume update succeeds.");
            Assert(service.SetResolution(GameResolution.Res_2560x1440).IsSuccess, "Resolution update succeeds.");
            Assert(service.SetWindowMode(GameWindow.Window).IsSuccess, "Window mode update succeeds.");
            Assert(service.SetLanguage(GameLanguageType.Japanese).IsSuccess, "Language update succeeds.");

            var reloaded = new ProjectSettingsService(new JsonSettingsRepository(store, paths));
            var reloadResult = await reloaded.InitializeAsync();
            Assert(reloadResult.Status == SettingsInitializationStatus.Loaded, "Settings reload from primary.");
            Assert(reloaded.Current == service.Current, "Settings snapshot round-trips.");

            store.Seed(paths.SettingsPath, "{broken-json");
            var recovered = new ProjectSettingsService(new JsonSettingsRepository(store, paths));
            var recoveryResult = await recovered.InitializeAsync();
            Assert(recoveryResult.Status == SettingsInitializationStatus.RecoveredFromBackup, "Corrupt primary loads backup.");
            Assert(recovered.Current != null, "Recovered settings produce a snapshot.");

            var schemaStore = new MemoryFileStore();
            var schemaPaths = new PersistencePaths("schema-memory");
            schemaStore.Seed(schemaPaths.SettingsPath, "{\"schemaVersion\":99}");
            var schemaService = new ProjectSettingsService(new JsonSettingsRepository(schemaStore, schemaPaths));
            var schemaResult = await schemaService.InitializeAsync();
            Assert(schemaResult.Status == SettingsInitializationStatus.CreatedDefaults, "Unsupported settings schema is rejected.");
            Assert(schemaService.IsFirstLaunch, "Rejected schema follows first-launch defaults flow.");

            var failureStore = new MemoryFileStore();
            var failurePaths = new PersistencePaths("failure-memory");
            var failureService = new ProjectSettingsService(new JsonSettingsRepository(failureStore, failurePaths));
            await failureService.InitializeAsync();
            var beforeFailure = failureService.Current;
            failureStore.FailWrites = true;
            var failedUpdate = failureService.SetMusicVolume(1);
            Assert(!failedUpdate.IsSuccess, "Write failure is returned to the caller.");
            Assert(failureService.Current == beforeFailure, "Write failure does not mutate current settings.");

            service.Dispose();
            reloaded.Dispose();
            recovered.Dispose();
            schemaService.Dispose();
            failureService.Dispose();
        }

        private static void VerifyGameSave()
        {
            var store = new MemoryFileStore();
            var paths = new PersistencePaths("game-memory");
            var repository = new JsonGameSaveRepository(store, paths);
            var firstTime = new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero);
            var secondTime = firstTime.AddMinutes(5);
            var clock = new MutableClock(firstTime);
            var service = new GameSaveService(repository, clock);

            Assert(service.Save(0).Status == GameSaveOperationStatus.NoActiveGame, "Save requires an active game.");
            Assert(service.NewGame(0).IsSuccess, "New game persists to its slot.");
            Assert(service.Current.LastSavedAtUtc == firstTime, "New game uses injected clock.");
            Assert(service.SlotExists(0), "Created slot exists.");

            clock.UtcNow = secondTime;
            Assert(service.Save(0).IsSuccess, "Active game saves.");
            Assert(service.Current.LastSavedAtUtc == secondTime, "Save updates timestamp after persistence.");

            var loaded = new GameSaveService(repository, clock);
            Assert(loaded.Load(0).IsSuccess, "Game save reloads.");
            Assert(loaded.Current == service.Current, "Game data round-trips.");

            store.Seed(paths.GetGameSavePath(0), "{broken-json");
            var recovered = new GameSaveService(repository, clock);
            Assert(recovered.Load(0).IsSuccess, "Corrupt game save loads backup.");
            Assert(recovered.Current.LastSavedAtUtc == firstTime, "Backup contains the previous atomic version.");

            var invalidStore = new MemoryFileStore();
            var invalidPaths = new PersistencePaths("invalid-game-memory");
            invalidStore.Seed(invalidPaths.GetGameSavePath(2), "{\"schemaVersion\":99}");
            var invalid = new JsonGameSaveRepository(invalidStore, invalidPaths).Load(2);
            Assert(invalid.Status == GameSaveOperationStatus.InvalidData, "Unsupported game schema is rejected.");

            service.Dispose();
            loaded.Dispose();
            recovered.Dispose();
        }

        private static void VerifyPhysicalAtomicWrite()
        {
            var root = Path.Combine(Path.GetTempPath(), "DI-R3-Template-Persistence-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(root, "state.json");
            var backupPath = path + ".bak";
            var temporaryPath = path + ".tmp";
            Directory.CreateDirectory(root);

            try
            {
                var store = new PhysicalFileStore();
                store.WriteAllTextAtomic(path, "first", backupPath);
                store.WriteAllTextAtomic(path, "second", backupPath);

                Assert(File.ReadAllText(path) == "second", "Atomic write replaces the primary file.");
                Assert(File.ReadAllText(backupPath) == "first", "Atomic write keeps the previous version as backup.");
                Assert(!File.Exists(temporaryPath), "Atomic write cleans its temporary file.");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void Assert(bool condition, string message)
        {
            _assertionCount++;
            if (!condition)
            {
                throw new InvalidOperationException($"Assertion {_assertionCount} failed: {message}");
            }
        }

        private sealed class MutableClock : IClock
        {
            public MutableClock(DateTimeOffset utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTimeOffset UtcNow { get; set; }
        }

        private sealed class MemoryFileStore : IFileStore
        {
            private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

            public bool FailWrites { get; set; }

            public bool Exists(string path) => _files.ContainsKey(path);

            public string ReadAllText(string path)
            {
                if (!_files.TryGetValue(path, out var contents))
                {
                    throw new FileNotFoundException("Memory file does not exist.", path);
                }

                return contents;
            }

            public void WriteAllTextAtomic(string path, string contents, string backupPath)
            {
                if (FailWrites)
                {
                    throw new IOException("Injected write failure.");
                }

                if (_files.TryGetValue(path, out var previous))
                {
                    _files[backupPath] = previous;
                }

                _files[path] = contents;
            }

            public void Seed(string path, string contents) => _files[path] = contents;
        }
    }
}
