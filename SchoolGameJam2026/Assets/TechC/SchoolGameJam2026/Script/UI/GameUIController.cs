using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.UI
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

        // 状態管理
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
            // 初期状態の反映
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

        #region イベントハンドラ（マネージャーからの通知）

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
            dialogueUI.ResetDialogueState();
            RefreshAllViews();
        }

        private void HandleDayReset()
        {
            dialogueUI.StartDialogueSequence("システム", "（本日の朝の状態に巻き戻しました）");
            RefreshAllViews();
        }

        #endregion

        #region 各種操作フロー（UIボタンからの呼び出し窓口）

        /// <summary>
        /// キャラクター選択時の処理
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            currentSelectedCharacterId = characterId;
            dialogueUI.ResetDialogueState();

            CharacterInstance targetChar = GetCharacter(characterId);
            interrogationUI.UpdateCharacterRoomDisplay(targetChar);
            profileUI.RefreshProfileDisplay(targetChar);
            UpdateInterrogationState();
        }

        /// <summary>
        /// 取調室でアクションボタンを押した時
        /// </summary>
        public void ExecuteInterrogationAction(int actionIndex)
        {
            if (dialogueUI.IsDialoguePlaying) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null) return;

            if (targetChar.IsReleased)
            {
                dialogueUI.StartDialogueSequence("システム", "（このキャラクターはすでに解放されているため、取り調べできません）");
                return;
            }

            if (InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId))
            {
                dialogueUI.StartDialogueSequence("システム", "（このキャラクターへの本日の取り調べは既に終了しています）");
                UpdateInterrogationState();
                return;
            }

            InterrogationActionType actionType = (InterrogationActionType)actionIndex;
            string rawDialogue = InterrogationManager.Instance.ExecuteAction(targetChar, actionType, currentHearingTargetId);
            string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;

            dialogueUI.StartDialogueSequence(speakerName, rawDialogue);
            UpdateInterrogationState();
        }

        /// <summary>
        /// 解放ボタン押下
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
            dialogueUI.StartDialogueSequence(speakerName, rawDialogue);

            monitorUI.RefreshMonitorButtons(GameManager.Instance?.AllCharacters);
            UpdateInterrogationState();
        }

        /// <summary>
        /// AI確定ボタン押下
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
                    dialogueUI.StartDialogueSequence(speakerName, rawDialogue);
                }
            }

            ShowResultFlow();
        }

        /// <summary>
        /// 聞き取りターゲット選択時のイベント
        /// </summary>
        public void OnSelectHearingTarget(string targetId)
        {
            currentHearingTargetId = targetId;
            interrogationUI.CloseHearingPanel();
            ExecuteInterrogationAction((int)InterrogationActionType.HearAbout);
        }

        /// <summary>
        /// 日時を進める/勝敗判定
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

        #region パネル・ビュー更新用プライベートメソッド

        private void RefreshAllViews()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            profileUI.RefreshProfileDisplay(targetChar);
            monitorUI.RefreshMonitorButtons(GameManager.Instance?.AllCharacters);
            UpdateInterrogationState();
        }

        private void UpdateInterrogationState()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            bool isReleased = targetChar != null && targetChar.IsReleased;
            bool isInterrogated = InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId);
            bool isLimitReached = InterrogationManager.Instance != null && InterrogationManager.Instance.IsActionLimitReached;
            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

            interrogationUI.UpdateButtonsInteractable(
                isReleased, 
                isInterrogated, 
                isLimitReached, 
                dialogueUI.IsDialoguePlaying, 
                currentDay >= finalDayNumber
            );
        }

        public void ShowResultFlow()
        {
            navigationUI.ShowResultPanel();
            DayScenarioSO todayScenario = GameManager.Instance?.GetTodayScenario();
            var history = InterrogationManager.Instance?.TodayActionHistory;
            int earnedPoints = InterrogationManager.Instance != null ? InterrogationManager.Instance.TodayEarnedPoints : 0;

            resultUI.BuildAndShowResult(GameManager.Instance?.AllCharacters, todayScenario, history, earnedPoints);
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