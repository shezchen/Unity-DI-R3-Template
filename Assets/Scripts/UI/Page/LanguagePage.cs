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
            _canvasGroup?.DOKill();
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
                .Subscribe(_ => ApplyLanguageAndContinueAsync(language).Forget())
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
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            try
            {
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Page teardown owns this cancellation.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LanguagePage] Language selection failed unexpectedly.\n{exception}");
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

            await SetVisibleAsync(true, cancellationToken);
        }

        public async UniTask OnPause(CancellationToken cancellationToken)
        {
            _raycaster.enabled = false;
            await SetVisibleAsync(false, cancellationToken);
        }

        public async UniTask OnResume(CancellationToken cancellationToken)
        {
            await SetVisibleAsync(true, cancellationToken);
            if (!_isApplying)
            {
                _raycaster.enabled = true;
            }
        }

        public async UniTask OnExit(CancellationToken cancellationToken)
        {
            _raycaster.enabled = false;
            await SetVisibleAsync(false, cancellationToken);
        }

        private async UniTask SetVisibleAsync(bool visible, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetAlpha = visible ? 1f : 0f;
            if (Mathf.Approximately(_canvasGroup.alpha, targetAlpha))
            {
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
                return;
            }

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            var tween = _canvasGroup
                .DOFade(targetAlpha, fadeDuration)
                .SetTarget(_canvasGroup)
                .SetEase(Ease.Linear)
                .SetUpdate(true);
            await tween.ToUniTask(cancellationToken: cancellationToken);

            if (visible)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
