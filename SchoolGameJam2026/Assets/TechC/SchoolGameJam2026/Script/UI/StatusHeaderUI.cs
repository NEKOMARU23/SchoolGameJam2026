using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 日数・所持ポイント・行動回数の表示を担当するビュー
    /// （同じ情報を複数の画面に出すため、表示先はリストで登録する）
    /// </summary>
    public class StatusHeaderUI : MonoBehaviour
    {
        [Header("--- ステータス表示テキスト ---")]
        [SerializeField] private List<TextMeshProUGUI> dayTexts = new List<TextMeshProUGUI>();
        [SerializeField] private List<TextMeshProUGUI> pointTexts = new List<TextMeshProUGUI>();
        [SerializeField] private List<TextMeshProUGUI> actionTexts = new List<TextMeshProUGUI>();

        /// <summary>
        /// 日数表示を更新
        /// </summary>
        public void UpdateDayDisplay(int day)
        {
            SetTexts(dayTexts, $"{day}日目");
        }

        /// <summary>
        /// 所持ポイント表示を更新
        /// </summary>
        public void UpdatePointDisplay(int points)
        {
            SetTexts(pointTexts, $"ポイント: {points}");
        }

        /// <summary>
        /// 行動回数表示を更新（初めて遊ぶ人に分かりやすいよう、使った回数ではなく残りの回数を出す）
        /// </summary>
        public void UpdateActionCountDisplay(int currentActions, int maxLimit)
        {
            int remaining = Mathf.Max(0, maxLimit - currentActions);
            SetTexts(actionTexts, $"残り行動: {remaining} / {maxLimit}");
        }

        private static void SetTexts(List<TextMeshProUGUI> texts, string value)
        {
            foreach (var text in texts)
            {
                if (text != null) text.text = value;
            }
        }
    }
}
