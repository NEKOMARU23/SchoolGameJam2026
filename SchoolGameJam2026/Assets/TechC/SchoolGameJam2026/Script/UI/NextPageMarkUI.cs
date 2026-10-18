using System;
using TMPro;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 会話の文字が全部出たら、文の最後のすぐ後ろに「▼」を点滅させて出す（クリックで次へ進めることを知らせる。描画専用）
    /// 会話の文そのものには混ぜないので、ログや文字送りには影響しない
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class NextPageMarkUI : MonoBehaviour
    {
        [Header("--- 「▼」を出す会話の文 ---")]
        [SerializeField] private TMP_Text targetText;

        [Header("--- 位置と点滅 ---")]
        [Tooltip("最後の文字からの横のすき間")]
        [SerializeField] private float gap = 8f;
        [Tooltip("点滅の速さ（1秒に何回、消えて出るか）")]
        [SerializeField, Min(0.1f)] private float blinkPerSecond = 1.2f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.15f;

        private TMP_Text markText;
        private RectTransform markRect;
        private Func<bool> canAdvance;

        /// <summary> 「▼」を出しているか </summary>
        public bool IsShowing { get; private set; }

        private void Awake()
        {
            markText = GetComponent<TMP_Text>();
            markRect = (RectTransform)transform;
            SetVisible(false);
        }

        /// <summary>
        /// 今クリックで次へ進めるかを調べる処理を設定する（GameUIController から渡される）
        /// </summary>
        public void SetCondition(Func<bool> condition)
        {
            canAdvance = condition;
        }

        private void LateUpdate()
        {
            Vector2 position = Vector2.zero;
            bool show = canAdvance != null && canAdvance() && TryGetMarkPosition(out position);
            if (show)
            {
                markRect.localPosition = position;
                float t = Mathf.PingPong(Time.unscaledTime * blinkPerSecond * 2f, 1f);
                Color c = markText.color;
                c.a = Mathf.Lerp(minAlpha, 1f, t);
                markText.color = c;
            }
            SetVisible(show);
        }

        /// <summary>
        /// 文字が全部出ていれば、最後の文字のすぐ後ろの位置（会話の文の中の座標）を返す
        /// </summary>
        private bool TryGetMarkPosition(out Vector2 position)
        {
            position = Vector2.zero;
            if (targetText == null || !targetText.isActiveAndEnabled) return false;

            TMP_TextInfo info = targetText.textInfo;
            if (info == null || info.characterCount == 0) return false;

            // 文字送りの途中は出さない（全部出たときは maxVisibleCharacters が文字数以上になる）
            if (targetText.maxVisibleCharacters < info.characterCount) return false;

            for (int i = info.characterCount - 1; i >= 0; i--)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible) continue;

                // 会話の文と「▼」は親子なので、会話の文の中の座標をそのまま使える
                position = new Vector2(ch.xAdvance + gap, ch.baseLine);
                return true;
            }
            return false;
        }

        private void SetVisible(bool visible)
        {
            IsShowing = visible;
            if (markText != null) markText.enabled = visible;
        }
    }
}
