using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// 最終日の後にどのエンディングになるか
    /// </summary>
    public class EndingDecision
    {
        /// <summary> 最終日の判定結果 </summary>
        public EndingResult Result;

        /// <summary> エンディングシートで選ばれた行（当てはまる行がなければ null） </summary>
        public ScenarioEnding Ending;

        /// <summary> エンディングの会話（なければ null。会話を見せずに画面を移る） </summary>
        public ScenarioDialogue Dialogue;

        /// <summary> 会話の後にゲームオーバー画面に移るか（false ならクリア画面） </summary>
        public bool IsGameOver;
    }

    /// <summary>
    /// ゲーム全体の進行管理、日数、および「その日の朝に戻す」リセット機能を管理するマネージャー (Singleton)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("ゲーム進行設定")]
        [Tooltip("現在の進行日数 (1〜)")]
        [SerializeField] private int currentDay = 1;

        [Tooltip("最大日数")]
        [SerializeField] private int maxDays = 7;

        [Header("キャラクター一覧 (A, B, C, D)")]
        [SerializeField] private List<CharacterInstance> allCharacters = new List<CharacterInstance>();

        [Header("シナリオ（Excel から取り込んだデータ。Tools ＞ シナリオExcelを取り込む で作る）")]
        [SerializeField] private ScenarioDatabase scenarioDatabase;

        [Header("エンディング遷移先")]
        [SerializeField] private string clearSceneName = "ClearScene";
        [SerializeField] private string gameOverSceneName = "GameOverScene";

        // その日の朝の状態（「今日をやり直す」で戻す先）
        private GameStateSnapshot morningSnapshot;

        // この周回で手に入れたフラグの記録（いつ・どうやって。資料画面用）
        private readonly List<InfoAcquisition> acquisitions = new List<InfoAcquisition>();

        // まだ流していない朝の会話（画面側が TakeMorningTalk で受け取って流す）
        private ScenarioDialogue pendingMorningTalk;

        #region プロパティ
        public int CurrentDay => currentDay;

        /// <summary> 今が何周目か（エンディングまで行くたびに増える。1周目は 1） </summary>
        public int CurrentPlaythrough { get; private set; } = 1;

        /// <summary> この周回で手に入れたフラグの記録（古い順） </summary>
        public IReadOnlyList<InfoAcquisition> Acquisitions => acquisitions;
        public int MaxDays => maxDays;
        public List<CharacterInstance> AllCharacters => allCharacters;

        /// <summary> キーアイテム・情報・進行フラグ（名前付きの ON / OFF） </summary>
        public FlagStore Flags { get; } = new FlagStore();

        /// <summary> その日の朝の状態のコピー（セーブで一緒に保存する） </summary>
        public GameStateSnapshot MorningSnapshot => morningSnapshot != null ? CloneSnapshot(morningSnapshot) : null;

        /// <summary> AIが解放されてしまったか（キャラ状態から導出するため、リセット時も自動で整合する） </summary>
        public bool HasAIEscaped => allCharacters.Exists(c => c != null && c.IsAI && c.IsReleased);

        /// <summary> 全員を判定（解放か AI確定）し終えたか。最終日はこれが true になるまで1日を終えられない </summary>
        public bool AreAllJudged => allCharacters.TrueForAll(c => c == null || c.IsJudged);
        #endregion

        #region イベント
        public event Action<int> OnDayStarted;
        public event Action OnDayReset;

        /// <summary> 状態をまとめて戻したとき（リセット・ロード・デバッグ）に通知。画面の更新に使う </summary>
        public event Action OnStateRestored;
        public event Action OnNightPhaseStarted;

        /// <summary> セーブを読み込んだとき（状態を戻した後）に通知。ログなど画面側の情報を戻すのに使う </summary>
        public event Action<SaveData> OnSaveLoaded;

        /// <summary>
        /// タイトル画面の「つづきから」で読み込んだセーブ（はじめからなら null）
        /// 画面側の Start が GameManager の Start より後になると OnSaveLoaded を受け取れないので、ここからも受け取れるようにする
        /// </summary>
        public SaveData SaveLoadedAtStart { get; private set; }
        #endregion

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // タイトル画面の「つづきから」で選んだセーブがあれば、その続きから始める
            if (SaveStore.TakePendingLoad(out SaveData save))
            {
                SaveLoadedAtStart = save;
                ApplySave(save);
                return;
            }

            SaveStore.CurrentSlot = 0;   // はじめから
            CurrentPlaythrough = InfoRecordStore.CompletedPlaythroughs + 1;
            StartNewDay();
        }

        /// <summary> シナリオのデータ（会話・条件・日別設定・フラグ一覧など） </summary>
        public ScenarioDatabase Scenario => scenarioDatabase;

        /// <summary>
        /// 条件シートの判定に使う、今の状態をまとめる
        /// </summary>
        /// <param name="partner">会話の相手（スマホのときは null）</param>
        /// <param name="actionCount">この行動をこのキャラにした通算回数（今回の分は含まない）</param>
        public ScenarioSelector.Context CreateSelectorContext(CharacterInstance partner, int actionCount)
        {
            return new ScenarioSelector.Context
            {
                SelfAffection = partner != null ? partner.CurrentAffection : 0,
                AffectionOf = id => allCharacters.Find(c => c != null && c.CharacterId == id)?.CurrentAffection ?? 0,
                ActionCount = actionCount,
                Flags = Flags,
            };
        }

        /// <summary>
        /// 会話を聞いたときの効果を適用する（獲得フラグを ON、AI化のキャラを AI にする）
        /// </summary>
        /// <param name="method">手に入れ方（資料画面に出す。例: ココアに対話）</param>
        public void ApplyScenarioEffects(ScenarioConditionRow row, string method = "")
        {
            if (row == null) return;
            ApplyEffects(row.gainFlags, row.aiTargets, $"会話（{row.dialogueId}）", method);
        }

        /// <summary>
        /// フラグを ON にして、手に入れた記録を残す（ポイントで解禁する情報など、会話以外で手に入れるとき用）
        /// </summary>
        public void GainFlags(IEnumerable<string> flags, string method)
        {
            ApplyEffects(flags, Array.Empty<string>(), method, method);
        }

        /// <summary> キャラの表示名（資料画面の「手に入れ方」などに使う） </summary>
        public string GetCharacterName(string characterId)
        {
            CharacterInstance c = allCharacters.Find(x => x != null && x.CharacterId == characterId);
            if (c == null) return characterId ?? "";
            return c.ProfileData != null ? c.ProfileData.basicInfo.characterName : c.CharacterId;
        }

        /// <summary>
        /// 会話の後に出す選択肢のうち、今の状態で条件を満たすものを並べる順に返す
        /// </summary>
        /// <param name="partner">会話の相手（スマホのときは null）</param>
        public List<ScenarioChoice> GetAvailableChoices(ScenarioDialogue dialogue, CharacterInstance partner)
        {
            var available = new List<ScenarioChoice>();
            if (dialogue == null || scenarioDatabase == null) return available;

            ScenarioSelector.Context context = CreateSelectorContext(partner, 0);
            foreach (ScenarioChoice choice in scenarioDatabase.GetChoices(dialogue.id))
            {
                if (ScenarioSelector.IsSatisfied(choice, context)) available.Add(choice);
            }
            return available;
        }

        /// <summary>
        /// 選択肢を選んだときの効果（好感度の変化・獲得フラグ・AI化）を適用し、次に出す会話を返す
        /// 行動回数・スマホの回数は使わない（最初の行動・会話で1回分を使っている）
        /// </summary>
        /// <param name="partner">会話の相手（スマホのときは null。好感度の変化は起きない）</param>
        /// <returns>次の会話（なければ null）</returns>
        public ScenarioDialogue SelectChoice(ScenarioChoice choice, CharacterInstance partner)
        {
            if (choice == null) return null;

            if (choice.affectionChange != 0 && partner != null) partner.AddAffection(choice.affectionChange);
            string method = partner != null ? $"{GetCharacterName(partner.CharacterId)}との会話の選択肢" : "スマホの選択肢";
            ApplyEffects(choice.gainFlags, choice.aiTargets, $"選択肢（{choice.sourceLocation}「{choice.text}」）", method);

            return scenarioDatabase != null ? scenarioDatabase.FindDialogue(choice.nextDialogueId) : null;
        }

        /// <summary> 獲得フラグを ON にし、AI化のキャラを AI にする </summary>
        private void ApplyEffects(IEnumerable<string> gainFlags, IEnumerable<string> aiTargets, string source, string method)
        {
            foreach (string flag in gainFlags)
            {
                // 初めて ON になったときだけ、この周回の記録と、周回をまたぐ記録に残す（進行フラグは資料画面に出さないので周回をまたぐ記録には残さない）
                if (!Flags.TurnOn(flag)) continue;
                acquisitions.Add(new InfoAcquisition { flag = flag, day = currentDay, method = method ?? "" });
                if (!flag.StartsWith(FlagStore.FlagPrefix, StringComparison.Ordinal))
                {
                    InfoRecordStore.RecordAcquired(flag, CurrentPlaythrough, currentDay, method);
                }
            }

            foreach (string id in aiTargets)
            {
                CharacterInstance character = allCharacters.Find(c => c != null && c.CharacterId == id);
                if (character != null && !character.IsAI)
                {
                    character.SetAIFlag(true);
                    Debug.Log($"[GameManager] {source}により {id} が AI になりました。");
                }
            }
        }

        private bool HasScenarioForDay(int day)
        {
            return scenarioDatabase != null && scenarioDatabase.HasDay(day);
        }

        /// <summary> 全キャラクターの好感度を辞書で取得する </summary>
        public Dictionary<string, int> GetAllCharacterAffections()
        {
            var dict = new Dictionary<string, int>();
            foreach (var character in allCharacters)
            {
                if (character != null && !string.IsNullOrEmpty(character.CharacterId))
                {
                    dict[character.CharacterId] = character.CurrentAffection;
                }
            }
            return dict;
        }

        /// <summary> 新しい1日（朝）を開始する </summary>
        public void StartNewDay()
        {
            Debug.Log($"<color=cyan>=== {currentDay} 日目の朝が始まりました ===</color>");

            // その日だけの状態（行動回数・スマホの会話回数など）を朝の状態にしてから、朝の状態を記録する
            InterrogationManager.Instance?.ResetDailyActions(0);
            SmartphoneTalkManager.Instance?.ResetForNewDay();

            // 朝の会話の効果（獲得フラグ・AI化）は、朝の状態を記録する前に反映する
            // （「今日をやり直す」で効果が消えず、会話も流し直さないようにするため）
            pendingMorningTalk = SelectMorningTalk();
            morningSnapshot = CaptureState();

            OnDayStarted?.Invoke(currentDay);
        }

        /// <summary>
        /// 条件シートの行動「朝」から、その日の朝の会話を選び、効果を反映する
        /// </summary>
        /// <returns>流す会話。その日の朝の会話がなければ null</returns>
        private ScenarioDialogue SelectMorningTalk()
        {
            if (scenarioDatabase == null) return null;

            ScenarioConditionRow row = ScenarioSelector.Select(
                scenarioDatabase.GetConditionRows(currentDay, "", ScenarioAction.Morning),
                CreateSelectorContext(null, 0));
            if (row == null) return null;

            ApplyScenarioEffects(row, "スマホ（朝の連絡）");
            return scenarioDatabase.FindDialogue(row.dialogueId);
        }

        /// <summary>
        /// まだ流していない朝の会話を受け取る（受け取ったら空になるので、1日に1回だけ流れる）
        /// ロードや「今日をやり直す」では朝の会話は用意されない
        /// </summary>
        /// <returns>流す会話。なければ null</returns>
        public ScenarioDialogue TakeMorningTalk()
        {
            ScenarioDialogue talk = pendingMorningTalk;
            pendingMorningTalk = null;
            return talk;
        }

        /// <summary>
        /// 「今日を最初からやり直す」: その日に行われたすべての行為を取り消し、朝の状態に戻す
        /// </summary>
        public void ResetCurrentDay()
        {
            if (morningSnapshot == null) return;

            Debug.Log($"<color=yellow>⚠️ {currentDay} 日目の朝の状態にリセットします。</color>");
            RestoreState(morningSnapshot);
            OnDayReset?.Invoke();
        }

        #region 状態の書き出し・復元（今日をやり直す・セーブ・ロード用）

        /// <summary>
        /// 今のゲームの状態をまとめて書き出す
        /// 新しい状態を追加したら、ここと RestoreState の両方に追加する
        /// </summary>
        public GameStateSnapshot CaptureState()
        {
            var snapshot = new GameStateSnapshot { day = currentDay };

            InterrogationManager.Instance?.CaptureState(snapshot);
            SmartphoneTalkManager.Instance?.CaptureState(snapshot);

            foreach (var character in allCharacters)
            {
                if (character == null || string.IsNullOrEmpty(character.CharacterId)) continue;
                snapshot.characters.Add(character.CaptureState());
            }

            snapshot.flags = Flags.ToList();
            snapshot.playthrough = CurrentPlaythrough;
            foreach (InfoAcquisition a in acquisitions)
            {
                snapshot.acquisitions.Add(new InfoAcquisition { flag = a.flag, day = a.day, method = a.method });
            }
            return snapshot;
        }

        /// <summary>
        /// 書き出した状態にまとめて戻す（日数も戻る）
        /// </summary>
        public void RestoreState(GameStateSnapshot snapshot)
        {
            if (snapshot == null) return;

            currentDay = snapshot.day;
            InterrogationManager.Instance?.RestoreState(snapshot);
            SmartphoneTalkManager.Instance?.RestoreState(snapshot);

            foreach (var state in snapshot.characters)
            {
                CharacterInstance character = allCharacters.Find(c => c != null && c.CharacterId == state.characterId);
                if (character != null) character.RestoreState(state);
            }

            Flags.RestoreFrom(snapshot.flags);
            CurrentPlaythrough = Mathf.Max(1, snapshot.playthrough);
            acquisitions.Clear();
            if (snapshot.acquisitions != null)
            {
                foreach (InfoAcquisition a in snapshot.acquisitions)
                {
                    if (a != null) acquisitions.Add(new InfoAcquisition { flag = a.flag, day = a.day, method = a.method });
                }
            }
            OnStateRestored?.Invoke();
        }

        /// <summary>
        /// セーブするデータを作る（今の状態と、その日の朝の状態をまとめる）
        /// </summary>
        /// <param name="log">これまでのログ（画面の側が持っている）</param>
        public SaveData CreateSaveData(List<LogEntry> log)
        {
            GameStateSnapshot current = CaptureState();
            return new SaveData
            {
                day = currentDay,
                remainingActions = InterrogationManager.Instance != null ? InterrogationManager.Instance.RemainingDailyActions : 0,
                current = current,
                morning = morningSnapshot != null ? CloneSnapshot(morningSnapshot) : CloneSnapshot(current),
                log = log != null ? new List<LogEntry>(log) : new List<LogEntry>(),
            };
        }

        /// <summary>
        /// セーブした状態に戻す（朝の状態も戻すので、ロード後も「今日をやり直す」で朝に戻れる）
        /// </summary>
        public void ApplySave(SaveData save)
        {
            if (save == null || save.current == null) return;

            morningSnapshot = CloneSnapshot(save.morning ?? save.current);
            pendingMorningTalk = null;
            RestoreState(save.current);
            OnSaveLoaded?.Invoke(save);
        }

        /// <summary>
        /// 【デバッグ用】指定した日の朝から始め直す（ポイント・好感度・フラグなどは今のまま）
        /// </summary>
        /// <returns>移動できた場合 true（範囲外・シナリオがない日は false）</returns>
        public bool JumpToDay(int day)
        {
            if (day < 1 || day > maxDays) return false;
            if (!HasScenarioForDay(day))
            {
                Debug.LogWarning($"Day {day} のシナリオが見つからないため移動できません。");
                return false;
            }

            currentDay = day;
            StartNewDay();
            return true;
        }

        private static GameStateSnapshot CloneSnapshot(GameStateSnapshot source)
        {
            return JsonUtility.FromJson<GameStateSnapshot>(JsonUtility.ToJson(source));
        }

        #endregion

        /// <summary> 夜フェーズ（1日の行動終了後）への移行処理 </summary>
        public void TransitionToNightPhase()
        {
            Debug.Log("<color=purple>--- 1日の取り調べが終了しました。夜フェーズへ移行します ---</color>");
            OnNightPhaseStarted?.Invoke();
        }

        /// <summary>
        /// AI判定シートの判定: その日が判定日のキャラは、回避フラグがすべて ON でなければ AI になる
        /// （1日を終えて翌日になるときに呼ぶ。一度 AI になったら戻らない）
        /// </summary>
        public void ApplyAIJudgements(int day)
        {
            if (scenarioDatabase == null) return;

            foreach (ScenarioAIJudgement judgement in scenarioDatabase.aiJudgements)
            {
                if (judgement.day != day || Flags.AreAllOn(judgement.avoidFlags)) continue;

                CharacterInstance character = allCharacters.Find(c => c != null && c.CharacterId == judgement.characterId);
                if (character != null && !character.IsAI)
                {
                    character.SetAIFlag(true);
                    Debug.Log($"[GameManager] {day}日目の終わりに回避フラグがそろっていなかったため、{character.CharacterId} が AI になりました。");
                }
            }
        }

        /// <summary> 現在の日を終了し、次の日（朝）へ移行する </summary>
        public void AdvanceToNextDay()
        {
            int nextDay = currentDay + 1;

            if (nextDay > maxDays)
            {
                Debug.LogWarning($"最終日（{maxDays}日目）を超えて日付を進めることはできません。");
                return;
            }

            // 日付を進める前に確認し、シナリオ欠落時に行動回数が未リセットのまま進むのを防ぐ
            if (!HasScenarioForDay(nextDay))
            {
                Debug.LogError($"Day {nextDay} のシナリオが見つかりません。");
                return;
            }

            // 翌朝の状態を記録する前に判定し、「今日をやり直す」で戻らないようにする
            ApplyAIJudgements(currentDay);

            currentDay = nextDay;
            StartNewDay();
        }

        /// <summary> 現在が最終日かどうか </summary>
        public bool IsFinalDay => currentDay >= maxDays;

        /// <summary>
        /// 最終日の判定の結果（エンディングの分岐に使う）
        /// AI を1人でも解放していれば「AIを解放した」、人間を AI確定していれば「人間をAI確定した」、それ以外は「全員正解」
        /// </summary>
        public EndingResult EvaluateFinalResult()
        {
            if (HasAIEscaped) return EndingResult.ReleasedAI;
            if (allCharacters.Exists(c => c != null && c.IsConfirmedAI && !c.IsAI)) return EndingResult.ConfirmedHuman;
            return EndingResult.AllCorrect;
        }

        /// <summary>
        /// 最終日の判定結果とフラグから、エンディングシートでどのエンディングになるかを決める
        /// エンディングシートに当てはまる行がなければ、AI を解放したらゲームオーバー、それ以外はクリア（会話なし）
        /// </summary>
        public EndingDecision DecideEnding()
        {
            EndingResult result = EvaluateFinalResult();
            ScenarioEnding ending = scenarioDatabase != null ? scenarioDatabase.SelectEnding(result, Flags) : null;

            return new EndingDecision
            {
                Result = result,
                Ending = ending,
                Dialogue = ending != null && scenarioDatabase != null ? scenarioDatabase.FindDialogue(ending.endingId) : null,
                IsGameOver = ending != null ? ending.kind == EndingKind.GameOver : result == EndingResult.ReleasedAI,
            };
        }

        /// <summary>
        /// エンディングを見たことを記録し、クリア画面かゲームオーバー画面に移る
        /// （エンディングの会話を見せた後に呼ぶ）
        /// </summary>
        public void FinishGame(EndingDecision decision)
        {
            if (decision == null) decision = DecideEnding();
            if (decision.Ending != null) EndingRecordStore.MarkSeen(decision.Ending.endingId);
            InfoRecordStore.CompletePlaythrough(CurrentPlaythrough);   // 次に「はじめから」を始めると、次の周回になる

            string nextScene = decision.IsGameOver ? gameOverSceneName : clearSceneName;
            Debug.Log($"<color=gold>全日程が終了しました。判定結果: {decision.Result} / エンディング: {decision.Ending?.endingId ?? "なし"}（{nextScene}）へ遷移します。</color>");

            if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
        }
    }
}