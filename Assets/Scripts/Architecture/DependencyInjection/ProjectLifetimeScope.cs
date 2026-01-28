using Architecture.AudioProvider;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Architecture
{
    public class ProjectLifetimeScope : LifetimeScope
    {
        [BoxGroup("游戏流程管理器"), LabelText("Game Flow Controller"), SerializeField]
        private GameFlowController gameFlowController;
        
        [BoxGroup("音乐与音效"), LabelText("声音库"), SerializeField]
        private AudioCatalog audioCatalog;

        [BoxGroup("音乐与音效"), LabelText("BGM播放器"), SerializeField]
        private AudioSource bgmSource;

        [BoxGroup("音乐与音效"), LabelText("SFX播放器"), SerializeField]
        private AudioSource sfxSource;

        [BoxGroup("UI"), LabelText("UI Root"), SerializeField]
        private UIRoot uiRoot;
        
        protected override void Configure(IContainerBuilder builder)
        {
            #region 流程控制

            builder.RegisterComponent(gameFlowController);

            #endregion

            #region 事件总线

            builder.Register<EventBus>(Lifetime.Singleton);

            #endregion

            #region 数据系统

            builder.Register<DataManager>(Lifetime.Singleton);

            #endregion

            #region 声音相关服务

            builder.RegisterInstance(audioCatalog);
            builder.RegisterInstance(new BgmAudioSourceProvider(bgmSource))
                .As<IBgmAudioSourceProvider>();
            builder.RegisterInstance(new SfxAudioSourceProvider(sfxSource))
                .As<ISfxAudioSourceProvider>();
            builder.Register<AudioService>(Lifetime.Singleton).As<IAudioService>();

            #endregion

            #region 语言管理

            builder.Register<LanguageManager>(Lifetime.Singleton);

            #endregion

            #region UI

            builder.RegisterComponent(uiRoot);
            builder.Register<UIManager>(Lifetime.Singleton);

            #endregion

        }
    }
}
