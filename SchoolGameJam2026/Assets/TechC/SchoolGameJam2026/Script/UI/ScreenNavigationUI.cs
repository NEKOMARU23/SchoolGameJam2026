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

            // 初期状態としてMonitorPanelを現在地に設定（バック機能の起点）
            if (monitorPanel != null)
            {
                currentActivePanel = monitorPanel;
                monitorPanel.SetActive(true);
            }
        }

        #region パネル表示切り替え（描画専用）

        /// <summary>
        /// モニターパネルを表示する
        /// </summary>
        public void ShowMonitorPanel()
        {
            SwitchPanel(monitorPanel);
        }

        /// <summary>
        /// 質問パネルを表示する
        /// </summary>
        public void ShowInterrogationPanel()
        {
            SwitchPanel(interrogationPanel);
        }

        /// <summary>
        /// プロファイルパネルを表示する
        /// </summary>
        public void ShowProfilePanel()
        {
            SwitchPanel(profilePanel);
        }

        /// <summary>
        /// 結果パネルを表示する
        /// </summary>  
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

            // すでに表示されているパネルを再度開こうとした場合は上書きを防ぐためスキップ
            if (currentActivePanel == targetPanel) return;

            if (currentActivePanel != null)
            {
                previousPanel = currentActivePanel;
                currentActivePanel.SetActive(false);
            }

            targetPanel.SetActive(true);
            currentActivePanel = targetPanel;
        }

        #endregion

        #region モーダル・確認ダイアログ制御（描画専用）

        /// <summary>
        /// リセット確認モーダルの表示切り替え
        /// </summary>
        /// <param name="isShow"></param>
        public void ShowResetConfirmModal(bool isShow)
        {
            if (resetConfirmModal != null)
            {
                resetConfirmModal.SetActive(isShow);
            }
        }

        /// <summary>
        /// 1日の終了確認モーダルの表示切り替え
        /// </summary>
        /// <param name="isShow"></param>
        public void ShowDayEndConfirmModal(bool isShow)
        {
            if (dayEndConfirmModal != null)
            {
                dayEndConfirmModal.SetActive(isShow);
            }
        }

        /// <summary>
        /// すべてのモーダルを閉じる
        /// </summary>
        public void CloseAllModals()
        {
            ShowResetConfirmModal(false);
            ShowDayEndConfirmModal(false);
        }

        #endregion

        #region UIボタンイベント通知

        /// <summary>
        /// リセット確認モーダルの「はい」ボタンが押されたときの処理
        /// </summary>
        public void OnClickConfirmReset()
        {
            ShowResetConfirmModal(false);
            if (gameUIController != null)
            {
                gameUIController.OnConfirmResetDay();
            }
        }

        /// <summary>
        /// 1日の終了確認モーダルの「はい」ボタンが押されたときの処理
        /// </summary>
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