using Cysharp.Threading.Tasks;
using Generated;
using UI;
using UnityEngine;
using VContainer;

namespace Architecture
{
    public class GameFlowController : MonoBehaviour
    {
        [Inject] private UIManager _uiManager;
        [Inject] private DataManager _dataManager;
        [Inject] private LanguageManager _languageManager;
        [Inject] private IAudioService _audioService;
        [Inject] private EventBus _eventBus;

        private async void Start()
        {
            // 游戏入口点
            await _languageManager.Init();
            await _dataManager.Init();
            await _uiManager.Init();

            // 此处可以加是否是首次启动的判断
            await _uiManager.PushPage<LanguagePage>(AddressableKeys.Assets.LanguagePagePrefab);
        }
    }
}