using System.Threading;
using Architecture;
using Architecture.Data.Settings;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI
{
    /// <summary>
    /// 设置页面
    /// Renders an immutable settings snapshot and sends controlled updates to ISettingsService.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup), typeof(UIBinder), typeof(GraphicRaycaster))]
    public sealed class SettingsPage : MonoBehaviour, IBasePage
    {
        [SerializeField] private float fadeDuration = 0.5f;

        [SerializeField] private TextMeshProUGUI bgmVolume;
        [SerializeField] private TextMeshProUGUI sfxVolume;

        [Inject] private ISettingsService _settings;
        [Inject] private IPageNavigator _navigator;

        private CanvasGroup _canvasGroup;
        private UIBinder _uiBinder;
        private GraphicRaycaster _raycaster;
        private TMP_Dropdown _resolutionDropdown;
        private Toggle _fullScreenToggle;
        private Slider _bgmSlider;
        private Slider _sfxSlider;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _uiBinder = GetComponent<UIBinder>();
            _raycaster = GetComponent<GraphicRaycaster>();
            _canvasGroup.alpha = 0;

            var finishButton = _uiBinder.Get<Button>("Button_FinishSettings");
            finishButton.OnClickAsObservable()
                .Subscribe(_ => CloseAsync().Forget(exception =>
                {
                    if (exception is not System.OperationCanceledException)
                    {
                        Debug.LogError($"[SettingsPage] Close boundary failed unexpectedly.\n{exception}");
                    }
                }))
                .AddTo(this);

            _resolutionDropdown = _uiBinder.Get<TMP_Dropdown>("Object_Resolution");
            _fullScreenToggle = _uiBinder.Get<Toggle>("Toggle_FullScreen");
            _bgmSlider = _uiBinder.Get<Slider>("Slider_BGM");
            _sfxSlider = _uiBinder.Get<Slider>("Slider_SFX");

            Render(_settings.Current);
            _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            _fullScreenToggle.onValueChanged.AddListener(OnWindowModeChanged);
            _bgmSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            _sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            _settings.Changes.Subscribe(Render).AddTo(this);
        }

        private void OnDestroy()
        {
            _canvasGroup?.DOKill();
            _resolutionDropdown?.onValueChanged.RemoveListener(OnResolutionChanged);
            _fullScreenToggle?.onValueChanged.RemoveListener(OnWindowModeChanged);
            _bgmSlider?.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            _sfxSlider?.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }

        private async UniTask CloseAsync()
        {
            var result = await _navigator.PopAsync(this.GetCancellationTokenOnDestroy());
            if (!result.IsSuccess)
            {
                Debug.LogError($"[SettingsPage] Close failed: {result.Status}. {result.Error}");
            }
        }

        public async UniTask OnEnter(CancellationToken cancellationToken)
        {
            if (_raycaster != null) _raycaster.enabled = true;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            await _canvasGroup
                .DOFade(1f, fadeDuration)
                .SetTarget(_canvasGroup)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: cancellationToken);
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        public UniTask OnPause(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_raycaster != null) _raycaster.enabled = false;
            return UniTask.CompletedTask;
        }

        public UniTask OnResume(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_raycaster != null) _raycaster.enabled = true;
            Render(_settings.Current);
            return UniTask.CompletedTask;
        }

        public async UniTask OnExit(CancellationToken cancellationToken)
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            await _canvasGroup
                .DOFade(0f, fadeDuration)
                .SetTarget(_canvasGroup)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: cancellationToken);
        }

        private void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= _resolutionDropdown.options.Count ||
                !TryParseResolution(_resolutionDropdown.options[index].text, out var resolution))
            {
                Debug.LogError($"[SettingsPage] Unsupported resolution option at index {index}.");
                Render(_settings.Current);
                return;
            }

            ApplyResult(_settings.SetResolution(resolution), "resolution");
        }

        private void OnWindowModeChanged(bool isFullScreen) => ApplyResult(
            _settings.SetWindowMode(isFullScreen ? GameWindow.FullScreenWindow : GameWindow.Window),
            "window mode");

        private void OnMusicVolumeChanged(float value) => ApplyResult(
            _settings.SetMusicVolume(Mathf.RoundToInt(value)),
            "music volume");

        private void OnSfxVolumeChanged(float value) => ApplyResult(
            _settings.SetSfxVolume(Mathf.RoundToInt(value)),
            "SFX volume");

        private void ApplyResult(SettingsUpdateResult result, string settingName)
        {
            if (result.IsSuccess)
            {
                return;
            }

            Debug.LogError($"[SettingsPage] Failed to save {settingName}: {result.Error}");
            Render(_settings.Current);
        }

        private void Render(SettingsSnapshot settings)
        {
            var resolutionIndex = _resolutionDropdown.options.FindIndex(option =>
                string.Equals("Res_" + option.text, settings.Resolution.ToString(),
                    System.StringComparison.Ordinal));
            if (resolutionIndex >= 0)
            {
                _resolutionDropdown.SetValueWithoutNotify(resolutionIndex);
            }

            _fullScreenToggle.SetIsOnWithoutNotify(settings.WindowMode == GameWindow.FullScreenWindow);
            _bgmSlider.SetValueWithoutNotify(settings.MusicVolume);
            _sfxSlider.SetValueWithoutNotify(settings.SfxVolume);
            bgmVolume.text = settings.MusicVolume.ToString();
            sfxVolume.text = settings.SfxVolume.ToString();
        }

        private static bool TryParseResolution(string value, out GameResolution resolution) =>
            System.Enum.TryParse("Res_" + value, out resolution) &&
            System.Enum.IsDefined(typeof(GameResolution), resolution);
    }
}
