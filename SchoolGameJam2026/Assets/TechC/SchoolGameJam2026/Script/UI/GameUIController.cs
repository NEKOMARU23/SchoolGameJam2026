using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
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

        [Header("--- 共通ステータス表示（TMP） ---")]
        [SerializeField] private List<TextMeshProUGUI> dayTexts = new List<TextMeshProUGUI>();       // 日数テキスト群
        [SerializeField] private List<TextMeshProUGUI> pointTexts = new List<TextMeshProUGUI>();     // ポイントテキスト群
        [SerializeField] private List<TextMeshProUGUI> actionTexts = new List<TextMeshProUGUI>();    // 行動回数テキスト群
        [Header("--- ゲーム進行設定 ---")]
        [SerializeField] private int finalDayNumber = 7;
        [SerializeField] private string clearSceneName = "ClearScene";
        [SerializeField] private string gameOverSceneName = "GameOverScene";

        // 内部状態管理
        private string currentSelectedCharacterId = "A";
        private string currentHearingTargetId = "B";
        private bool hasEscapedAI = false;
        private bool isAiIdentified = false;

        public string CurrentSelectedCharacterId => currentSelectedCharacterId;

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
            foreach (var text in pointTexts)
            {
                if (text != null) text.text = $"PT: {points}";
            }
        }

        private void HandleActionCountChanged(int currentActions, int maxLimit)
        {
            foreach (var text in actionTexts)
            {
                if (text != null) text.text = $"行動回数: {currentActions} / {maxLimit}";
            }
        }

        private void HandleDayStarted(int day)
        {
            foreach (var text in dayTexts)
            {
                if (text != null) text.text = $"{day}日目";
            }
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
        /// 現在選択中の CharacterInstance を取得
        /// </summary>
        public CharacterInstance GetSelectedCharacter()
        {
            return GetCharacter(currentSelectedCharacterId);
        }

        /// <summary>
        /// 【モニター用】キャラクターを選択し、取調（拷問）画面を表示します。
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            SelectCharacterForProfile(characterId);

            if (navigationUI != null)
            {
                navigationUI.ShowInterrogationPanel();
            }
        }

        /// <summary>
        /// 【プロフィールパネル用】画面遷移を行わずにプロフィール表示情報のみを更新します。
        /// </summary>
        public void SelectCharacterForProfile(string characterId)
        {
            currentSelectedCharacterId = characterId;
            if (dialogueUI != null) dialogueUI.ResetDialogueState();

            CharacterInstance targetChar = GetCharacter(characterId);

            if (interrogationUI != null) interrogationUI.UpdateCharacterRoomDisplay(targetChar);
            if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);

            UpdateInterrogationState();
        }

        /// <summary>
        /// プロフィール画面等の「戻る」ボタン用処理
        /// </summary>
        public void OnClickReturnToInterrogationOrMonitor()
        {
            if (navigationUI != null)
            {
                navigationUI.ShowInterrogationPanel();
            }
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

            if (targetChar.IsBasicInfoUnlocked) return;

            int cost = targetChar.ProfileData.basicInfo != null ? targetChar.ProfileData.basicInfo.unlockCost : 0;

            if (InterrogationManager.Instance != null)
            {
                if (InterrogationManager.Instance.CurrentPoints >= cost)
                {
                    InterrogationManager.Instance.ConsumePoints(cost);
                    targetChar.SetBasicInfoUnlocked(true);
                    if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);
                }
            }
        }

        /// <summary>
        /// 特定プロフィールの解放を試みる（ロジック）
        /// </summary>
        public void TryUnlockProfileItem(string itemKey)
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            if (targetChar.IsHistoryUnlocked(itemKey)) return;

            var item = targetChar.ProfileData.historyInfoList?.Find(i => i.itemKey == itemKey);
            if (item == null) return;

            if (InterrogationManager.Instance != null)
            {
                if (InterrogationManager.Instance.CurrentPoints >= item.unlockCost)
                {
                    InterrogationManager.Instance.ConsumePoints(item.unlockCost);
                    targetChar.UnlockHistoryItem(itemKey);
                    if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);
                }
            }
        }

        /// <summary>
        /// 聞き取りターゲット選択の受付
        /// </summary>
        public void OnSelectHearingTarget(string targetId)
        {
            currentHearingTargetId = targetId;
            if (interrogationUI != null) interrogationUI.CloseHearingPanel();
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
                if (navigationUI != null) navigationUI.ShowMonitorPanel();
            }
        }

        #endregion

        #region プライベートロジックメソッド

        private void RefreshAllViews()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);
            RefreshMonitorView();
            UpdateInterrogationState();
        }

        private void RefreshMonitorView()
        {
            var characters = GameManager.Instance?.AllCharacters;
            Dictionary<string, bool> statusMap = new Dictionary<string, bool>();

            if (characters != null)
            {
                foreach (CharacterInstance c in characters)
                {
                    statusMap[c.CharacterId] = c.IsReleased;
                }
            }

            if (monitorUI != null) monitorUI.UpdateRoomStatus(statusMap);
        }

        private void UpdateInterrogationState()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            bool isReleased = targetChar != null && targetChar.IsReleased;
            bool isInterrogated = InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId);
            bool isLimitReached = InterrogationManager.Instance != null && InterrogationManager.Instance.IsActionLimitReached;
            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            bool isPlaying = dialogueUI != null && dialogueUI.IsDialoguePlaying;

            if (interrogationUI != null)
            {
                interrogationUI.UpdateButtonsInteractable(
                    isReleased,
                    isInterrogated,
                    isLimitReached,
                    isPlaying,
                    currentDay >= finalDayNumber
                );
            }
        }

        /// <summary>
        /// リザルト用文字列構築および表示指示
        /// </summary>
        private void ShowResultFlow()
        {
            if (navigationUI != null) navigationUI.ShowResultPanel();

            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            DayScenarioSO todayScenario = GameManager.Instance?.GetTodayScenario();
            var history = InterrogationManager.Instance?.TodayActionHistory;
            int earnedPoints = InterrogationManager.Instance != null ? InterrogationManager.Instance.TodayEarnedPoints : 0;

            string titleText = $"{currentDay}日目 リザルト";
            StringBuilder sb = new StringBuilder();

            if (todayScenario != null)
            {
                string scenarioText = GetScenarioDescription(todayScenario);
                if (!string.IsNullOrEmpty(scenarioText))
                {
                    sb.AppendLine($"<b>【本日のできごと】</b>\n{scenarioText}\n");
                }
            }

            sb.AppendLine("<b>【本日の取り調べログ】</b>");
            if (history != null && history.Count > 0)
            {
                foreach (KeyValuePair<string, InterrogationActionType> record in history)
                {
                    sb.AppendLine($"・{record.Key} へ 「{record.Value}」 を実行");
                }
            }
            else
            {
                sb.AppendLine("・本日は誰にも取り調べを行いませんでした。");
            }

            if (resultUI != null) resultUI.DisplayResult(titleText, sb.ToString(), earnedPoints);
        }

        private CharacterInstance GetCharacter(string characterId)
        {
            return GameManager.Instance?.AllCharacters?.Find(c => c.CharacterId == characterId);
        }

        private bool CheckIsAI(CharacterInstance targetChar)
        {
            if (targetChar == null) return false;
            Type type = targetChar.GetType();
            System.Reflection.PropertyInfo prop = type.GetProperty("IsAI") ?? type.GetProperty("isAI");
            if (prop != null) return (bool)prop.GetValue(targetChar);

            System.Reflection.FieldInfo field = type.GetField("IsAI") ?? type.GetField("isAI");
            if (field != null) return (bool)field.GetValue(targetChar);

            return false;
        }

        private string GetScenarioDescription(DayScenarioSO scenario)
        {
            if (scenario == null) return "";
            Type type = scenario.GetType();

            string[] possibleNames = { "daySummary", "description", "summary", "scenarioText", "dayDescription" };

            foreach (string name in possibleNames)
            {
                System.Reflection.FieldInfo field = type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) return field.GetValue(scenario)?.ToString() ?? "";

                System.Reflection.PropertyInfo prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null) return prop.GetValue(scenario)?.ToString() ?? "";
            }

            return "";
        }

        #endregion
    }
}