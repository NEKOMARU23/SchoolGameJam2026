using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 行動で入ったポイントを「+20pt」のように短く出して消す（取調室用。描画専用）
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PointGainUI : MonoBehaviour
    {
        [Header("--- 表示パーツ ---")]
        [SerializeField] private TextMeshProUGUI pointText;

        [Header("--- 演出（秒） ---")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
        [SerializeField, Min(0f)] private float showDuration = 1.5f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [Tooltip("出ている間に上へ動く量")]
        [SerializeField] private float riseAmount = 20f;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Vector2 basePosition;
        private CancellationTokenSource showCts;

        /// <summary> 表示中か </summary>
        public bool IsShowing { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = (RectTransform)transform;
            basePosition = rectTransform.anchoredPosition;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void OnDisable()
        {
            // 画面を離れたら消す
            Hide();
        }

        /// <summary>
        /// 入ったポイントを出す（0以下なら何も出さない）
        /// </summary>
        public void Show(int points)
        {
            if (points <= 0 || !isActiveAndEnabled) return;

            if (pointText != null) pointText.text = $"+{points}pt";

            showCts?.Cancel();
            showCts?.Dispose();
            showCts = new CancellationTokenSource();
            PlayAsync(showCts.Token).Forget();
        }

        public void Hide()
        {
            showCts?.Cancel();
            showCts?.Dispose();
            showCts = null;
            IsShowing = false;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (rectTransform != null) rectTransform.anchoredPosition = basePosition;
        }

        private async UniTaskVoid PlayAsync(CancellationToken token)
        {
            IsShowing = true;
            float total = fadeInDuration + showDuration + fadeOutDuration;
            float elapsed = 0f;

            while (elapsed < total)
            {
                float alpha = elapsed < fadeInDuration ? elapsed / Mathf.Max(fadeInDuration, 0.0001f)
                    : elapsed < fadeInDuration + showDuration ? 1f
                    : 1f - (elapsed - fadeInDuration - showDuration) / Mathf.Max(fadeOutDuration, 0.0001f);
                canvasGroup.alpha = Mathf.Clamp01(alpha);
                rectTransform.anchoredPosition = basePosition + Vector2.up * (riseAmount * Mathf.Clamp01(elapsed / Mathf.Max(total, 0.0001f)));

                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (canceled) return;
                elapsed += Time.unscaledDeltaTime;
            }

            canvasGroup.alpha = 0f;
            rectTransform.anchoredPosition = basePosition;
            IsShowing = false;
        }
    }
}
