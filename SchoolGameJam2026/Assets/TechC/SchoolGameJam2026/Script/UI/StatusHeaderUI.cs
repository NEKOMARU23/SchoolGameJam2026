using UnityEngine;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    public class StatusHeaderUI : MonoBehaviour
    {
        [Header("--- ステータス表示テキスト ---")]
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI actionCountText;
        [SerializeField] private TextMeshProUGUI pointText;

        /// <summary>
        /// 日数表示を更新
        /// </summary>
        public void UpdateDayDisplay(int day)
        {
            if (dayText != null)
            {
                dayText.text = $"{day} 日目";
            }
        }

        /// <summary>
        /// 所持ポイント表示を更新
        /// </summary>
        public void UpdatePointDisplay(int points)
        {
            if (pointText != null)
            {
                pointText.text = $"{points} pt";
            }
        }

        /// <summary>
        /// 残り行動回数表示を更新
        /// </summary>
        public void UpdateActionCountDisplay(int currentActions, int maxLimit)
        {
            if (actionCountText != null)
            {
                int remaining = Mathf.Max(0, maxLimit - currentActions);
                actionCountText.text = $"残り行動: {remaining}/{maxLimit}";
            }
        }
    }
}