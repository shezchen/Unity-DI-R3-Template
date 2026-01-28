using Architecture;
using Architecture.Data;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI
{
    /// <summary>
    /// 设置页面
    /// 通过 DataManager 的 public 方法修改设置，设置会自动保存并通知相关模块
    /// </summary>
    [RequireComponent(typeof(CanvasGroup), typeof(UIBinder), typeof(GraphicRaycaster))]
    public class SettingsPage : MonoBehaviour, IBasePage
    {
        [SerializeField] private float fadeDuration = 0.5f;

        [SerializeField] private TextMeshProUGUI bgmVolume;
        [SerializeField] private TextMeshProUGUI sfxVolume;

        [Inject] private DataManager _dataManager;
        [Inject] private UIManager _uiManager;

        private CanvasGroup _canvasGroup;
        private UIBinder _uiBinder;
        private GraphicRaycaster _raycaster;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _uiBinder = GetComponent<UIBinder>();
            _raycaster = GetComponent<GraphicRaycaster>();
            _canvasGroup.alpha = 0;

            // 绑定关闭按钮 - 使用新的 PopPage API
            var finishButton = _uiBinder.Get<Button>("Button_FinishSettings");
            finishButton.OnClickAsObservable().Subscribe((_) =>
            {
                OnCloseButtonClicked();
            }).AddTo(this);

            // 分辨率设置
            var resolutionDropdown = _uiBinder.Get<TMP_Dropdown>("Object_Resolution");
            resolutionDropdown.value = resolutionDropdown.options.FindIndex(option =>
                ("Res_" + option.text) == _dataManager.GameSettings.Resolution.ToString());
            resolutionDropdown.onValueChanged.RemoveAllListeners();
            resolutionDropdown.onValueChanged.AddListener(async (index) =>
            {
                var options = resolutionDropdown.options;
                if (index >= 0 && index < options.Count)
                {
                    var resText = options[index].text;

                    var dimensions = resText.Split('x');
                    if (dimensions.Length == 2 &&
                        int.TryParse(dimensions[0].Trim(), out int width) &&
                        int.TryParse(dimensions[1].Trim(), out int height))
                    {
                        Screen.SetResolution(width, height,
                            _dataManager.GameSettings.WindowMode == GameWindow.FullScreenWindow
                                ? FullScreenMode.FullScreenWindow
                                : FullScreenMode.Windowed);

                        await UniTask.Yield();
                    }

                    var resolution = resText switch
                    {
                        "1280x720" => GameResolution.Res_1280x720,
                        "1366x768" => GameResolution.Res_1366x768,
                        "1600x900" => GameResolution.Res_1600x900,
                        "1920x1080" => GameResolution.Res_1920x1080,
                        "2560x1440" => GameResolution.Res_2560x1440,
                        "3840x2160" => GameResolution.Res_3840x2160,
                        "1280x800" => GameResolution.Res_1280x800,
                        "1920x1200" => GameResolution.Res_1920x1200,
                        "2560x1600" => GameResolution.Res_2560x1600,
                        _ => _dataManager.GameSettings.Resolution
                    };
                    
                    _dataManager.SetResolution(resolution);
                }
            });

            // 全屏设置
            var fullScreenToggle = _uiBinder.Get<Toggle>("Toggle_FullScreen");
            fullScreenToggle.isOn = _dataManager.GameSettings.WindowMode == GameWindow.FullScreenWindow;
            fullScreenToggle.onValueChanged.RemoveAllListeners();
            fullScreenToggle.onValueChanged.AddListener((isFullScreen) =>
            {
                Screen.fullScreenMode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                _dataManager.SetWindowMode(isFullScreen ? GameWindow.FullScreenWindow : GameWindow.Window);
            });

            // BGM 音量设置
            var bgmSlider = _uiBinder.Get<Slider>("Slider_BGM");
            bgmSlider.value = _dataManager.GameSettings.BgmVolume;
            bgmVolume.text = Mathf.RoundToInt(bgmSlider.value).ToString();
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.onValueChanged.AddListener((value) =>
            {
                bgmVolume.text = Mathf.RoundToInt(value).ToString();
                _dataManager.SetBgmVolume(Mathf.RoundToInt(value));
            });

            // SFX 音效设置
            var sfxSlider = _uiBinder.Get<Slider>("Slider_SFX");
            sfxSlider.value = _dataManager.GameSettings.SfxVolume;
            sfxVolume.text = Mathf.RoundToInt(sfxSlider.value).ToString();
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener((value) =>
            {
                sfxVolume.text = Mathf.RoundToInt(value).ToString();
                _dataManager.SetSfxVolume(Mathf.RoundToInt(value));
            });
        }

        private void OnCloseButtonClicked()
        {
            // 设置已自动保存，直接关闭页面
            _uiManager.PopPage().Forget();
        }

        #region IBasePage 实现

        public async UniTask OnEnter()
        {
            if (_raycaster != null) _raycaster.enabled = true;
            await _canvasGroup.FadeIn(fadeDuration).AsyncWaitForCompletion();
        }

        public async UniTask OnPause()
        {
            if (_raycaster != null) _raycaster.enabled = false;
            await UniTask.CompletedTask;
        }

        public async UniTask OnResume()
        {
            if (_raycaster != null) _raycaster.enabled = true;
            // 刷新设置数据（如果需要）
            await UniTask.CompletedTask;
        }

        public async UniTask OnExit()
        {
            await _canvasGroup.FadeOut(fadeDuration).AsyncWaitForCompletion();
        }

        #endregion
    }
}
