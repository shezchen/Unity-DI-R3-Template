using DG.Tweening;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class BreathableButtonIndicator : MonoBehaviour
    {
        [Header("Breath Settings")]
        [SerializeField]
        private bool canBreathe = true;
        
        [SerializeField, Range(0.1f, 5f), Tooltip("Duration for one breath cycle (max to min)")]
        private float duration = 1f;

        [SerializeField, Range(0f, 1f)]
        private float minAlpha = 0.4f;

        [SerializeField, Range(0f, 1f)]
        private float maxAlpha = 1f;

        [SerializeField]
        private Ease easeType = Ease.InOutSine;

        [SerializeField]
        private bool independentUpdate = true;

        private CanvasGroup _canvasGroup;
        private Tween _breathTween;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (canBreathe)
            {
                StartBreathing();
            }
            
        }

        private void OnDisable()
        {
            StopBreathing();
        }

        private void StartBreathing()
        {
            if (_canvasGroup == null) return;

            StopBreathing();

            var clampedMinAlpha = Mathf.Clamp01(minAlpha);
            var clampedMaxAlpha = Mathf.Clamp01(maxAlpha);
            if (clampedMinAlpha > clampedMaxAlpha)
            {
                (clampedMinAlpha, clampedMaxAlpha) = (clampedMaxAlpha, clampedMinAlpha);
            }

            _canvasGroup.alpha = clampedMaxAlpha;
            _breathTween = _canvasGroup
                .DOFade(clampedMinAlpha, duration)
                .SetTarget(_canvasGroup)
                .SetEase(easeType)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(independentUpdate);
        }

        private void StopBreathing()
        {
            if (_breathTween != null && _breathTween.IsActive())
            {
                _breathTween.Kill();
                _breathTween = null;
            }
            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = maxAlpha;
            }
        }

    }
}
