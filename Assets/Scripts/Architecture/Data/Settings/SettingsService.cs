using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Architecture.Data.Settings
{
    public sealed class SettingsService : ISettingsService, IDisposable
    {
        private readonly ISettingsRepository _repository;
        private readonly Subject<SettingsSnapshot> _changes = new();

        public SettingsService(ISettingsRepository repository)
        {
            _repository = repository;
        }

        public bool IsInitialized { get; private set; }
        public bool IsFirstLaunch { get; private set; }
        public SettingsSnapshot Current { get; private set; }
        public Observable<SettingsSnapshot> Changes => _changes;

        public async UniTask<SettingsInitializationResult> InitializeAsync()
        {
            if (IsInitialized)
            {
                return new SettingsInitializationResult(SettingsInitializationStatus.Loaded);
            }

            var load = _repository.Load();
            if (load.Status is SettingsLoadStatus.Loaded or SettingsLoadStatus.RecoveredFromBackup)
            {
                Current = load.Settings;
                IsFirstLaunch = false;
                IsInitialized = true;

                if (load.Status == SettingsLoadStatus.RecoveredFromBackup)
                {
                    var restore = _repository.Save(Current);
                    if (!restore.IsSuccess)
                    {
                        Debug.LogWarning($"[Settings] Backup loaded but primary restore failed: {restore.Error}");
                    }
                }

                await UniTask.CompletedTask;
                return new SettingsInitializationResult(
                    load.Status == SettingsLoadStatus.Loaded
                        ? SettingsInitializationStatus.Loaded
                        : SettingsInitializationStatus.RecoveredFromBackup);
            }

            Current = SettingsSnapshot.Default;
            IsFirstLaunch = true;
            var save = _repository.Save(Current);
            IsInitialized = true;

            if (!save.IsSuccess)
            {
                Debug.LogError($"[Settings] Failed to persist defaults: {save.Error}");
                await UniTask.CompletedTask;
                return new SettingsInitializationResult(SettingsInitializationStatus.PersistenceFailed, save.Error);
            }

            if (load.Status is SettingsLoadStatus.Invalid or SettingsLoadStatus.Failed)
            {
                Debug.LogWarning($"[Settings] Existing settings rejected; defaults created. {load.Error}");
            }

            await UniTask.CompletedTask;
            return new SettingsInitializationResult(SettingsInitializationStatus.CreatedDefaults);
        }

        public SettingsUpdateResult SetMusicVolume(int volume) =>
            Update(RequireCurrent() with { MusicVolume = Mathf.Clamp(volume, 0, 100) });

        public SettingsUpdateResult SetSfxVolume(int volume) =>
            Update(RequireCurrent() with { SfxVolume = Mathf.Clamp(volume, 0, 100) });

        public SettingsUpdateResult SetResolution(GameResolution resolution) =>
            Update(RequireCurrent() with { Resolution = resolution });

        public SettingsUpdateResult SetWindowMode(GameWindow windowMode) =>
            Update(RequireCurrent() with { WindowMode = windowMode });

        public SettingsUpdateResult SetLanguage(GameLanguageType language) =>
            Update(RequireCurrent() with { Language = language });

        public void Dispose() => _changes.Dispose();

        private SettingsUpdateResult Update(SettingsSnapshot next)
        {
            if (next == Current)
            {
                return SettingsUpdateResult.Success();
            }

            var result = _repository.Save(next);
            if (!result.IsSuccess)
            {
                return result;
            }

            Current = next;
            _changes.OnNext(Current);
            return result;
        }

        private SettingsSnapshot RequireCurrent()
        {
            if (!IsInitialized || Current == null)
            {
                throw new InvalidOperationException("SettingsService must be initialized before use.");
            }

            return Current;
        }
    }
}
