using System;
using Architecture.Data.Persistence;
using Newtonsoft.Json;

namespace Architecture.Data.Settings
{
    public enum SettingsLoadStatus
    {
        Loaded,
        RecoveredFromBackup,
        Missing,
        Invalid,
        Failed
    }

    public sealed record SettingsLoadResult(
        SettingsLoadStatus Status,
        SettingsSnapshot Settings,
        string Error = null);

    public interface ISettingsRepository
    {
        SettingsLoadResult Load();
        SettingsUpdateResult Save(SettingsSnapshot settings);
    }

    [Serializable]
    internal sealed class SettingsDocument
    {
        public int? schemaVersion;
        public int? musicVolume;
        public int? sfxVolume;
        public GameResolution? resolution;
        public GameWindow? windowMode;
        public GameLanguageType? language;
    }

    public sealed class JsonSettingsRepository : ISettingsRepository
    {
        private const int CurrentSchemaVersion = 1;

        private readonly IFileStore _fileStore;
        private readonly PersistencePaths _paths;

        public JsonSettingsRepository(IFileStore fileStore, PersistencePaths paths)
        {
            _fileStore = fileStore;
            _paths = paths;
        }

        public SettingsLoadResult Load()
        {
            var primaryExists = _fileStore.Exists(_paths.SettingsPath);
            var primary = TryLoad(_paths.SettingsPath);
            if (primary.Status == SettingsLoadStatus.Loaded)
            {
                return primary;
            }

            var backup = TryLoad(_paths.SettingsBackupPath);
            if (backup.Status == SettingsLoadStatus.Loaded)
            {
                return backup with { Status = SettingsLoadStatus.RecoveredFromBackup };
            }

            if (!primaryExists && backup.Status == SettingsLoadStatus.Missing)
            {
                return new SettingsLoadResult(SettingsLoadStatus.Missing, null);
            }

            var error = $"Primary: {primary.Error ?? primary.Status.ToString()}; " +
                        $"Backup: {backup.Error ?? backup.Status.ToString()}";
            var status = primary.Status == SettingsLoadStatus.Failed || backup.Status == SettingsLoadStatus.Failed
                ? SettingsLoadStatus.Failed
                : SettingsLoadStatus.Invalid;
            return new SettingsLoadResult(status, null, error);
        }

        public SettingsUpdateResult Save(SettingsSnapshot settings)
        {
            try
            {
                Validate(settings);
                var document = new SettingsDocument
                {
                    schemaVersion = CurrentSchemaVersion,
                    musicVolume = settings.MusicVolume,
                    sfxVolume = settings.SfxVolume,
                    resolution = settings.Resolution,
                    windowMode = settings.WindowMode,
                    language = settings.Language
                };
                var json = JsonConvert.SerializeObject(document, Formatting.Indented);
                _fileStore.WriteAllTextAtomic(_paths.SettingsPath, json, _paths.SettingsBackupPath);
                return SettingsUpdateResult.Success();
            }
            catch (Exception exception)
            {
                return SettingsUpdateResult.Failure(exception.Message);
            }
        }

        private SettingsLoadResult TryLoad(string path)
        {
            if (!_fileStore.Exists(path))
            {
                return new SettingsLoadResult(SettingsLoadStatus.Missing, null);
            }

            try
            {
                var document = JsonConvert.DeserializeObject<SettingsDocument>(_fileStore.ReadAllText(path));
                if (document == null)
                {
                    return new SettingsLoadResult(SettingsLoadStatus.Invalid, null, "Document is null.");
                }

                if (document.schemaVersion != CurrentSchemaVersion)
                {
                    return new SettingsLoadResult(
                        SettingsLoadStatus.Invalid,
                        null,
                        $"Unsupported schema version {document.schemaVersion?.ToString() ?? "missing"}.");
                }

                if (!document.musicVolume.HasValue ||
                    !document.sfxVolume.HasValue ||
                    !document.resolution.HasValue ||
                    !document.windowMode.HasValue ||
                    !document.language.HasValue)
                {
                    return new SettingsLoadResult(SettingsLoadStatus.Invalid, null, "Required field is missing.");
                }

                var settings = new SettingsSnapshot(
                    document.musicVolume.Value,
                    document.sfxVolume.Value,
                    document.resolution.Value,
                    document.windowMode.Value,
                    document.language.Value);
                Validate(settings);
                return new SettingsLoadResult(SettingsLoadStatus.Loaded, settings);
            }
            catch (JsonException exception)
            {
                return new SettingsLoadResult(SettingsLoadStatus.Invalid, null, exception.Message);
            }
            catch (Exception exception)
            {
                return new SettingsLoadResult(SettingsLoadStatus.Failed, null, exception.Message);
            }
        }

        private static void Validate(SettingsSnapshot settings)
        {
            if (settings == null)
            {
                throw new InvalidOperationException("Settings snapshot is null.");
            }

            if (settings.MusicVolume is < 0 or > 100 || settings.SfxVolume is < 0 or > 100)
            {
                throw new InvalidOperationException("Audio volume must be between 0 and 100.");
            }

            if (!Enum.IsDefined(typeof(GameResolution), settings.Resolution) ||
                !Enum.IsDefined(typeof(GameWindow), settings.WindowMode) ||
                !Enum.IsDefined(typeof(GameLanguageType), settings.Language))
            {
                throw new InvalidOperationException("Settings contain an unsupported enum value.");
            }
        }
    }
}
