using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 画面遷移UIの制御（描画専用）
    /// </summary>
    public class ScreenNavigationUI : MonoBehaviour
    {
        [Header("--- 画面パネル登録 ---")]
        [SerializeField] private GameObject monitorPanel;
        [SerializeField] private GameObject interrogationPanel;
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private GameObject smartphoneTalkPanel;

        [Header("--- ダイアログ・モーダル登録 ---")]
        [SerializeField] private GameObject resetConfirmModal;
        [SerializeField] private GameObject dayEndConfirmModal;

        private GameObject currentActivePanel;
        
        // 遷移履歴を積むスタック構造
        private readonly Stack<GameObject> historyStack = new Stack<GameObject>();

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
        /// モニターパネルを表示する（通常遷移）
        /// </summary>
        public void ShowMonitorPanel()
        {
            // モニター画面（トップ画面など）に戻る際は履歴をクリアしたい場合、必要に応じて ClearHistory() できます
            SwitchPanel(monitorPanel);
        }

        /// <summary>
        /// 質問パネルを表示する（通常遷移）
        /// </summary>
        public void ShowInterrogationPanel()
        {
            SwitchPanel(interrogationPanel);
        }

        /// <summary>
        /// プロファイルパネルを表示する（通常遷移）
        /// </summary>
        public void ShowProfilePanel()
        {
            SwitchPanel(profilePanel);
        }

        /// <summary>
        /// 結果パネルを表示する（通常遷移）
        /// </summary>  
        public void ShowResultPanel()
        {
            SwitchPanel(resultPanel);
        }

        /// <summary>
        /// スマホ（マキナとの会話）パネルを表示する（通常遷移）
        /// </summary>
        public void ShowSmartphoneTalkPanel()
        {
            SwitchPanel(smartphoneTalkPanel);
        }

        /// <summary>
        /// 1つ前の画面に戻る（バック処理専用）
        /// </summary>
        public void BackToPreviousPanel()
        {
            if (historyStack.Count == 0) return;

            // スタックから1つ前のパネルを取り出す
            GameObject previousPanel = historyStack.Pop();
            if (previousPanel == null) return;

            // 現在のパネルを非表示にする
            if (currentActivePanel != null)
            {
                currentActivePanel.SetActive(false);
            }

            // 取り出したパネルを表示し、現在地にセット（履歴追加は行わない）
            previousPanel.SetActive(true);
            currentActivePanel = previousPanel;
        }

        /// <summary>
        /// 通常の画面遷移（履歴をPushする）
        /// </summary>
        private void SwitchPanel(GameObject targetPanel)
        {
            if (targetPanel == null) return;

            // すでに表示されているパネルを再度開こうとした場合は上書きを防ぐためスキップ
            if (currentActivePanel == targetPanel) return;

            if (currentActivePanel != null)
            {
                // 現在のパネルを履歴スタックに保存
                historyStack.Push(currentActivePanel);
                currentActivePanel.SetActive(false);
            }

            targetPanel.SetActive(true);
            currentActivePanel = targetPanel;
        }

        /// <summary>
        /// 必要に応じて画面履歴をリセットする処理
        /// </summary>
        public void ClearHistory()
        {
            historyStack.Clear();
        }

        #endregion

        #region モーダル・確認ダイアログ制御（描画専用）

        /// <summary>
        /// リセット確認モーダルの表示切り替え
        /// </summary>
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
    }
}