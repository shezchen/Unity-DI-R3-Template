using System;
using System.Threading;
using Architecture;
using Architecture.Audio;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Generated;
using R3;
using Tools;
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
    public class MainScenePage : MonoBehaviour, IBasePage
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
                    .ForgetLogged("[MainScenePage] Settings click SFX boundary");
                OpenSettingsAsync().ForgetLogged("[MainScenePage] Open settings boundary");
            }).AddTo(this);
        }

        #region IBasePage 实现

        public async UniTask OnEnter(CancellationToken cancellationToken)
        {
            if (_raycaster != null) _raycaster.enabled = true;

            // 播放"按任意键"入场动画
            pressAnyButton.gameObject.SetActive(true);
            var canvasGroup = pressAnyButton;
            var pos = pressAnyButton.transform.localPosition;
            pressAnyButton.transform.localPosition -= (Vector3)slideOffset;
            canvasGroup.alpha = 0;

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(pressAnyButton.transform.LocalMoveTo(pos, slideDuration));
            seq.Join(canvasGroup.FadeIn(slideDuration));
            await AwaitTransitionAsync(seq, cancellationToken);

            // 等待任意按键
            _anyButtonSubscription?.Dispose();
            _anyButtonSubscription = InputSystem.onAnyButtonPress.CallOnce((_) =>
            {
                _anyButtonSubscription = null;
                HandleAnyButtonAsync(this.GetCancellationTokenOnDestroy())
                    .ForgetLogged("[MainScenePage] Any-button boundary");
            });
        }

        public async UniTask OnPause(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _anyButtonSubscription?.Dispose();
            _anyButtonSubscription = null;
            _transition?.Kill();
            _transition = null;
            if (_raycaster != null) _raycaster.enabled = false;
            gameObject.SetActive(false);
            await UniTask.CompletedTask;
        }

        public async UniTask OnResume(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            gameObject.SetActive(true);
            if (_raycaster != null) _raycaster.enabled = true;
            defaultSelectedButton.Select();
            await UniTask.CompletedTask;
        }

        public async UniTask OnExit(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _anyButtonSubscription?.Dispose();
            _anyButtonSubscription = null;
            _transition?.Kill();
            _transition = null;
            await UniTask.CompletedTask;
        }

        #endregion

        /// <summary>
        /// 按任意键之后，显示主页面的全部内容
        /// </summary>
        private async UniTask HandleAnyButtonAsync(CancellationToken cancellationToken)
        {
            _sfxPlayer.PlayAsync(
                new SfxCueId(AudioClipName.SFX.ClickSound),
                cancellationToken: cancellationToken)
                .ForgetLogged("[MainScenePage] Any-button SFX boundary");

            var currentPos = pressAnyButton.transform.localPosition;
            var hideSequence = DOTween.Sequence().SetUpdate(true);
            hideSequence.Append(pressAnyButton.transform.LocalMoveTo(
                currentPos + (Vector3)slideOffset,
                slideDuration));
            hideSequence.Join(pressAnyButton.FadeOut(slideDuration));
            await AwaitTransitionAsync(hideSequence, cancellationToken);

            pressAnyButton.gameObject.SetActive(false);
            await ShowMainScene(cancellationToken);
        }

        private async UniTask ShowMainScene(CancellationToken cancellationToken)
        {
            mainSceneContent.gameObject.SetActive(true);
            mainSceneContent.alpha = 0;
            await AwaitTransitionAsync(
                mainSceneContent.FadeIn(mainSceneDuration),
                cancellationToken);
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
    }
}
