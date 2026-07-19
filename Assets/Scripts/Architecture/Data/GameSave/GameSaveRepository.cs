using System;
using Architecture.Data.Persistence;
using Newtonsoft.Json;

namespace Architecture.Data.GameSave
{
    public sealed record GameSaveLoadResult(
        GameSaveOperationStatus Status,
        GameDataRuntime Game,
        bool RecoveredFromBackup = false,
        string Error = null);

    public interface IGameSaveRepository
    {
        bool Exists(int slotIndex);
        GameSaveLoadResult Load(int slotIndex);
        GameSaveOperationResult Save(int slotIndex, GameDataRuntime game);
    }

    [Serializable]
    internal sealed class GameSaveDocument
    {
        public int? SchemaVersion;
        public DateTimeOffset? LastSavedAtUtc;
    }

    public sealed class JsonGameSaveRepository : IGameSaveRepository
    {
        private const int CurrentSchemaVersion = 1;

        private readonly IFileStore _fileStore;
        private readonly PersistencePaths _paths;

        public JsonGameSaveRepository(IFileStore fileStore, PersistencePaths paths)
        {
            _fileStore = fileStore;
            _paths = paths;
        }

        public bool Exists(int slotIndex) =>
            _fileStore.Exists(_paths.GetGameSavePath(slotIndex)) ||
            _fileStore.Exists(_paths.GetGameSaveBackupPath(slotIndex));

        public GameSaveLoadResult Load(int slotIndex)
        {
            var primaryPath = _paths.GetGameSavePath(slotIndex);
            var primaryExists = _fileStore.Exists(primaryPath);
            var primary = TryLoad(primaryPath);
            if (primary.Status == GameSaveOperationStatus.Success)
            {
                return primary;
            }

            var backup = TryLoad(_paths.GetGameSaveBackupPath(slotIndex));
            if (backup.Status == GameSaveOperationStatus.Success)
            {
                return backup with { RecoveredFromBackup = true };
            }

            if (!primaryExists && backup.Status == GameSaveOperationStatus.Missing)
            {
                return new GameSaveLoadResult(GameSaveOperationStatus.Missing, null);
            }

            var status = primary.Status == GameSaveOperationStatus.Failed ||
                         backup.Status == GameSaveOperationStatus.Failed
                ? GameSaveOperationStatus.Failed
                : GameSaveOperationStatus.InvalidData;
            return new GameSaveLoadResult(
                status,
                null,
                false,
                $"Primary: {primary.Error ?? primary.Status.ToString()}; " +
                $"Backup: {backup.Error ?? backup.Status.ToString()}");
        }

        public GameSaveOperationResult Save(int slotIndex, GameDataRuntime game)
        {
            if (game == null)
            {
                return new GameSaveOperationResult(GameSaveOperationStatus.InvalidData, "Game data is null.");
            }

            try
            {
                var document = new GameSaveDocument
                {
                    SchemaVersion = CurrentSchemaVersion,
                    LastSavedAtUtc = game.LastSavedAtUtc
                };
                var json = JsonConvert.SerializeObject(document, Formatting.Indented);
                _fileStore.WriteAllTextAtomic(
                    _paths.GetGameSavePath(slotIndex),
                    json,
                    _paths.GetGameSaveBackupPath(slotIndex));
                return new GameSaveOperationResult(GameSaveOperationStatus.Success);
            }
            catch (Exception exception)
            {
                return new GameSaveOperationResult(GameSaveOperationStatus.Failed, exception.Message);
            }
        }

        private GameSaveLoadResult TryLoad(string path)
        {
            if (!_fileStore.Exists(path))
            {
                return new GameSaveLoadResult(GameSaveOperationStatus.Missing, null);
            }

            try
            {
                var document = JsonConvert.DeserializeObject<GameSaveDocument>(_fileStore.ReadAllText(path));
                if (document == null || document.SchemaVersion != CurrentSchemaVersion || !document.LastSavedAtUtc.HasValue)
                {
                    return new GameSaveLoadResult(
                        GameSaveOperationStatus.InvalidData,
                        null,
                        false,
                        "Schema version or required field is invalid.");
                }

                return new GameSaveLoadResult(
                    GameSaveOperationStatus.Success,
                    new GameDataRuntime(document.LastSavedAtUtc.Value));
            }
            catch (JsonException exception)
            {
                return new GameSaveLoadResult(GameSaveOperationStatus.InvalidData, null, false, exception.Message);
            }
            catch (Exception exception)
            {
                return new GameSaveLoadResult(GameSaveOperationStatus.Failed, null, false, exception.Message);
            }
        }
    }
}
