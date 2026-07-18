using DG.Tweening;
using UnityEngine;

namespace Tools
{
    /// <summary>
    /// DOTween extensions used by the template UI. Callers own the returned Tween.
    /// </summary>
    public static class DOTweenTool
    {
        public static Tweener LocalMoveTo(
            this Transform transform,
            Vector2 localPosition,
            float duration,
            bool snapping = false,
            Ease ease = Ease.OutQuad,
            bool independentUpdate = false)
        {
            if (transform == null) return null;

            return transform
                .DOLocalMove(
                    new Vector3(localPosition.x, localPosition.y, transform.localPosition.z),
                    duration,
                    snapping)
                .SetTarget(transform)
                .SetEase(ease)
                .SetUpdate(independentUpdate);
        }

        public static Tweener FadeIn(
            this CanvasGroup canvasGroup,
            float duration,
            bool setInteractable = true,
            Ease ease = Ease.Linear,
            bool independentUpdate = true) =>
            FadeTo(canvasGroup, 1f, duration, setInteractable, ease, independentUpdate);

        public static Tweener FadeOut(
            this CanvasGroup canvasGroup,
            float duration,
            bool setInteractable = true,
            Ease ease = Ease.Linear,
            bool independentUpdate = true) =>
            FadeTo(canvasGroup, 0f, duration, setInteractable, ease, independentUpdate);

        public static Tween Breath(
            this CanvasGroup canvasGroup,
            float minAlpha = 0.6f,
            float maxAlpha = 1f,
            float duration = 1f,
            int loops = -1,
            Ease ease = Ease.InOutSine,
            bool independentUpdate = true)
        {
            if (canvasGroup == null) return null;

            minAlpha = Mathf.Clamp01(minAlpha);
            maxAlpha = Mathf.Clamp01(maxAlpha);
            if (minAlpha > maxAlpha)
            {
                var swap = minAlpha;
                minAlpha = maxAlpha;
                maxAlpha = swap;
            }

            canvasGroup.alpha = maxAlpha;
            return canvasGroup
                .DOFade(minAlpha, duration)
                .SetTarget(canvasGroup)
                .SetEase(ease)
                .SetLoops(loops, LoopType.Yoyo)
                .SetUpdate(independentUpdate);
        }

        private static Tweener FadeTo(
            CanvasGroup canvasGroup,
            float alpha,
            float duration,
            bool setInteractable,
            Ease ease,
            bool independentUpdate)
        {
            if (canvasGroup == null) return null;

            if (setInteractable)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            var tween = canvasGroup
                .DOFade(Mathf.Clamp01(alpha), duration)
                .SetTarget(canvasGroup)
                .SetEase(ease)
                .SetUpdate(independentUpdate);

            if (setInteractable && alpha > 0f)
            {
                tween.OnComplete(() =>
                {
                    if (canvasGroup == null) return;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                });
            }

            return tween;
        }
    }
}
