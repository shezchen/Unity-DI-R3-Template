using Architecture;
#if UNITY_EDITOR
using Architecture.Audio;
using Cysharp.Threading.Tasks;
using Tools;
using VContainer;
#endif
using UnityEngine;

namespace Test
{
    /// <summary>
    /// Editor-only manual diagnostic kept temporarily during the audio migration.
    /// It is inert in Player builds and must not become part of runtime game flow.
    /// </summary>
    public sealed class SoundTest : MonoBehaviour
    {
#if UNITY_EDITOR
        [Inject] private IMusicPlayer _musicPlayer;
        [Inject] private ISfxPlayer _sfxPlayer;

        [ContextMenu("播放测试BGM")]
        public void PlayTestBGM()
        {
            _musicPlayer.PlayAsync(
                new MusicCueId("TestBGM"),
                MusicTransition.Default).ForgetLogged("[SoundTest] BGM boundary");
        }
        
        [ContextMenu("播放测试SFX")]
        public void PlayTestSFX()
        {
            _sfxPlayer.PlayAsync(new SfxCueId("TestSFX"))
                .ForgetLogged("[SoundTest] SFX boundary");
        }
#endif
    }
}
