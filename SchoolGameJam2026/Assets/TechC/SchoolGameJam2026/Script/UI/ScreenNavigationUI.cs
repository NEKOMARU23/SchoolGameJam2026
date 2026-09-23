using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.Core;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 画面遷移UIの制御（描画専用）
    /// </summary>
    public class ScreenNavigationUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- 画面パネル登録 ---")]
        [SerializeField] private GameObject monitorPanel;
        [SerializeField] private GameObject interrogationPanel;
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private GameObject resultPanel;

        [Header("--- ダイアログ・モーダル登録 ---")]
        [SerializeField] private GameObject resetConfirmModal;
        [SerializeField] private GameObject dayEndConfirmModal;

        private GameObject currentActivePanel;
        private GameObject previousPanel;

        private void Start()
        {
            CloseAllModals();
        }

        #region パネル表示切り替え（描画専用）

        public void ShowMonitorPanel()
        {
            SwitchPanel(monitorPanel);
        }

        public void ShowInterrogationPanel()
        {
            SwitchPanel(interrogationPanel);
        }

        public void ShowProfilePanel()
        {
            SwitchPanel(profilePanel);
        }

        public void ShowResultPanel()
        {
            SwitchPanel(resultPanel);
        }

        /// <summary>
        /// 1つ前の画面に戻る
        /// </summary>
        public void BackToPreviousPanel()
        {
            if (previousPanel != null)
            {
                SwitchPanel(previousPanel);
            }
        }

        private void SwitchPanel(GameObject targetPanel)
        {
            if (targetPanel == null) return;

            if (currentActivePanel != null && currentActivePanel != targetPanel)
            {
                previousPanel = currentActivePanel;
                currentActivePanel.SetActive(false);
            }

            targetPanel.SetActive(true);
            currentActivePanel = targetPanel;
        }

        #endregion

        #region モーダル・確認ダイアログ制御（描画専用）

        public void ShowResetConfirmModal(bool isShow)
        {
            if (resetConfirmModal != null)
            {
                resetConfirmModal.SetActive(isShow);
            }
        }

        public void ShowDayEndConfirmModal(bool isShow)
        {
            if (dayEndConfirmModal != null)
            {
                dayEndConfirmModal.SetActive(isShow);
            }
        }

        public void CloseAllModals()
        {
            ShowResetConfirmModal(false);
            ShowDayEndConfirmModal(false);
        }

        #endregion

        #region UIボタンイベント通知


        public void OnClickConfirmReset()
        {
            ShowResetConfirmModal(false);
            if (gameUIController != null)
            {
                gameUIController.OnConfirmResetDay();
            }
        }

        public void OnClickConfirmDayEnd()
        {
            ShowDayEndConfirmModal(false);
            if (gameUIController != null)
            {
                gameUIController.OnConfirmEndDay();
            }
        }

        #endregion
    }
}