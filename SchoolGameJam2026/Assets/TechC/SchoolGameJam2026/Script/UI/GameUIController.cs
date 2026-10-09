using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
        [SerializeField] private StatusHeaderUI statusHeaderUI;

        // 画面上の選択状態（ゲームの状態は各 Manager が持つ）
        private string currentSelectedCharacterId = "A"; // 拷問対象キャラクターID
        private string currentHearingTargetId = "B";

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

            if (GameManagerInstance != null && statusHeaderUI != null)
            {
                statusHeaderUI.UpdateDayDisplay(GameManagerInstance.CurrentDay);
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
            if (statusHeaderUI != null) statusHeaderUI.UpdatePointDisplay(points);
        }

        private void HandleActionCountChanged(int currentActions, int maxLimit)
        {
            if (statusHeaderUI != null) statusHeaderUI.UpdateActionCountDisplay(currentActions, maxLimit);
        }

        private void HandleDayStarted(int day)
        {
            if (statusHeaderUI != null) statusHeaderUI.UpdateDayDisplay(day);
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

            if (!TryValidateAction(targetChar)) return;

            // 解放フラグの更新は InterrogationManager が行う
            string rawDialogue = InterrogationInstance.ExecuteAction(targetChar, InterrogationActionType.Release, currentHearingTargetId);

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
            if (GameManagerInstance == null || !GameManagerInstance.ConfirmAI(targetChar)) return;

            string rawDialogue = InterrogationInstance != null ? InterrogationInstance.GetConfirmAIDialogue(targetChar) : "";
            if (!string.IsNullOrEmpty(rawDialogue) && rawDialogue != "「……」")
            {
                ShowDialogue(GetDisplayName(targetChar), rawDialogue);
            }

            // パネル切替でセリフが見えなくなるため、判定結果はリザルトにも載せる
            string verdict = $"<b>【最終判定】</b>\n{GetDisplayName(targetChar)} をAIと判定しました。\n{DialogueTextFormat.ToPlainText(rawDialogue)}\n";
            ShowResultFlow(verdict);
        }

        /// <summary>
        /// 特定キャラのプロフィール解放（"BASIC" または 個別itemKey に対応）
        /// </summary>
        public void TryUnlockProfileItemForCharacter(string characterId, string itemKey)
        {
            CharacterInstance targetChar = GetCharacter(characterId);
            if (InterrogationInstance == null) return;

            if (InterrogationInstance.TryUnlockProfile(targetChar, itemKey))
            {
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

            // 最終日ならエンディングへの遷移は GameManager が行う
            bool advancedToNextDay = GameManagerInstance.AdvanceDayOrFinishGame();
            if (advancedToNextDay && navigationUI != null)
            {
                // 新しい日はモニターを起点にするため、前日の画面履歴は破棄する
                navigationUI.ShowMonitorPanel();
                navigationUI.ClearHistory();
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
            if (!InterrogationInstance.CanExecuteAction(targetChar, out string rejectReason))
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
                foreach (DialoguePage page in DialogueTextFormat.Parse(text))
                {
                    logUI.AddLog(page.IsSystem ? "状況" : speakerName, page.Body);
                }
            }
        }

        /// <summary> 取調室ボタンから実行してよいアクションか </summary>
        private static bool IsInterrogationAction(InterrogationActionType actionType)
        {
            return actionType == InterrogationActionType.Torture
                || actionType == InterrogationActionType.Talk
                || actionType == InterrogationActionType.Negotiate
                || actionType == InterrogationActionType.HearAbout;
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
