using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// リセット確認ダイアログUIの制御クラス
    /// </summary>
    public class ResetConfirmDialogUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- ボタン参照 ---")]
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private void Awake()
        {
            if (yesButton != null)
            {
                yesButton.onClick.AddListener(OnClickYes);
            }

            if (noButton != null)
            {
                noButton.onClick.AddListener(OnClickNo);
            }
        }

        /// <summary>
        /// ダイアログを表示
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// ダイアログを非表示
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnClickYes()
        {
            if (gameUIController != null)
            {
                gameUIController.OnConfirmResetDay();
            }
            Hide();
        }

        private void OnClickNo()
        {
            Hide();
        }
    }
}