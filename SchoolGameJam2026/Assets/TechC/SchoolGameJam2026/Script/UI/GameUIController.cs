using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using TechC.SchoolGameJam2026.Scripts.UI;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.Core
{
    public class GameUIController : MonoBehaviour
    {
        [Header("--- 各機能UI（ビュー）の参照 ---")]
        [SerializeField] private ScreenNavigationUI navigationUI;
        [SerializeField] private InterrogationUI interrogationUI;
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private ProfileUI profileUI;
        [SerializeField] private MonitorUI monitorUI;
        [SerializeField] private ResultUI resultUI;

        [Header("--- ゲーム進行設定 ---")]
        [SerializeField] private int finalDayNumber = 7;
        [SerializeField] private string clearSceneName = "ClearScene";
        [SerializeField] private string gameOverSceneName = "GameOverScene";

        // 内部状態管理
        private string currentSelectedCharacterId = "A";
        private string currentHearingTargetId = "B";
        private bool hasEscapedAI = false;
        private bool isAiIdentified = false;

        private void OnEnable()
        {
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.OnPointsChanged += HandlePointsChanged;
                InterrogationManager.Instance.OnActionCountChanged += HandleActionCountChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayStarted += HandleDayStarted;
                GameManager.Instance.OnDayReset += HandleDayReset;
            }
        }

        private void OnDisable()
        {
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.OnPointsChanged -= HandlePointsChanged;
                InterrogationManager.Instance.OnActionCountChanged -= HandleActionCountChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayStarted -= HandleDayStarted;
                GameManager.Instance.OnDayReset -= HandleDayReset;
            }
        }

        private void Start()
        {
            if (InterrogationManager.Instance != null)
            {
                HandlePointsChanged(InterrogationManager.Instance.CurrentPoints);
                HandleActionCountChanged(InterrogationManager.Instance.CurrentDailyActions, InterrogationManager.Instance.DailyActionLimit);
            }

            if (GameManager.Instance != null)
            {
                HandleDayStarted(GameManager.Instance.CurrentDay);
            }

            RefreshAllViews();
        }

        #region イベントハンドラ（Managerからの通知を受けてViewに描画指示）

        private void HandlePointsChanged(int points)
        {
            interrogationUI.UpdatePointDisplay(points);
        }

        private void HandleActionCountChanged(int currentActions, int maxLimit)
        {
            interrogationUI.UpdateActionCountDisplay(currentActions, maxLimit);
            UpdateInterrogationState();
        }

        private void HandleDayStarted(int day)
        {
            interrogationUI.UpdateDayDisplay(day);
            if (dialogueUI != null) dialogueUI.ResetDialogueState();
            RefreshAllViews();
        }

        private void HandleDayReset()
        {
            if (dialogueUI != null)
            {
                dialogueUI.StartDialogueSequence("システム", "（本日の朝の状態に巻き戻しました）");
            }
            RefreshAllViews();
        }

        #endregion

        #region UIからの操作受付・ゲームロジックの処理

        /// <summary>
        /// キャラクター選択時の処理
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            currentSelectedCharacterId = characterId;
            if (dialogueUI != null) dialogueUI.ResetDialogueState();

            CharacterInstance targetChar = GetCharacter(characterId);
            interrogationUI.UpdateCharacterRoomDisplay(targetChar);
            profileUI.RefreshProfileDisplay(targetChar);
            
            navigationUI.ShowInterrogationPanel();
            UpdateInterrogationState();
        }

        /// <summary>
        /// 取調室アクションの実行ロジック
        /// </summary>
        public void ExecuteInterrogationAction(int actionIndex)
        {
            if (dialogueUI != null && dialogueUI.IsDialoguePlaying) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null) return;

            if (targetChar.IsReleased)
            {
                dialogueUI?.StartDialogueSequence("システム", "（このキャラクターはすでに解放されているため、取り調べできません）");
                return;
            }

            if (InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId))
            {
                dialogueUI?.StartDialogueSequence("システム", "（このキャラクターへの本日の取り調べは既に終了しています）");
                UpdateInterrogationState();
                return;
            }

            InterrogationActionType actionType = (InterrogationActionType)actionIndex;
            string rawDialogue = InterrogationManager.Instance != null 
                ? InterrogationManager.Instance.ExecuteAction(targetChar, actionType, currentHearingTargetId) 
                : "";

            string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;

            dialogueUI?.StartDialogueSequence(speakerName, rawDialogue);
            UpdateInterrogationState();
        }

        /// <summary>
        /// キャラクター解放ロジック
        /// </summary>
        public void ExecuteReleaseCharacter()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || targetChar.IsReleased) return;

            targetChar.SetReleaseFlag(true);

            if (CheckIsAI(targetChar))
            {
                hasEscapedAI = true;
                Debug.LogWarning($"<color=red>【警告】AIである {currentSelectedCharacterId} が解放されました。</color>");
            }

            string rawDialogue = InterrogationManager.Instance != null
                ? InterrogationManager.Instance.ExecuteAction(targetChar, InterrogationActionType.Release, currentHearingTargetId)
                : "";

            if (string.IsNullOrEmpty(rawDialogue) || rawDialogue == "「……」")
            {
                rawDialogue = "解放される……ということですか。わかりました。|[SYS][IMG:OFF]（対象は部屋から退出しました）";
            }

            string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;
            dialogueUI?.StartDialogueSequence(speakerName, rawDialogue);

            RefreshMonitorView();
            UpdateInterrogationState();
        }

        /// <summary>
        /// AI確定ボタン押下ロジック
        /// </summary>
        public void ExecuteConfirmAI()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null) return;

            isAiIdentified = CheckIsAI(targetChar);

            if (InterrogationManager.Instance != null)
            {
                string rawDialogue = InterrogationManager.Instance.ExecuteAction(targetChar, InterrogationActionType.ConfirmAI, currentHearingTargetId);
                string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;

                if (!string.IsNullOrEmpty(rawDialogue) && rawDialogue != "「……」")
                {
                    dialogueUI?.StartDialogueSequence(speakerName, rawDialogue);
                }
            }

            ShowResultFlow();
        }

        /// <summary>
        /// 基本プロフィールの解放を試みる（ロジック）
        /// </summary>
        public void TryUnlockBasicProfile()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            BasicProfileGroup basicGroup = targetChar.ProfileData.basicInfo;
            if (basicGroup.isUnlocked) return;

            if (InterrogationManager.Instance != null && InterrogationManager.Instance.TrySpendPoints(basicGroup.unlockCost))
            {
                basicGroup.isUnlocked = true;
                profileUI.RefreshProfileDisplay(targetChar);
            }
        }

        /// <summary>
        /// 特定プロフィールの解放を試みる（ロジック）
        /// </summary>
        public void TryUnlockProfileItem(string itemKey)
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            ProfileItem item = targetChar.ProfileData.historyInfoList.Find(i => i.itemKey == itemKey);
            if (item != null && !item.isUnlocked)
            {
                if (InterrogationManager.Instance != null && InterrogationManager.Instance.TrySpendPoints(item.unlockCost))
                {
                    item.isUnlocked = true;
                    profileUI.RefreshProfileDisplay(targetChar);
                }
            }
        }

        /// <summary>
        /// 聞き取りターゲット選択の受付
        /// </summary>
        public void OnSelectHearingTarget(string targetId)
        {
            currentHearingTargetId = targetId;
            interrogationUI.CloseHearingPanel();
            ExecuteInterrogationAction((int)InterrogationActionType.HearAbout);
        }

        /// <summary>
        /// リセット確認のダイアログ承認時の処理
        /// </summary>
        public void OnConfirmResetDay()
        {
            GameManager.Instance?.ResetCurrentDay();
        }

        /// <summary>
        /// 1日終了確認の承認時の処理
        /// </summary>
        public void OnConfirmEndDay()
        {
            ShowResultFlow();
        }

        /// <summary>
        /// 日時進行および勝敗遷移判定
        /// </summary>
        public void OnClickCloseResultAndAdvanceDay()
        {
            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

            if (currentDay >= finalDayNumber)
            {
                if (isAiIdentified && !hasEscapedAI)
                {
                    if (!string.IsNullOrEmpty(clearSceneName)) SceneManager.LoadScene(clearSceneName);
                }
                else
                {
                    if (!string.IsNullOrEmpty(gameOverSceneName)) SceneManager.LoadScene(gameOverSceneName);
                }
            }
            else
            {
                GameManager.Instance?.AdvanceToNextDay();
                navigationUI.ShowMonitorPanel();
            }
        }

        #endregion

        #region プライベートロジックメソッド

        private void RefreshAllViews()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            profileUI.RefreshProfileDisplay(targetChar);
            RefreshMonitorView();
            UpdateInterrogationState();
        }

        private void RefreshMonitorView()
        {
            var characters = GameManager.Instance?.AllCharacters;
            var statusMap = new Dictionary<string, bool>();

            if (characters != null)
            {
                foreach (var c in characters)
                {
                    statusMap[c.CharacterId] = c.IsReleased;
                }
            }

            monitorUI.UpdateRoomStatus(statusMap);
        }

        private void UpdateInterrogationState()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            bool isReleased = targetChar != null && targetChar.IsReleased;
            bool isInterrogated = InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId);
            bool isLimitReached = InterrogationManager.Instance != null && InterrogationManager.Instance.IsActionLimitReached;
            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            bool isPlaying = dialogueUI != null && dialogueUI.IsDialoguePlaying;

            interrogationUI.UpdateButtonsInteractable(
                isReleased,
                isInterrogated,
                isLimitReached,
                isPlaying,
                currentDay >= finalDayNumber
            );
        }

        /// <summary>
        /// リザルト用文字列構築および表示指示
        /// </summary>
        private void ShowResultFlow()
        {
            navigationUI.ShowResultPanel();

            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            DayScenarioSO todayScenario = GameManager.Instance?.GetTodayScenario();
            var history = InterrogationManager.Instance?.TodayActionHistory;
            int earnedPoints = InterrogationManager.Instance != null ? InterrogationManager.Instance.TodayEarnedPoints : 0;

            string titleText = $"{currentDay}日目 リザルト";
            StringBuilder sb = new StringBuilder();

            if (todayScenario != null)
            {
                sb.AppendLine($"<b>【本日のできごと】</b>\n{todayScenario.daySummary}\n");
            }

            sb.AppendLine("<b>【本日の取り調べログ】</b>");
            if (history != null && history.Count > 0)
            {
                foreach (var record in history)
                {
                    sb.AppendLine($"・{record.characterId} へ 「{record.actionType}」 を実行");
                }
            }
            else
            {
                sb.AppendLine("・本日は誰にも取り調べを行いませんでした。");
            }

            resultUI.DisplayResult(titleText, sb.ToString(), earnedPoints);
        }

        private CharacterInstance GetCharacter(string characterId)
        {
            return GameManager.Instance?.AllCharacters?.Find(c => c.CharacterId == characterId);
        }

        private bool CheckIsAI(CharacterInstance targetChar)
        {
            if (targetChar == null) return false;
            var type = targetChar.GetType();
            var prop = type.GetProperty("IsAI") ?? type.GetProperty("isAI");
            if (prop != null) return (bool)prop.GetValue(targetChar);

            var field = type.GetField("IsAI") ?? type.GetField("isAI");
            if (field != null) return (bool)field.GetValue(targetChar);

            return false;
        }

        #endregion
    }
}