using Cysharp.Threading.Tasks;
using R3;

namespace Architecture.Data.Settings
{
    public sealed record SettingsSnapshot(
        int MusicVolume,
        int SfxVolume,
        GameResolution Resolution,
        GameWindow WindowMode,
        GameLanguageType Language)
    {
        public static SettingsSnapshot Default { get; } = new(
            100,
            100,
            GameResolution.Res_1920x1080,
            GameWindow.FullScreenWindow,
            GameLanguageType.English);
    }

    public enum SettingsInitializationStatus
    {
        Loaded,
        RecoveredFromBackup,
        CreatedDefaults,
        PersistenceFailed
    }

    public sealed record SettingsInitializationResult(
        SettingsInitializationStatus Status,
        string Error = null)
    {
        public bool IsSuccess => Status != SettingsInitializationStatus.PersistenceFailed;
    }

    public sealed record SettingsUpdateResult(bool IsSuccess, string Error = null)
    {
        public static SettingsUpdateResult Success() => new(true);
        public static SettingsUpdateResult Failure(string error) => new(false, error);
    }

    public interface ISettingsService
    {
        bool IsInitialized { get; }
        bool IsFirstLaunch { get; }
        SettingsSnapshot Current { get; }
        Observable<SettingsSnapshot> Changes { get; }

        UniTask<SettingsInitializationResult> InitializeAsync();
        SettingsUpdateResult SetMusicVolume(int volume);
        SettingsUpdateResult SetSfxVolume(int volume);
        SettingsUpdateResult SetResolution(GameResolution resolution);
        SettingsUpdateResult SetWindowMode(GameWindow windowMode);
        SettingsUpdateResult SetLanguage(GameLanguageType language);
    }
}
