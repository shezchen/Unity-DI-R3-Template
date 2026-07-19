using System;
using System.Threading;
using Architecture;
using Architecture.Audio;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Generated;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using VContainer;

namespace UI
{
    /// <summary>
    /// 主界面 Page
    /// </summary>
    [RequireComponent(typeof(UIBinder), typeof(GraphicRaycaster))]
    public sealed class MainScenePage : MonoBehaviour, IBasePage
    {
        [Header("按任意键")]
        [SerializeField]
        private CanvasGroup pressAnyButton;

        [SerializeField]
        private Vector2 slideOffset;

        [SerializeField]
        private float slideDuration;

        [Header("主页面")]
        [SerializeField]
        private CanvasGroup mainSceneContent;

        [SerializeField]
        private float mainSceneDuration;

        [SerializeField]
        private Button defaultSelectedButton;

        [Inject] private ISfxPlayer _sfxPlayer;
        [Inject] private IPageNavigator _navigator;

        private UIBinder _uiBinder;
        private GraphicRaycaster _raycaster;
        private IDisposable _anyButtonSubscription;
        private Tween _transition;

        private void Awake()
        {
            _raycaster = GetComponent<GraphicRaycaster>();
            _uiBinder = GetComponent<UIBinder>();

            // 绑定设置按钮事件
            _uiBinder.Get<Button>("Button_Settings").OnClickAsObservable().Subscribe((_) =>
            {
                _sfxPlayer.PlayAsync(new SfxCueId(AudioClipName.SFX.ClickSound))
                    .Forget(exception =>
                    {
                        if (exception is not OperationCanceledException)
                        {
                            Debug.LogError(
                                $"[MainScenePage] Settings click SFX boundary failed unexpectedly.\n{exception}");
                        }
                    });
                OpenSettingsAsync().Forget(exception =>
                {
                    if (exception is not OperationCanceledException)
                    {
                        Debug.LogError(
                            $"[MainScenePage] Open settings boundary failed unexpectedly.\n{exception}");
                    }
                });
            }).AddTo(this);
        }

        private void OnDestroy() => ReleaseTransientState();

        public async UniTask OnEnter(CancellationToken cancellationToken)
        {
            if (_raycaster != null) _raycaster.enabled = true;

            // 播放"按任意键"入场动画
            pressAnyButton.gameObject.SetActive(true);
            var canvasGroup = pressAnyButton;
            var pos = pressAnyButton.transform.localPosition;
            pressAnyButton.transform.localPosition -= (Vector3)slideOffset;
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(pressAnyButton.transform
                .DOLocalMove(pos, slideDuration)
                .SetTarget(pressAnyButton.transform)
                .SetEase(Ease.OutQuad)
                .SetUpdate(false));
            seq.Join(canvasGroup
                .DOFade(1f, slideDuration)
                .SetTarget(canvasGroup)
                .SetEase(Ease.Linear)
                .SetUpdate(true));
            await AwaitTransitionAsync(seq, cancellationToken);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // 等待任意按键
            _anyButtonSubscription?.Dispose();
            _anyButtonSubscription = InputSystem.onAnyButtonPress.CallOnce((_) =>
            {
                _anyButtonSubscription = null;
                HandleAnyButtonAsync(this.GetCancellationTokenOnDestroy())
                    .Forget(exception =>
                    {
                        if (exception is not OperationCanceledException)
                        {
                            Debug.LogError(
                                $"[MainScenePage] Any-button boundary failed unexpectedly.\n{exception}");
                        }
                    });
            });
        }

        public UniTask OnPause(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseTransientState();
            if (_raycaster != null) _raycaster.enabled = false;
            gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }

        public UniTask OnResume(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            gameObject.SetActive(true);
            if (_raycaster != null) _raycaster.enabled = true;
            defaultSelectedButton.Select();
            return UniTask.CompletedTask;
        }

        public UniTask OnExit(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseTransientState();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 按任意键之后，显示主页面的全部内容
        /// </summary>
        private async UniTask HandleAnyButtonAsync(CancellationToken cancellationToken)
        {
            _sfxPlayer.PlayAsync(
                new SfxCueId(AudioClipName.SFX.ClickSound),
                cancellationToken: cancellationToken)
                .Forget(exception =>
                {
                    if (exception is not OperationCanceledException)
                    {
                        Debug.LogError(
                            $"[MainScenePage] Any-button SFX boundary failed unexpectedly.\n{exception}");
                    }
                });

            var currentPos = pressAnyButton.transform.localPosition;
            pressAnyButton.interactable = false;
            pressAnyButton.blocksRaycasts = false;
            var hideSequence = DOTween.Sequence().SetUpdate(true);
            hideSequence.Append(pressAnyButton.transform
                .DOLocalMove(currentPos + (Vector3)slideOffset, slideDuration)
                .SetTarget(pressAnyButton.transform)
                .SetEase(Ease.OutQuad)
                .SetUpdate(false));
            hideSequence.Join(pressAnyButton
                .DOFade(0f, slideDuration)
                .SetTarget(pressAnyButton)
                .SetEase(Ease.Linear)
                .SetUpdate(true));
            await AwaitTransitionAsync(hideSequence, cancellationToken);

            pressAnyButton.gameObject.SetActive(false);
            await ShowMainScene(cancellationToken);
        }

        private async UniTask ShowMainScene(CancellationToken cancellationToken)
        {
            mainSceneContent.gameObject.SetActive(true);
            mainSceneContent.alpha = 0;
            mainSceneContent.interactable = false;
            mainSceneContent.blocksRaycasts = false;
            await AwaitTransitionAsync(
                mainSceneContent
                    .DOFade(1f, mainSceneDuration)
                    .SetTarget(mainSceneContent)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true),
                cancellationToken);
            mainSceneContent.interactable = true;
            mainSceneContent.blocksRaycasts = true;
            defaultSelectedButton.Select();
        }

        private async UniTask OpenSettingsAsync()
        {
            var result = await _navigator.PushAsync<SettingsPage>(
                AddressableKeys.Assets.SettingsPagePrefab,
                this.GetCancellationTokenOnDestroy());
            if (!result.IsSuccess)
            {
                Debug.LogError($"[MainScenePage] Open settings failed: {result.Status}. {result.Error}");
            }
        }

        private async UniTask AwaitTransitionAsync(Tween tween, CancellationToken cancellationToken)
        {
            _transition?.Kill();
            _transition = tween;
            try
            {
                await tween.ToUniTask(cancellationToken: cancellationToken);
            }
            finally
            {
                if (ReferenceEquals(_transition, tween))
                {
                    _transition = null;
                }
            }
        }

        private void ReleaseTransientState()
        {
            _anyButtonSubscription?.Dispose();
            _anyButtonSubscription = null;
            _transition?.Kill();
            _transition = null;
        }
    }
}
