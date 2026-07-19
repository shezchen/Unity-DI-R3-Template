using System;
using Architecture.Data.Settings;
using R3;

namespace Architecture.Audio
{
    /// <summary>
    /// Maps settings snapshots into AudioLevels without coupling Audio core to persistence.
    /// </summary>
    public sealed class AudioSettingsBinding : IDisposable
    {
        private readonly ISettingsService _settings;
        private readonly IAudioLevelsControl _levelsControl;
        private readonly DisposableBag _disposables = new();

        public AudioSettingsBinding(
            ISettingsService settings,
            IAudioLevelsControl levelsControl)
        {
            _settings = settings;
            _levelsControl = levelsControl;

            settings.Changes
                .Subscribe(Apply)
                .AddTo(ref _disposables);
        }

        public void ApplyCurrent()
        {
            if (!_settings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "[AudioSettingsBinding] Settings must be initialized before applying audio settings.");
            }

            Apply(_settings.Current);
        }

        public void Dispose() => _disposables.Dispose();

        private void Apply(SettingsSnapshot settings)
        {
            _levelsControl.Apply(new AudioLevels(
                settings.MusicVolume / 100f,
                settings.SfxVolume / 100f));
        }
    }
}
