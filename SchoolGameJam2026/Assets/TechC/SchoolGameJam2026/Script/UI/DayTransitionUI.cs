using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 日付切り替え時のロード画面風演出を担当するビュー
    /// 画面全体を覆っている間に、呼び出し側が日付の更新や画面の切り替えを行う
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DayTransitionUI : MonoBehaviour
    {
        private const string LoadingLabel = "Now Loading";
        private const int MaxLoadingDots = 3;

        [Header("--- 表示パーツ ---")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI loadingText;

        [Header("--- 演出時間（秒） ---")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.4f;
        [SerializeField, Min(0f)] private float holdDuration = 1.2f;
        [SerializeField, Min(0.05f)] private float loadingDotInterval = 0.3f;

        /// <summary> 覆い始めてから外し終わるまでの間 true </summary>
        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            SetCover(0f, false);
        }

        /// <summary>
        /// 演出時間を変更する（テストで演出を省略するときなどに使用）
        /// </summary>
        public void SetDurations(float fade, float hold)
        {
            fadeDuration = Mathf.Max(0f, fade);
            holdDuration = Mathf.Max(0f, hold);
        }

        /// <summary>
        /// 画面を覆い、一定時間ロード表示を見せる（完了時点で画面は完全に覆われている）
        /// </summary>
        /// <param name="label">中央に表示する文字（例: "2日目"）</param>
        public async UniTask CoverAsync(string label, CancellationToken token)
        {
            IsPlaying = true;
            if (dayText != null) dayText.text = label;
            UpdateLoadingText(0);

            // 演出中は下の画面のボタンを押せないようにする
            canvasGroup.blocksRaycasts = true;

            await FadeAsync(1f, token);
            await HoldAsync(token);
        }

        /// <summary>
        /// 覆いを外して元の画面に戻す
        /// </summary>
        public async UniTask RevealAsync(CancellationToken token)
        {
            await FadeAsync(0f, token);
            SetCover(0f, false);
            IsPlaying = false;
        }

        private async UniTask FadeAsync(float targetAlpha, CancellationToken token)
        {
            if (fadeDuration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
                return;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            canvasGroup.alpha = targetAlpha;
        }

        private async UniTask HoldAsync(CancellationToken token)
        {
            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                // "Now Loading" の後ろの点を 0〜3 個で繰り返す
                UpdateLoadingText((int)(elapsed / loadingDotInterval) % (MaxLoadingDots + 1));

                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private void UpdateLoadingText(int dotCount)
        {
            if (loadingText != null) loadingText.text = LoadingLabel + new string('.', dotCount);
        }

        private void SetCover(float alpha, bool blocksRaycasts)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }
    }
}
