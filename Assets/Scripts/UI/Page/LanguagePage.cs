using System;
using System.Collections.Generic;
using System.Threading;
using Architecture;
using Architecture.Data.Settings;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Generated;
using R3;
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI
{
    [RequireComponent(typeof(UIBinder), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public sealed class LanguagePage : MonoBehaviour, IBasePage
    {
        [SerializeField] private TextMeshProUGUI languageText;
        [SerializeField] private float fadeDuration = 0.5f;

        [Inject] private ILanguageService _language;
        [Inject] private ISettingsService _settings;
        [Inject] private IPageNavigator _navigator;

        private readonly List<LanguageButton> _languageButtons = new();
        private UIBinder _uiBinder;
        private CanvasGroup _canvasGroup;
        private GraphicRaycaster _raycaster;
        private bool _isApplying;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _raycaster = GetComponent<GraphicRaycaster>();
            _uiBinder = GetComponent<UIBinder>();
            _canvasGroup.alpha = 0;

            BindButton("Button_Chinese", GameLanguageType.Chinese);
            BindButton("Button_English", GameLanguageType.English);
            BindButton("Button_Japanese", GameLanguageType.Japanese);
            UpdatePreview(_language.CurrentLanguage);
        }

        private void OnDestroy()
        {
            foreach (var languageButton in _languageButtons)
            {
                if (languageButton != null)
                {
                    languageButton.Selected -= UpdatePreview;
                }
            }
        }

        private void BindButton(string id, GameLanguageType language)
        {
            var button = _uiBinder.Get<Button>(id);
            button.OnClickAsObservable()
                .Subscribe(_ => ApplyLanguageAndContinueAsync(language)
                    .ForgetLogged("[LanguagePage] Language selection boundary"))
                .AddTo(this);

            var languageButton = button.GetComponent<LanguageButton>();
            if (languageButton != null)
            {
                languageButton.Selected += UpdatePreview;
                _languageButtons.Add(languageButton);
            }
        }

        private async UniTask ApplyLanguageAndContinueAsync(GameLanguageType language)
        {
            if (_isApplying)
            {
                return;
            }

            _isApplying = true;
            _raycaster.enabled = false;
            var previousLanguage = _language.CurrentLanguage;

            try
            {
                var cancellationToken = this.GetCancellationTokenOnDestroy();
                var change = await _language.SetLanguageAsync(language, cancellationToken);
                if (!change.IsSuccess)
                {
                    Debug.LogError($"[LanguagePage] Locale change failed: {change.Status}. {change.Error}");
                    return;
                }

                var save = _settings.SetLanguage(language);
                if (!save.IsSuccess)
                {
                    var rollback = await _language.SetLanguageAsync(previousLanguage, cancellationToken);
                    Debug.LogError(
                        $"[LanguagePage] Language setting save failed: {save.Error}. " +
                        $"Locale rollback: {rollback.Status}.");
                    return;
                }

                var navigation = await _navigator.ReplaceAsync<MainScenePage>(
                    AddressableKeys.Assets.MainScenePrefab,
                    cancellationToken);
                if (!navigation.IsSuccess)
                {
                    Debug.LogError(
                        $"[LanguagePage] Navigation failed: {navigation.Status}. {navigation.Error}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _isApplying = false;
                if (_raycaster != null)
                {
                    _raycaster.enabled = true;
                }
            }
        }

        private void UpdatePreview(GameLanguageType language)
        {
            languageText.text = language switch
            {
                GameLanguageType.Chinese => "这是正确的语言吗？",
                GameLanguageType.English => "Is this the correct language?",
                GameLanguageType.Japanese => "これは正しい言語ですか？",
                _ => languageText.text
            };
        }

        public async UniTask OnEnter(CancellationToken cancellationToken)
        {
            if (!_isApplying)
            {
                _raycaster.enabled = true;
            }

            await _canvasGroup.FadeIn(fadeDuration).ToUniTask(cancellationToken: cancellationToken);
        }

        public async UniTask OnPause(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _raycaster.enabled = false;
            await UniTask.CompletedTask;
        }

        public async UniTask OnResume(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_isApplying)
            {
                _raycaster.enabled = true;
            }

            await UniTask.CompletedTask;
        }

        public async UniTask OnExit(CancellationToken cancellationToken)
        {
            await _canvasGroup.FadeOut(fadeDuration).ToUniTask(cancellationToken: cancellationToken);
        }
    }
}
