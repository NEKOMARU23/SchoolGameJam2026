using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.Core;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    public class ResultUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- リザルト表示パーツ ---")]
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI earnedPointsText;

        [Header("--- 操作ボタン ---")]
        [SerializeField] private Button advanceDayButton;

        private void Awake()
        {
            if (advanceDayButton != null)
            {
                advanceDayButton.onClick.AddListener(OnClickAdvanceDay);
            }
        }

        #region 描画専用メソッド

        /// <summary>
        /// リザルト画面の各種テキストを描画（表示専用）
        /// </summary>
        public void DisplayResult(string title, string mainContent, int earnedPoints)
        {
            if (resultTitleText != null)
            {
                resultTitleText.text = title;
            }

            if (resultText != null)
            {
                resultText.text = mainContent;
            }

            if (earnedPointsText != null)
            {
                earnedPointsText.text = $"+{earnedPoints} pt";
            }
        }

        #endregion

        #region UIボタンイベント通知

        private void OnClickAdvanceDay()
        {
            if (gameUIController != null)
            {
                gameUIController.OnClickCloseResultAndAdvanceDay();
            }
        }

        #endregion
    }
}