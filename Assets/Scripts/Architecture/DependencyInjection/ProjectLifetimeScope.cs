using Architecture.Audio;
using Architecture.Audio.Internal;
using Architecture.Data.GameSave;
using Architecture.Data.Persistence;
using Architecture.Data.Settings;
using UI;
using UnityEngine;
using UnityEngine.Audio;
using VContainer;
using VContainer.Unity;

namespace Architecture
{
    public class ProjectLifetimeScope : LifetimeScope
    {
        [Header("游戏流程")]
        [SerializeField]
        private GameFlowController gameFlowController;
        
        [Header("音乐与音效")]
        [SerializeField]
        private AudioCatalog audioCatalog;

        [SerializeField]
        private AudioSource bgmSource;

        [SerializeField]
        private AudioSource sfxSource;

        [SerializeField]
        private AudioMixer audioMixer;

        [SerializeField]
        private string musicVolumeParameter = "MusicVolume";

        [SerializeField]
        private string sfxVolumeParameter = "SfxVolume";

        [Header("UI")]
        [SerializeField]
        private UIRoot uiRoot;
        
        protected override void Configure(IContainerBuilder builder)
        {
            #region 流程控制

            builder.RegisterComponent(gameFlowController);

            #endregion

            #region 数据系统

            builder.RegisterInstance(new PersistencePaths(Application.persistentDataPath));
            builder.Register<PhysicalFileStore>(Lifetime.Singleton).As<IFileStore>();
            builder.Register<SystemClock>(Lifetime.Singleton).As<IClock>();
            builder.Register<JsonSettingsRepository>(Lifetime.Singleton).As<ISettingsRepository>();
            builder.Register<SettingsService>(Lifetime.Singleton).As<ISettingsService>();
            builder.Register<DisplaySettingsApplier>(Lifetime.Singleton);
            builder.Register<JsonGameSaveRepository>(Lifetime.Singleton).As<IGameSaveRepository>();
            builder.Register<GameSaveService>(Lifetime.Singleton).As<IGameSaveService>();

            #endregion

            #region 声音相关服务

            builder.RegisterInstance(audioCatalog);
            builder.RegisterInstance(new AudioOutputConfiguration(
                bgmSource,
                sfxSource,
                audioMixer,
                musicVolumeParameter,
                sfxVolumeParameter));
            builder.Register<UnityAudioOutput>(Lifetime.Singleton)
                .As<IAudioOutput, IAudioLevelsControl, IAudioImmediateControl>();
            builder.Register<AddressableAudioClipStore>(Lifetime.Singleton)
                .As<IAudioClipStore>();
            builder.Register<MusicPlayer>(Lifetime.Singleton).As<IMusicPlayer>();
            builder.Register<SfxPlayer>(Lifetime.Singleton).As<ISfxPlayer>();
            builder.Register<AudioSettingsBinding>(Lifetime.Singleton);

            #endregion

            #region 语言管理

            builder.Register<LanguageManager>(Lifetime.Singleton).As<ILanguageService>();
            builder.Register<LocalizationSettingsBinding>(Lifetime.Singleton);

            #endregion

            #region UI

            builder.RegisterComponent(uiRoot);
            builder.Register<AddressableUiPrefabProvider>(Lifetime.Singleton).As<IUiPrefabProvider>();
            builder.Register<PageStack>(Lifetime.Singleton);
            builder.Register<PageNavigator>(Lifetime.Singleton).As<IPageNavigator>();

            #endregion

        }
    }
}
