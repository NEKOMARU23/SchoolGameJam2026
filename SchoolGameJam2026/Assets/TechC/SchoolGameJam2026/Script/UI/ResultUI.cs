using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    public class ResultUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- UI表示要素 ---")]
        [SerializeField] private TextMeshProUGUI detailsText;    // 本日のできごと / 取り調べログ
        [SerializeField] private TextMeshProUGUI pointText;      // 獲得ポイント表示

        [Header("--- ボタン要素 ---")]
        [SerializeField] private Button nextDayButton;           // NextDay_Button

        private void Start()
        {
            if (nextDayButton != null)
            {
                nextDayButton.onClick.AddListener(OnClickNextDay);
            }
        }

        /// <summary>
        /// リザルト画面の描画更新と表示
        /// </summary>
        public void DisplayResult(string title, string details, int earnedPoints)
        {

            if (detailsText != null)
            {
                detailsText.text = details;
            }

            if (pointText != null)
            {
                string sign = earnedPoints >= 0 ? "+" : "";
                pointText.text = $"獲得ポイント: {sign}{earnedPoints} pt";
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 「次の日へ」ボタン押下時の処理
        /// </summary>
        private void OnClickNextDay()
        {
            if (gameUIController != null)
            {
                gameUIController.OnClickCloseResultAndAdvanceDay();
            }
            gameObject.SetActive(false);
        }
    }
}