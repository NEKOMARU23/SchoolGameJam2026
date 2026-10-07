using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
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
        [SerializeField] private LogUI logUI;

        [Header("--- 共通ステータス表示（TMP） ---")]
        [SerializeField] private List<TextMeshProUGUI> dayTexts = new List<TextMeshProUGUI>();       // 日数テキスト群
        [SerializeField] private List<TextMeshProUGUI> pointTexts = new List<TextMeshProUGUI>();     // ポイントテキスト群
        [SerializeField] private List<TextMeshProUGUI> actionTexts = new List<TextMeshProUGUI>();    // 行動回数テキスト群
        [Header("--- ゲーム進行設定 ---")]
        [SerializeField] private string clearSceneName = "ClearScene";
        [SerializeField] private string gameOverSceneName = "GameOverScene";

        // 内部状態管理
        private string currentSelectedCharacterId = "A"; // 拷問対象キャラクターID
        private string currentHearingTargetId = "B";
        private bool isAiIdentified = false;

        public string CurrentSelectedCharacterId => currentSelectedCharacterId;

        private static GameManager GameManagerInstance => GameManager.Instance != null ? GameManager.Instance : null;
        private static InterrogationManager InterrogationInstance => InterrogationManager.Instance != null ? InterrogationManager.Instance : null;

        private void Start()
        {
            // 同一オブジェクト上の Manager の Awake 完了後に購読するため、OnEnable ではなく Start で行う
            Subscribe();

            if (InterrogationInstance != null)
            {
                HandlePointsChanged(InterrogationInstance.CurrentPoints);
                HandleActionCountChanged(InterrogationInstance.CurrentDailyActions, InterrogationInstance.DailyActionLimit);
            }

            if (GameManagerInstance != null)
            {
                UpdateDayTexts(GameManagerInstance.CurrentDay);
            }

            RefreshAllViews();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (InterrogationInstance != null)
            {
                InterrogationInstance.OnPointsChanged += HandlePointsChanged;
                InterrogationInstance.OnActionCountChanged += HandleActionCountChanged;
            }

            if (GameManagerInstance != null)
            {
                GameManagerInstance.OnDayStarted += HandleDayStarted;
                GameManagerInstance.OnDayReset += HandleDayReset;
            }

            if (dialogueUI != null)
            {
                dialogueUI.OnDialogueFinished += UpdateInterrogationState;
            }
        }

        private void Unsubscribe()
        {
            if (InterrogationInstance != null)
            {
                InterrogationInstance.OnPointsChanged -= HandlePointsChanged;
                InterrogationInstance.OnActionCountChanged -= HandleActionCountChanged;
            }

            if (GameManagerInstance != null)
            {
                GameManagerInstance.OnDayStarted -= HandleDayStarted;
                GameManagerInstance.OnDayReset -= HandleDayReset;
            }

            if (dialogueUI != null)
            {
                dialogueUI.OnDialogueFinished -= UpdateInterrogationState;
            }
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
            UpdateDayTexts(day);
            RefreshAllViews();
        }

        private void HandleDayReset()
        {
            ShowDialogue("システム", "（本日の朝の状態に巻き戻しました）");
            RefreshAllViews();
        }

        #endregion

        #region UIからの操作受付・ゲームロジックの処理

        /// <summary>
        /// 【モニター用】キャラクターを選択し、取調（拷問）画面を表示します。
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            currentSelectedCharacterId = characterId;
            if (dialogueUI != null) dialogueUI.ResetDialogueState();

            CharacterInstance targetChar = GetCharacter(characterId);

            if (interrogationUI != null) interrogationUI.UpdateCharacterRoomDisplay(targetChar);
            if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);

            UpdateInterrogationState();

            if (navigationUI != null)
            {
                navigationUI.ShowInterrogationPanel();
            }
        }

        /// <summary>
        /// 【プロフィール画面専用】拷問対象を変更せずに、プロフィール表示のみを特定キャラに更新します。
        /// </summary>
        public void ShowProfileOnly(string characterId)
        {
            CharacterInstance targetChar = GetCharacter(characterId);
            if (profileUI != null && targetChar != null)
            {
                profileUI.RefreshProfileDisplay(targetChar);
            }
        }

        /// <summary>
        /// 取調室アクション（拷問・対話・交渉・聞き取り）の実行ロジック
        /// </summary>
        public void ExecuteInterrogationAction(int actionIndex)
        {
            if (dialogueUI != null && dialogueUI.IsDialoguePlaying) return;

            // 解放・AI確定は専用メソッドを経由させる（フラグ更新や判定が必要なため）
            InterrogationActionType actionType = (InterrogationActionType)actionIndex;
            if (!IsInterrogationAction(actionType))
            {
                Debug.LogError($"[GameUIController] 取調室ボタンに不正なアクション番号が設定されています: {actionIndex}");
                return;
            }

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || InterrogationInstance == null) return;

            if (!TryValidateAction(targetChar)) return;

            string rawDialogue = InterrogationInstance.ExecuteAction(targetChar, actionType, currentHearingTargetId);

            ShowDialogue(GetDisplayName(targetChar), rawDialogue);
            UpdateInterrogationState();
        }

        /// <summary>
        /// キャラクター解放ロジック
        /// </summary>
        public void ExecuteReleaseCharacter()
        {
            if (dialogueUI != null && dialogueUI.IsDialoguePlaying) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || InterrogationInstance == null) return;

            // 行動不可なら解放もしない（先にフラグを立てると拒否された場合も解放が確定してしまう）
            if (!TryValidateAction(targetChar)) return;

            string rawDialogue = InterrogationInstance.ExecuteAction(targetChar, InterrogationActionType.Release, currentHearingTargetId);
            targetChar.SetReleaseFlag(true);

            if (targetChar.IsAI)
            {
                Debug.LogWarning($"<color=red>【警告】AIである {currentSelectedCharacterId} が解放されました。</color>");
            }

            if (string.IsNullOrEmpty(rawDialogue) || rawDialogue == "「……」")
            {
                rawDialogue = "解放される……ということですか。わかりました。|[SYS][IMG:OFF]（対象は部屋から退出しました）";
            }

            ShowDialogue(GetDisplayName(targetChar), rawDialogue);

            RefreshMonitorView();
            UpdateInterrogationState();
        }

        /// <summary>
        /// AI確定ボタン押下ロジック（最終判定のため行動回数は消費しない）
        /// </summary>
        public void ExecuteConfirmAI()
        {
            if (dialogueUI != null && dialogueUI.IsDialoguePlaying) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || targetChar.IsReleased) return;

            isAiIdentified = targetChar.IsAI;

            string rawDialogue = InterrogationInstance != null ? InterrogationInstance.GetConfirmAIDialogue(targetChar) : "";
            if (!string.IsNullOrEmpty(rawDialogue) && rawDialogue != "「……」")
            {
                ShowDialogue(GetDisplayName(targetChar), rawDialogue);
            }

            // パネル切替でセリフが見えなくなるため、判定結果はリザルトにも載せる
            string verdict = $"<b>【最終判定】</b>\n{GetDisplayName(targetChar)} をAIと判定しました。\n{StripDialogueTags(rawDialogue)}\n";
            ShowResultFlow(verdict);
        }

        /// <summary>
        /// 特定キャラのプロフィール解放（"BASIC" または 個別itemKey に対応）
        /// </summary>
        public void TryUnlockProfileItemForCharacter(string characterId, string itemKey)
        {
            CharacterInstance targetChar = GetCharacter(characterId);
            if (targetChar == null || targetChar.ProfileData == null) return;
            if (InterrogationInstance == null || string.IsNullOrEmpty(itemKey)) return;

            // --- 1. 基本情報の解放処理 ("BASIC") ---
            if (itemKey.Equals("BASIC", StringComparison.OrdinalIgnoreCase))
            {
                if (targetChar.IsBasicInfoUnlocked) return;

                int cost = targetChar.ProfileData.basicInfo != null ? targetChar.ProfileData.basicInfo.unlockCost : 0;
                if (InterrogationInstance.TrySpendPoints(cost))
                {
                    targetChar.SetBasicInfoUnlocked(true);
                    if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);
                }
                return;
            }

            // --- 2. 個別履歴・詳細項目の解放処理 (その他のキー) ---
            if (targetChar.IsHistoryUnlocked(itemKey)) return;

            var item = targetChar.ProfileData.historyInfoList?.Find(i => i.itemKey == itemKey);
            if (item == null)
            {
                Debug.LogWarning($"[GameUIController] {characterId} のプロフィールに itemKey '{itemKey}' が存在しません。");
                return;
            }

            if (InterrogationInstance.TrySpendPoints(item.unlockCost))
            {
                targetChar.UnlockHistoryItem(itemKey);
                if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);
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
            if (GameManagerInstance != null) GameManagerInstance.ResetCurrentDay();
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
            if (GameManagerInstance == null) return;

            if (GameManagerInstance.IsFinalDay)
            {
                bool isClear = isAiIdentified && !HasAIEscaped();
                string nextScene = isClear ? clearSceneName : gameOverSceneName;
                if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
            }
            else
            {
                GameManagerInstance.AdvanceToNextDay();
                if (navigationUI != null)
                {
                    // 新しい日はモニターを起点にするため、前日の画面履歴は破棄する
                    navigationUI.ShowMonitorPanel();
                    navigationUI.ClearHistory();
                }
            }
        }

        #endregion

        #region プライベートロジックメソッド

        private void UpdateDayTexts(int day)
        {
            foreach (var text in dayTexts)
            {
                if (text != null) text.text = $"{day}日目";
            }
        }

        private void RefreshAllViews()
        {
            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (profileUI != null) profileUI.RefreshProfileDisplay(targetChar);
            RefreshMonitorView();
            UpdateInterrogationState();
        }

        private void RefreshMonitorView()
        {
            var characters = GameManagerInstance != null ? GameManagerInstance.AllCharacters : null;
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
            bool isInterrogated = InterrogationInstance != null && InterrogationInstance.HasInterrogatedToday(currentSelectedCharacterId);
            bool isLimitReached = InterrogationInstance != null && InterrogationInstance.IsActionLimitReached;
            bool isFinalDay = GameManagerInstance != null && GameManagerInstance.IsFinalDay;
            bool isPlaying = dialogueUI != null && dialogueUI.IsDialoguePlaying;

            if (interrogationUI != null)
            {
                interrogationUI.UpdateButtonsInteractable(
                    isReleased,
                    isInterrogated,
                    isLimitReached,
                    isPlaying,
                    isFinalDay
                );
            }
        }

        /// <summary>
        /// 対象キャラに行動可能か確認し、不可ならその理由を会話欄に表示する
        /// </summary>
        private bool TryValidateAction(CharacterInstance targetChar)
        {
            if (targetChar.IsReleased)
            {
                ShowDialogue("システム", "（このキャラクターはすでに解放されているため、取り調べできません）");
                return false;
            }

            if (!InterrogationInstance.CanExecuteAction(targetChar.CharacterId, out string rejectReason))
            {
                ShowDialogue("システム", rejectReason);
                UpdateInterrogationState();
                return false;
            }

            return true;
        }

        /// <summary>
        /// リザルト用文字列構築および表示指示
        /// </summary>
        private void ShowResultFlow(string headerText = null)
        {
            if (navigationUI != null) navigationUI.ShowResultPanel();

            int currentDay = GameManagerInstance != null ? GameManagerInstance.CurrentDay : 1;
            var history = InterrogationInstance != null ? InterrogationInstance.TodayActionHistory : null;
            int earnedPoints = InterrogationInstance != null ? InterrogationInstance.TodayEarnedPoints : 0;

            string titleText = $"{currentDay}日目 リザルト";
            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(headerText))
            {
                sb.AppendLine(headerText);
            }

            sb.AppendLine("<b>【本日の取り調べログ】</b>");
            if (history != null && history.Count > 0)
            {
                foreach (KeyValuePair<string, InterrogationActionType> record in history)
                {
                    sb.AppendLine($"・{GetDisplayName(GetCharacter(record.Key), record.Key)} へ 「{GetActionTypeName(record.Value)}」 を実行");
                }
            }
            else
            {
                sb.AppendLine("・本日は誰にも取り調べを行いませんでした。");
            }

            if (resultUI != null) resultUI.DisplayResult(titleText, sb.ToString(), earnedPoints);
        }

        /// <summary>
        /// 会話を表示し、ログにも記録する
        /// </summary>
        private void ShowDialogue(string speakerName, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (dialogueUI != null) dialogueUI.StartDialogueSequence(speakerName, text);

            if (logUI != null)
            {
                foreach (string page in text.Split('|'))
                {
                    string trimmed = page.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    string logSpeaker = trimmed.StartsWith(DialogueUI.SystemTag) ? "状況" : speakerName;
                    logUI.AddLog(logSpeaker, StripDialogueTags(trimmed));
                }
            }
        }

        /// <summary> [SYS] や [IMG:xxx] などの演出タグとページ区切りを除去する </summary>
        private static string StripDialogueTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return System.Text.RegularExpressions.Regex.Replace(text, @"\[(SYS|IMG:[^\]]*)\]", "").Replace('|', '\n');
        }

        /// <summary> 取調室ボタンから実行してよいアクションか </summary>
        private static bool IsInterrogationAction(InterrogationActionType actionType)
        {
            return actionType == InterrogationActionType.Torture
                || actionType == InterrogationActionType.Talk
                || actionType == InterrogationActionType.Negotiate
                || actionType == InterrogationActionType.HearAbout;
        }

        /// <summary> AIが解放されてしまったか（キャラ状態から導出するため、リセット時も自動で整合する） </summary>
        private bool HasAIEscaped()
        {
            var characters = GameManagerInstance != null ? GameManagerInstance.AllCharacters : null;
            return characters != null && characters.Exists(c => c != null && c.IsAI && c.IsReleased);
        }

        private CharacterInstance GetCharacter(string characterId)
        {
            var characters = GameManagerInstance != null ? GameManagerInstance.AllCharacters : null;
            return characters?.Find(c => c.CharacterId == characterId);
        }

        private static string GetDisplayName(CharacterInstance character, string fallback = "")
        {
            if (character == null) return fallback;
            return character.ProfileData != null ? character.ProfileData.basicInfo.characterName : character.CharacterId;
        }

        private static string GetActionTypeName(InterrogationActionType type)
        {
            switch (type)
            {
                case InterrogationActionType.Torture: return "拷問";
                case InterrogationActionType.Talk: return "対話";
                case InterrogationActionType.Negotiate: return "交渉";
                case InterrogationActionType.HearAbout: return "聞き取り";
                case InterrogationActionType.Release: return "解放";
                case InterrogationActionType.ConfirmAI: return "AI確定";
                default: return "不明";
            }
        }

        #endregion
    }
}
