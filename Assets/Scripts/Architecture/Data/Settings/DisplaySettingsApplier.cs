using System;
using R3;
using UnityEngine;

namespace Architecture.Data.Settings
{
    public sealed class DisplaySettingsApplier : IDisposable
    {
        private readonly ISettingsService _settings;
        private readonly DisposableBag _disposables = new();

        public DisplaySettingsApplier(ISettingsService settings)
        {
            _settings = settings;
            settings.Changes
                .Subscribe(Apply)
                .AddTo(ref _disposables);
        }

        public void ApplyCurrent()
        {
            if (!_settings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "[DisplaySettingsApplier] Settings must be initialized before applying display settings.");
            }

            Apply(_settings.Current);
        }

        public void Dispose() => _disposables.Dispose();

        private static void Apply(SettingsSnapshot settings)
        {
            var (width, height) = GetDimensions(settings.Resolution);
            var mode = settings.WindowMode == GameWindow.FullScreenWindow
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, mode);
        }

        public static (int Width, int Height) GetDimensions(GameResolution resolution) => resolution switch
        {
            GameResolution.Res_1280x720 => (1280, 720),
            GameResolution.Res_1366x768 => (1366, 768),
            GameResolution.Res_1600x900 => (1600, 900),
            GameResolution.Res_1920x1080 => (1920, 1080),
            GameResolution.Res_2560x1440 => (2560, 1440),
            GameResolution.Res_3840x2160 => (3840, 2160),
            GameResolution.Res_1280x800 => (1280, 800),
            GameResolution.Res_1920x1200 => (1920, 1200),
            GameResolution.Res_2560x1600 => (2560, 1600),
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
        };
    }
}
