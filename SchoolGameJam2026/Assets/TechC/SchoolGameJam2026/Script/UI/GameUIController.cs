using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.UI.Dialogue;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.Core.State;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    public class GameUIController : MonoBehaviour, ISaveTarget
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
        [SerializeField] private DayTransitionUI dayTransitionUI;
        [SerializeField] private DialogueClickable smartphoneTalk;   // スマホ（補助ちゃんとの会話）
        [SerializeField] private ConfirmDialogUI confirmDialog;      // AI確定・解放・プロフィール解禁の確認
        [SerializeField] private ChoiceUI choiceUI;                  // 会話の後に出す選択肢
        [SerializeField] private EndingPlayerUI endingPlayer;        // 最終日の後のエンディング
        [SerializeField] private AutoSkipButtonsUI[] autoSkipButtons; // 会話枠の「オート」「スキップ」（取調室・スマホ）
        [SerializeField] private PointGainUI pointGainUI;            // 行動で入ったポイント（「+20pt」）
        [SerializeField] private NextPageMarkUI interrogationNextMark; // 取調室の会話の「▼」（クリックで次へ）
        [SerializeField] private NextPageMarkUI smartphoneNextMark;    // スマホの会話の「▼」

        [Header("--- 朝の会話（その日の始めに自動で流れるスマホの会話） ---")]
        [Tooltip("スマホの呼び出し音（Data/SE のファイル名。空なら鳴らさない）")]
        [SerializeField] private string morningRingSeName = "";
        [Tooltip("呼び出し音を鳴らしてから会話画面を開くまでの秒数（音がないときは待たない）")]
        [SerializeField, Min(0f)] private float morningRingSeconds = 1.5f;

        private const string SmartphoneNoTalkMessage = "今日はもう連絡ができない";
        private const string UnjudgedMessage = "まだ判定していない人がいます";

        // 画面上の選択状態（ゲームの状態は各 Manager が持つ）
        private string currentSelectedCharacterId = "A"; // 拷問対象キャラクターID
        private string currentProfileCharacterId = "A";  // プロフィール画面で表示中のキャラクターID
        private string currentHearingTargetId = "B";
        private bool isDayTransitioning = false;             // 日付切り替え演出中（連打で複数日進むのを防ぐ）
        private bool isAutoOn;                               // オート（取調室とスマホで共通）
        private bool isSkipOn;                               // スキップ（取調室とスマホで共通）
        private bool isMorningTalk;                          // 表示中のスマホ会話が朝の会話か（終わったら日が変わる演出でモニターへ戻る）
        private List<DialoguePage> currentSmartphonePages;   // 表示中のスマホ会話（ページごとの SE 用）
        private ScenarioDialogue currentSmartphoneDialogue;  // 表示中のスマホ会話（終わったら選択肢を調べる）
        private ScenarioDialogue interrogationDialogue;      // 取調室で表示中の Excel の会話（終わったら選択肢を調べる）
        private CharacterInstance interrogationPartner;      // その会話の相手
        private int pendingEarnedPoints;                     // 行動で入ったポイント（会話が全部終わったら出す）
        private bool wasInterrogatedOnEnter;                 // 部屋に入ったとき、もう取り調べ済みだったか（「今日はもう取り調べました」を出すか）
        private readonly List<LogEntry> logHistory = new List<LogEntry>();   // ログの内容（セーブに入れる）

        public string CurrentSelectedCharacterId => currentSelectedCharacterId;

        private static GameManager GameManagerInstance => GameManager.Instance != null ? GameManager.Instance : null;
        private static InterrogationManager InterrogationInstance => InterrogationManager.Instance != null ? InterrogationManager.Instance : null;
        private static SmartphoneTalkManager SmartphoneInstance => SmartphoneTalkManager.Instance != null ? SmartphoneTalkManager.Instance : null;

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

            // はじめからのときは、1日目の朝の会話を流す（GameManager の Start が先に終わっていた場合）
            StartMorningTalkAsync(this.GetCancellationTokenOnDestroy()).Forget();

            // セーブ・ロード（Manager シーンのオプション画面）から操作できるようにする
            SaveLoadController.Target = this;

            // タイトルの「つづきから」で始めたときは、セーブのログも戻す（GameManager の Start が先に終わっていた場合）
            if (GameManagerInstance != null && GameManagerInstance.SaveLoadedAtStart != null) RestoreLog(GameManagerInstance.SaveLoadedAtStart.log);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(SaveLoadController.Target, this)) SaveLoadController.Target = null;
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
                GameManagerInstance.OnStateRestored += HandleStateRestored;
                GameManagerInstance.OnSaveLoaded += HandleSaveLoaded;
            }

            if (dialogueUI != null)
            {
                dialogueUI.OnDialogueFinished += HandleDialogueFinished;
                dialogueUI.OnSkipStopped += StopSkip;
            }

            if (smartphoneTalk != null)
            {
                smartphoneTalk.OnClosed += HandleSmartphoneTalkClosed;
                smartphoneTalk.OnPageShown += HandleSmartphonePageShown;
                smartphoneTalk.OnSkipStopped += StopSkip;
            }
            RefreshAutoSkipButtons();

            // 「▼」はクリックで次へ進めるときだけ出す
            // 取調室: 最後のページは表示し終えると会話が終わるので、次のページが残っているときだけ
            // スマホ: 最後のページもクリックで閉じるので、会話画面が開いている間ずっと
            if (interrogationNextMark != null) interrogationNextMark.SetCondition(() => dialogueUI != null && dialogueUI.IsDialoguePlaying);
            if (smartphoneNextMark != null) smartphoneNextMark.SetCondition(() => smartphoneTalk != null && smartphoneTalk.IsOpen);

            // 立ち絵と SE は、Excel の名前で Data フォルダの素材を探す
            if (dialogueUI != null) dialogueUI.SetAssetResolvers(FindCharacterSprite, PlaySeByName);

            // オプションの設定（文字の速さ）は Manager シーンの SettingsManager から受け取る
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged += HandleSettingsChanged;
                HandleSettingsChanged(SettingsManager.Instance.Current);
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
                GameManagerInstance.OnStateRestored -= HandleStateRestored;
                GameManagerInstance.OnSaveLoaded -= HandleSaveLoaded;
            }

            if (dialogueUI != null)
            {
                dialogueUI.OnDialogueFinished -= HandleDialogueFinished;
                dialogueUI.OnSkipStopped -= StopSkip;
            }

            if (smartphoneTalk != null)
            {
                smartphoneTalk.OnClosed -= HandleSmartphoneTalkClosed;
                smartphoneTalk.OnPageShown -= HandleSmartphonePageShown;
                smartphoneTalk.OnSkipStopped -= StopSkip;
            }

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged -= HandleSettingsChanged;
            }
        }

        private void HandleSettingsChanged(GameSettings settings)
        {
            if (dialogueUI != null)
            {
                dialogueUI.SetTextSpeed(settings.TextIntervalSeconds);
                dialogueUI.SetAutoWait(settings.AutoWaitSeconds);
            }
            if (smartphoneTalk != null)
            {
                smartphoneTalk.SetCharacterDisplayInterval(settings.TextIntervalSeconds);
                smartphoneTalk.SetAutoWait(settings.AutoWaitSeconds);
            }
        }

        #region オート・スキップ

        /// <summary> 「オート」ボタン: 取調室とスマホの会話で共通のオン・オフ </summary>
        public void ToggleAuto()
        {
            isAutoOn = !isAutoOn;
            if (dialogueUI != null) dialogueUI.SetAuto(isAutoOn);
            if (smartphoneTalk != null) smartphoneTalk.SetAuto(isAutoOn);
            RefreshAutoSkipButtons();
        }

        /// <summary> 「スキップ」ボタン: 取調室とスマホの会話で共通のオン・オフ </summary>
        public void ToggleSkip()
        {
            isSkipOn = !isSkipOn;
            if (dialogueUI != null) dialogueUI.SetSkip(isSkipOn);
            if (smartphoneTalk != null) smartphoneTalk.SetSkip(isSkipOn);
            RefreshAutoSkipButtons();
        }

        public bool IsAutoOn => isAutoOn;
        public bool IsSkipOn => isSkipOn;

        /// <summary> スキップを止める（未読の会話に来たとき・選択肢が出たとき） </summary>
        private void StopSkip()
        {
            if (!isSkipOn) return;
            ToggleSkip();
        }

        private void RefreshAutoSkipButtons()
        {
            if (autoSkipButtons == null) return;
            foreach (AutoSkipButtonsUI buttons in autoSkipButtons)
            {
                if (buttons != null) buttons.SetState(isAutoOn, isSkipOn);
            }
        }

        /// <summary>
        /// スキップしてよい会話か。オプションのスキップの種類が「すべて」なら全部、「既読のみ」なら一度最後まで読んだ会話だけ
        /// （Excel の会話でない短い文は、いつでも飛ばしてよい）
        /// </summary>
        private static bool IsSkippable(ScenarioDialogue dialogue)
        {
            if (dialogue == null || string.IsNullOrEmpty(dialogue.id)) return true;
            bool skipAll = SettingsManager.Instance != null && SettingsManager.Instance.Current.skipMode == SkipMode.All;
            return skipAll || ReadHistoryStore.IsRead(dialogue.id);
        }

        #endregion

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
            CloseChoices();
            if (statusHeaderUI != null) statusHeaderUI.UpdateDayDisplay(day);
            RefreshAllViews();

            // 日付の切り替え演出の中では、演出の側で流す（はじめから・デバッグの日付移動のときはここで流す）
            if (!isDayTransitioning) StartMorningTalkAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 状態がまとめて戻ったとき（リセット・ロード・デバッグ）に、画面をすべて今の状態で描き直す
        /// </summary>
        private void HandleStateRestored()
        {
            CloseChoices();
            if (statusHeaderUI != null && GameManagerInstance != null) statusHeaderUI.UpdateDayDisplay(GameManagerInstance.CurrentDay);
            RefreshAllViews();
        }

        /// <summary>
        /// セーブを読み込んだとき: 会話を閉じてモニター画面から再開し、ログを戻す
        /// </summary>
        private void HandleSaveLoaded(SaveData save)
        {
            if (dialogueUI != null) dialogueUI.ResetDialogueState();
            interrogationDialogue = null;
            isMorningTalk = false;
            if (smartphoneTalk != null && smartphoneTalk.IsOpen)
            {
                currentSmartphoneDialogue = null;
                smartphoneTalk.CloseDialogue();
            }
            if (navigationUI != null)
            {
                navigationUI.CloseAllModals();
                navigationUI.ShowMonitorPanel();
                navigationUI.ClearHistory();
            }

            RestoreLog(save.log);
            RefreshAllViews();
        }

        private void HandleDayReset()
        {
            ShowDialogue("システム", "（本日の朝の状態に巻き戻しました）");
            RefreshAllViews();
        }

        #endregion

        #region セーブ・ロード（ISaveTarget）

        /// <summary>
        /// 今セーブしてよいか。会話の途中・選択肢を選んでいる途中・日付の切り替え中などはだめ
        /// </summary>
        public bool CanSave(out string reason)
        {
            bool busy = isDayTransitioning
                || (dialogueUI != null && dialogueUI.IsDialoguePlaying)
                || (choiceUI != null && choiceUI.IsOpen)
                || (smartphoneTalk != null && smartphoneTalk.IsOpen)
                || (confirmDialog != null && confirmDialog.IsOpen)
                || (endingPlayer != null && endingPlayer.IsPlaying);

            reason = busy ? "会話の途中はセーブできません。会話が終わってからセーブしてください" : null;
            return !busy && GameManagerInstance != null;
        }

        public SaveData CreateSaveData()
        {
            return GameManagerInstance != null ? GameManagerInstance.CreateSaveData(logHistory) : null;
        }

        public void ApplySave(SaveData save)
        {
            if (GameManagerInstance != null) GameManagerInstance.ApplySave(save);
        }

        /// <summary> ログを1件足す（画面とセーブ用の記録の両方） </summary>
        private void AddLog(string speaker, string text)
        {
            logHistory.Add(new LogEntry { speaker = speaker, text = text });
            if (logUI != null) logUI.AddLog(speaker, text);
        }

        private void RestoreLog(List<LogEntry> entries)
        {
            logHistory.Clear();
            if (logUI != null) logUI.ClearLog();
            if (entries == null) return;
            foreach (LogEntry entry in entries) AddLog(entry.speaker, entry.text);
        }

        #endregion

        #region UIからの操作受付・ゲームロジックの処理

        /// <summary>
        /// 【モニター用】キャラクターを選択し、取調（拷問）画面を表示します。
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            currentSelectedCharacterId = characterId;
            interrogationDialogue = null;   // 別の部屋に移ったら、前の会話の選択肢は出さない
            pendingEarnedPoints = 0;
            if (dialogueUI != null) dialogueUI.ResetDialogueState();

            CharacterInstance targetChar = GetCharacter(characterId);
            wasInterrogatedOnEnter = InterrogationInstance != null && targetChar != null && InterrogationInstance.HasInterrogatedToday(characterId);

            if (interrogationUI != null && targetChar != null && targetChar.ProfileData != null)
            {
                interrogationUI.UpdateCharacterRoomDisplay(GetDisplayName(targetChar), targetChar.ProfileData.characterIcon);
            }
            RefreshProfileView(characterId);

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
            RefreshProfileView(characterId);
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

            if (!TryValidateAction(targetChar, actionType)) return;

            ActionOutcome outcome = InterrogationInstance.ExecuteAction(targetChar, actionType, currentHearingTargetId);

            if (pointGainUI != null) pointGainUI.Hide();
            pendingEarnedPoints = outcome != null ? outcome.EarnedPoints : 0;
            ShowOutcome(targetChar, outcome);
            UpdateInterrogationState();
        }

        /// <summary>
        /// 解放ボタン（最終日のみ）。確認してから解放する
        /// </summary>
        public void ExecuteReleaseCharacter()
        {
            if (dialogueUI != null && dialogueUI.IsDialoguePlaying) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || InterrogationInstance == null) return;

            if (!TryValidateAction(targetChar, InterrogationActionType.Release)) return;

            Confirm($"{GetDisplayName(targetChar)}を解放しますか？", () => ExecuteJudgement(targetChar, InterrogationActionType.Release));
        }

        /// <summary>
        /// AI確定ボタン（最終日のみ）。確認してから AI と判定する
        /// </summary>
        public void ExecuteConfirmAI()
        {
            if (dialogueUI != null && dialogueUI.IsDialoguePlaying) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            if (targetChar == null || InterrogationInstance == null) return;

            if (!TryValidateAction(targetChar, InterrogationActionType.ConfirmAI)) return;

            Confirm($"{GetDisplayName(targetChar)}を AI と判定しますか？", () => ExecuteJudgement(targetChar, InterrogationActionType.ConfirmAI));
        }

        /// <summary>
        /// 解放・AI確定を実行して会話を表示する（判定のフラグ更新は InterrogationManager が行う）
        /// </summary>
        private void ExecuteJudgement(CharacterInstance targetChar, InterrogationActionType actionType)
        {
            if (!TryValidateAction(targetChar, actionType)) return;

            ActionOutcome outcome = InterrogationInstance.ExecuteAction(targetChar, actionType, currentHearingTargetId);

            if (outcome.HasDialogue)
            {
                ShowOutcome(targetChar, outcome);
            }
            else if (actionType == InterrogationActionType.Release)
            {
                // Excel に解放の会話がないときの決まり文句
                ShowDialogue(GetDisplayName(targetChar), "解放される……ということですか。わかりました。|[SYS][IMG:OFF]（対象は部屋から退出しました）");
            }
            else
            {
                ShowDialogue(GetDisplayName(targetChar), $"[SYS]（{GetDisplayName(targetChar)} を AI と判定しました）");
            }

            RefreshMonitorView();
            UpdateInterrogationState();
        }

        /// <summary>
        /// 特定キャラのプロフィール解放（"BASIC" または 個別itemKey に対応）。確認してから解禁する
        /// </summary>
        public void TryUnlockProfileItemForCharacter(string characterId, string itemKey)
        {
            CharacterInstance targetChar = GetCharacter(characterId);
            if (InterrogationInstance == null) return;

            // 解禁済み・ポイント不足のときは確認を出さない（解禁もできない）
            if (!InterrogationInstance.CanUnlockProfile(targetChar, itemKey, out int cost, out string itemName)) return;

            Confirm($"{cost}pt を使って『{itemName}』を解禁しますか？", () =>
            {
                if (InterrogationInstance.TryUnlockProfile(targetChar, itemKey))
                {
                    RefreshProfileView(characterId);
                }
            });
        }

        /// <summary>
        /// プロフィール画面で表示中のキャラのプロフィール解放（解放ボタン用）
        /// </summary>
        public void TryUnlockProfileItemForViewingCharacter(string itemKey)
        {
            TryUnlockProfileItemForCharacter(currentProfileCharacterId, itemKey);
        }

        /// <summary>
        /// スマホ（補助ちゃん）との会話を開く。今日の回数が残っていなければ何もしない
        /// </summary>
        public void OpenSmartphoneTalk()
        {
            if (smartphoneTalk == null || SmartphoneInstance == null || GameManagerInstance == null) return;

            if (!SmartphoneInstance.TryStartTalk(out ScenarioDialogue dialogue))
            {
                // 今日の回数を使い切っている場合は会話画面を開かずにお知らせだけ出す
                if (monitorUI != null) monitorUI.ShowNotice(SmartphoneNoTalkMessage);
                return;
            }

            if (navigationUI != null) navigationUI.ShowSmartphoneTalkPanel();
            OpenSmartphoneDialogue(dialogue);
        }

        /// <summary>
        /// 朝の会話があれば、呼び出し音を鳴らしてからスマホの会話画面を自動で開く
        /// （スマホで話せる回数は使わない。ロード・今日をやり直すでは GameManager が会話を用意しないので流れない）
        /// </summary>
        /// <returns>朝の会話を始めた場合 true</returns>
        private async UniTask<bool> StartMorningTalkAsync(CancellationToken token)
        {
            if (GameManagerInstance == null || smartphoneTalk == null || isMorningTalk) return false;

            ScenarioDialogue talk = GameManagerInstance.TakeMorningTalk();
            if (talk == null) return false;

            isMorningTalk = true;
            if (!string.IsNullOrEmpty(morningRingSeName))
            {
                PlaySeByName(morningRingSeName);
                await UniTask.Delay(TimeSpan.FromSeconds(morningRingSeconds), ignoreTimeScale: true, cancellationToken: token);
            }

            if (navigationUI != null) navigationUI.ShowSmartphoneTalkPanel();
            OpenSmartphoneDialogue(talk);
            return true;
        }

        /// <summary>
        /// スマホの会話（選択肢の後の会話も含む）が全部終わったときの処理
        /// 朝の会話なら日が変わるときと同じ演出でモニター画面へ、それ以外は元の画面に戻る
        /// </summary>
        private void FinishSmartphoneTalk()
        {
            if (isMorningTalk)
            {
                isMorningTalk = false;
                ReturnFromMorningTalkAsync(this.GetCancellationTokenOnDestroy()).Forget();
                return;
            }
            if (navigationUI != null) navigationUI.BackToPreviousPanel();
        }

        private async UniTaskVoid ReturnFromMorningTalkAsync(CancellationToken token)
        {
            // 日付の切り替え演出がまだ終わっていなければ（スキップですぐ閉じたときなど）、終わるのを待つ
            await UniTask.WaitWhile(() => isDayTransitioning, cancellationToken: token);
            if (GameManagerInstance == null) return;

            isDayTransitioning = true;
            try
            {
                if (dayTransitionUI != null) await dayTransitionUI.CoverAsync($"{GameManagerInstance.CurrentDay}日目", token);

                if (navigationUI != null)
                {
                    navigationUI.ShowMonitorPanel();
                    navigationUI.ClearHistory();
                }

                if (dayTransitionUI != null) await dayTransitionUI.RevealAsync(token);
            }
            finally
            {
                isDayTransitioning = false;
            }
        }

        /// <summary>
        /// スマホの会話画面に Excel の会話を表示する（選択肢を選んだ後の会話もここで出す）
        /// </summary>
        private void OpenSmartphoneDialogue(ScenarioDialogue dialogue)
        {
            currentSmartphoneDialogue = dialogue;

            // Excel の会話のページと、最初に指定された立ち絵で開く（ページごとの SE は OnPageShown で鳴らす）
            currentSmartphonePages = DialogueTextFormat.FromScenario(dialogue);
            var texts = new List<string>();
            Sprite image = null;
            foreach (DialoguePage page in currentSmartphonePages)
            {
                texts.Add(page.Body);
                if (image == null && page.ImageKey != null && !page.HidesImage) image = FindCharacterSprite(page.ImageKey);
            }
            smartphoneTalk.OpenDialogueWithPages(texts, image, IsSkippable(dialogue));

            if (logUI != null)
            {
                foreach (DialoguePage page in currentSmartphonePages) AddLog(GetSpeaker(page, "補助ちゃん"), page.Body);
            }
        }

        private void HandleSmartphonePageShown(int pageIndex)
        {
            if (currentSmartphonePages == null || pageIndex < 0 || pageIndex >= currentSmartphonePages.Count) return;
            string se = currentSmartphonePages[pageIndex].Se;
            if (!string.IsNullOrEmpty(se)) PlaySeByName(se);
        }

        /// <summary>
        /// スマホの会話が終わったら、選択肢があれば出し、なければ元の画面（モニター）に戻る
        /// </summary>
        private void HandleSmartphoneTalkClosed()
        {
            ScenarioDialogue finished = currentSmartphoneDialogue;
            currentSmartphoneDialogue = null;
            if (finished != null) ReadHistoryStore.MarkRead(finished.id);
            if (TryShowChoices(finished, null, isSmartphone: true)) return;

            FinishSmartphoneTalk();
        }

        /// <summary>
        /// 取調室の会話が終わったら、選択肢があれば出す
        /// </summary>
        private void HandleDialogueFinished()
        {
            ScenarioDialogue finished = interrogationDialogue;
            CharacterInstance partner = interrogationPartner;
            interrogationDialogue = null;
            interrogationPartner = null;

            if (finished != null) ReadHistoryStore.MarkRead(finished.id);
            if (!TryShowChoices(finished, partner, isSmartphone: false)) ShowEarnedPoints();
            UpdateInterrogationState();
        }

        /// <summary>
        /// 会話の後の選択肢のうち、条件を満たすものだけを出す（1つもなければ何もしない）
        /// </summary>
        /// <returns>選択肢を出した場合 true</returns>
        private bool TryShowChoices(ScenarioDialogue dialogue, CharacterInstance partner, bool isSmartphone)
        {
            if (dialogue == null || choiceUI == null || GameManagerInstance == null) return false;

            List<ScenarioChoice> choices = GameManagerInstance.GetAvailableChoices(dialogue, partner);
            if (choices.Count == 0) return false;

            StopSkip();   // 選択肢はプレイヤーが選ぶので、スキップはここで止める
            choiceUI.Show(choices.ConvertAll(c => c.text), index => HandleChoiceSelected(choices[index], partner, isSmartphone));
            return true;
        }

        /// <summary>
        /// 選択肢を選んだときの処理。効果は GameManager が適用し、次の会話があれば同じ画面で続ける
        /// （行動回数・スマホの回数は増えない）
        /// </summary>
        private void HandleChoiceSelected(ScenarioChoice choice, CharacterInstance partner, bool isSmartphone)
        {
            AddLog("あなた", choice.text);
            ScenarioDialogue next = GameManagerInstance.SelectChoice(choice, partner);

            if (isSmartphone)
            {
                if (next != null) OpenSmartphoneDialogue(next);
                else FinishSmartphoneTalk();
                return;
            }

            if (next != null) ShowOutcome(partner, ActionOutcome.FromDialogue(next));
            else ShowEarnedPoints();   // 選択肢で会話が終わったら、ここで行動のポイントを出す
            RefreshProfileView(currentProfileCharacterId);
            UpdateInterrogationState();
        }

        /// <summary>
        /// 行動の会話（選択肢の後の会話も含む）が全部終わったら、入ったポイントを「+◯pt」と短く出す（0なら出さない）
        /// </summary>
        private void ShowEarnedPoints()
        {
            int points = pendingEarnedPoints;
            pendingEarnedPoints = 0;
            if (pointGainUI != null) pointGainUI.Show(points);
        }

        private void CloseChoices()
        {
            pendingEarnedPoints = 0;
            interrogationDialogue = null;
            currentSmartphoneDialogue = null;
            if (choiceUI != null && choiceUI.IsOpen) choiceUI.Hide();
        }

        /// <summary>
        /// 聞き取りパネルを開く（話題にできるキャラだけを選択肢に出す）
        /// </summary>
        public void OpenHearingPanel()
        {
            if (interrogationUI == null || InterrogationInstance == null) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            interrogationUI.ShowHearingPanel(InterrogationInstance.GetHearingTargetIds(targetChar));
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
        /// 「1日を終える」ボタン。最終日に判定していない人が残っていればお知らせを出し、それ以外は確認を出す
        /// </summary>
        public void OnClickEndDay()
        {
            if (!CanEndDay()) return;
            if (navigationUI != null) navigationUI.ShowDayEndConfirmModal(true);
        }

        /// <summary>
        /// 1日終了確認の承認時の処理
        /// </summary>
        public void OnConfirmEndDay()
        {
            if (!CanEndDay()) return;
            ShowResultFlow();
        }

        /// <summary>
        /// 1日を終えられるか（最終日は全員を判定するまで終えられない）。終えられないときはお知らせを出す
        /// </summary>
        private bool CanEndDay()
        {
            if (GameManagerInstance == null) return false;
            if (!GameManagerInstance.IsFinalDay || GameManagerInstance.AreAllJudged) return true;

            if (monitorUI != null) monitorUI.ShowNotice(UnjudgedMessage);
            return false;
        }

        /// <summary>
        /// 日時進行および勝敗遷移判定
        /// </summary>
        public void OnClickCloseResultAndAdvanceDay()
        {
            AdvanceDayWithTransitionAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// ロード画面で覆う → 日付を進めて画面を整える → 覆いを外す、の順で翌日へ進める
        /// </summary>
        private async UniTaskVoid AdvanceDayWithTransitionAsync(CancellationToken token)
        {
            if (isDayTransitioning || GameManagerInstance == null) return;
            isDayTransitioning = true;

            try
            {
                string label = GameManagerInstance.IsFinalDay ? "最終結果" : $"{GameManagerInstance.CurrentDay + 1}日目";
                if (dayTransitionUI != null) await dayTransitionUI.CoverAsync(label, token);

                if (GameManagerInstance.IsFinalDay)
                {
                    await PlayEndingAsync(token);
                    return;
                }

                GameManagerInstance.AdvanceToNextDay();

                if (navigationUI != null)
                {
                    // 新しい日はモニターを起点にするため、前日の画面履歴は破棄する
                    navigationUI.ShowMonitorPanel();
                    navigationUI.ClearHistory();
                }

                // 朝の会話があれば、覆いを外す前に呼び出し音を鳴らしてスマホの会話画面を開く
                await StartMorningTalkAsync(token);

                if (dayTransitionUI != null) await dayTransitionUI.RevealAsync(token);
            }
            finally
            {
                isDayTransitioning = false;
            }
        }

        /// <summary>
        /// 最終日の後: エンディングシートで決まったエンディングの会話を見せてから、クリア / ゲームオーバー画面に移る
        /// （ロード画面で覆われた状態で呼ぶ。会話がなければそのまま画面を移る）
        /// </summary>
        private async UniTask PlayEndingAsync(CancellationToken token)
        {
            EndingDecision decision = GameManagerInstance.DecideEnding();

            if (decision.Dialogue != null && endingPlayer != null)
            {
                var finished = new UniTaskCompletionSource();
                endingPlayer.Play(decision.Dialogue, GameManagerInstance.Scenario, () => finished.TrySetResult());
                if (dayTransitionUI != null) await dayTransitionUI.RevealAsync(token);
                await finished.Task.AttachExternalCancellation(token);
            }

            GameManagerInstance.FinishGame(decision);
        }

        #endregion

        #region プライベートロジックメソッド

        private void RefreshAllViews()
        {
            RefreshProfileView(currentSelectedCharacterId);
            RefreshMonitorView();
            UpdateInterrogationState();
        }

        /// <summary>
        /// プロフィール画面を指定キャラの内容で更新する
        /// </summary>
        private void RefreshProfileView(string characterId)
        {
            CharacterInstance targetChar = GetCharacter(characterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            currentProfileCharacterId = characterId;
            if (profileUI != null) profileUI.RefreshProfileDisplay(BuildProfileViewData(targetChar));
        }

        /// <summary>
        /// キャラの解禁状態から、プロフィール画面の表示データを組み立てる
        /// </summary>
        private static ProfileViewData BuildProfileViewData(CharacterInstance targetChar)
        {
            ProfileDataSO profile = targetChar.ProfileData;
            var items = new Dictionary<string, ProfileItemViewData>(StringComparer.OrdinalIgnoreCase);

            BasicProfileGroup basic = profile.basicInfo;
            string basicText = $"年齢：{basic.age}\n" +
                               $"職業：{basic.occupation}\n" +
                               $"誕生日：{basic.birthday}\n" +
                               $"身長：{basic.height}\n" +
                               $"体重：{basic.weight}\n" +
                               $"特技：{basic.skills}\n" +
                               $"好きなこと：{basic.likes}";
            items[ProfileDataSO.BasicInfoKey] = new ProfileItemViewData(targetChar.IsBasicInfoUnlocked, basicText);

            if (profile.historyInfoList != null)
            {
                foreach (ProfileItem item in profile.historyInfoList)
                {
                    if (item == null || string.IsNullOrEmpty(item.itemKey)) continue;
                    items[item.itemKey] = new ProfileItemViewData(targetChar.IsHistoryUnlocked(item.itemKey), item.valueContent);
                }
            }

            return new ProfileViewData(GetDisplayName(targetChar), profile.characterIcon, items);
        }

        private void RefreshMonitorView()
        {
            var characters = GameManagerInstance != null ? GameManagerInstance.AllCharacters : null;
            Dictionary<string, bool> statusMap = new Dictionary<string, bool>();
            Dictionary<string, Sprite> faceMap = new Dictionary<string, Sprite>();
            Dictionary<string, MonitorJudgementLabel> judgementMap = new Dictionary<string, MonitorJudgementLabel>();
            bool isFinalDay = GameManagerInstance != null && GameManagerInstance.IsFinalDay;

            if (characters != null)
            {
                foreach (CharacterInstance c in characters)
                {
                    // 解放した人・判定した人の部屋には入れない
                    statusMap[c.CharacterId] = c.IsJudged;
                    // モニターには取調室と同じ画像を映す
                    if (c.ProfileData != null) faceMap[c.CharacterId] = c.ProfileData.characterIcon;
                    // 最終日は、判定することがその日の目的なので、判定の状態を出す
                    judgementMap[c.CharacterId] = !isFinalDay ? MonitorJudgementLabel.None
                        : c.IsConfirmedAI ? MonitorJudgementLabel.ConfirmedAI
                        : c.IsReleased ? MonitorJudgementLabel.Released
                        : MonitorJudgementLabel.Unjudged;
                }
            }

            if (monitorUI != null)
            {
                monitorUI.UpdateRoomStatus(statusMap);
                monitorUI.UpdateRoomFaces(faceMap);
                monitorUI.UpdateJudgementLabels(judgementMap);
            }
        }

        private void UpdateInterrogationState()
        {
            if (interrogationUI == null) return;

            CharacterInstance targetChar = GetCharacter(currentSelectedCharacterId);
            // 会話中・選択肢を選んでいる間は、次の行動はできない
            bool isPlaying = (dialogueUI != null && dialogueUI.IsDialoguePlaying) || (choiceUI != null && choiceUI.IsOpen);

            // 判定は各 Manager に問い合わせ、ビューには結果だけを渡す
            // 最終日は取り調べのボタンを隠して判定（解放・AI確定）のボタンだけを出す
            bool isFinalDay = GameManagerInstance != null && GameManagerInstance.IsFinalDay;
            bool canInterrogate = !isPlaying && InterrogationInstance != null
                && InterrogationInstance.CanExecuteAction(targetChar, InterrogationActionType.Talk, out _);
            bool canJudge = !isPlaying && InterrogationInstance != null
                && InterrogationInstance.CanExecuteAction(targetChar, InterrogationActionType.Release, out _);

            // 今日もう取り調べた人には、灰色のボタンを残さない
            // 行動を選んだその場ではボタンを消すだけにして、あとで部屋に入り直したときに「今日はもう取り調べました」と出す
            // （1日の行動回数とキャラの人数が同じなので、回数を使い切ったときも全員取り調べ済みになる）
            bool interrogatedToday = !isFinalDay && targetChar != null && InterrogationInstance != null
                && InterrogationInstance.HasInterrogatedToday(targetChar.CharacterId);
            bool showNotice = interrogatedToday && wasInterrogatedOnEnter;

            interrogationUI.UpdateActionButtons(!isFinalDay && !interrogatedToday, canInterrogate, isFinalDay, canJudge, showNotice);
        }

        /// <summary>
        /// 確認ダイアログを出し、「はい」なら処理する（ダイアログがなければすぐ処理する）
        /// </summary>
        private void Confirm(string message, Action onYes)
        {
            if (confirmDialog != null) confirmDialog.Show(message, onYes);
            else onYes?.Invoke();
        }

        /// <summary>
        /// 対象キャラに行動可能か確認し、不可ならその理由を会話欄に表示する
        /// </summary>
        private bool TryValidateAction(CharacterInstance targetChar, InterrogationActionType actionType)
        {
            if (!InterrogationInstance.CanExecuteAction(targetChar, actionType, out string rejectReason))
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
            ShowPages(speakerName, DialogueTextFormat.Parse(text));
        }

        /// <summary>
        /// 取り調べの結果（Excel の会話か短い文）を表示し、ログにも記録する
        /// </summary>
        private void ShowOutcome(CharacterInstance partner, ActionOutcome outcome)
        {
            if (outcome == null) return;
            string speakerName = GetDisplayName(partner);

            if (outcome.HasDialogue)
            {
                // 会話が終わったら、この会話の選択肢を調べる
                interrogationDialogue = outcome.Dialogue;
                interrogationPartner = partner;
                ShowPages(speakerName, DialogueTextFormat.FromScenario(outcome.Dialogue), IsSkippable(outcome.Dialogue));
            }
            else
            {
                ShowDialogue(speakerName, outcome.Text);
            }
        }

        private void ShowPages(string speakerName, List<DialoguePage> pages, bool skippable = true)
        {
            if (pages == null || pages.Count == 0) return;

            if (dialogueUI != null) dialogueUI.StartDialogue(speakerName, pages, skippable);

            if (logUI != null)
            {
                foreach (DialoguePage page in pages) AddLog(GetSpeaker(page, speakerName), page.Body);
            }
        }

        private static string GetSpeaker(DialoguePage page, string defaultSpeaker)
        {
            if (page.IsSystem) return "状況";
            return string.IsNullOrEmpty(page.Speaker) ? defaultSpeaker : page.Speaker;
        }

        private static Sprite FindCharacterSprite(string name)
        {
            ScenarioDatabase db = GameManagerInstance != null ? GameManagerInstance.Scenario : null;
            return db != null ? db.FindCharacterSprite(name) : null;
        }

        private static void PlaySeByName(string name)
        {
            ScenarioDatabase db = GameManagerInstance != null ? GameManagerInstance.Scenario : null;
            AudioClip clip = db != null ? db.FindSE(name) : null;
            if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySE(clip);
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
