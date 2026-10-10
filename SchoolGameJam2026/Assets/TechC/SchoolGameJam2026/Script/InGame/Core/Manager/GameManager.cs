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

        #region プロパティ
        public int CurrentDay => currentDay;
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
        public void ApplyScenarioEffects(ScenarioConditionRow row)
        {
            if (row == null) return;

            foreach (string flag in row.gainFlags) Flags.TurnOn(flag);

            foreach (string id in row.aiTargets)
            {
                CharacterInstance character = allCharacters.Find(c => c != null && c.CharacterId == id);
                if (character != null && !character.IsAI)
                {
                    character.SetAIFlag(true);
                    Debug.Log($"[GameManager] 会話（{row.dialogueId}）により {id} が AI になりました。");
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
            morningSnapshot = CaptureState();

            OnDayStarted?.Invoke(currentDay);
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
            OnStateRestored?.Invoke();
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
        /// リザルトを閉じた後の進行。最終日ならエンディングへ、それ以外は翌日へ進む
        /// </summary>
        /// <returns>翌日に進んだ場合 true、エンディングへ遷移した場合 false</returns>
        public bool AdvanceDayOrFinishGame()
        {
            if (IsFinalDay)
            {
                FinishGame();
                return false;
            }

            AdvanceToNextDay();
            return true;
        }

        private void FinishGame()
        {
            // エンディングシートでの分岐ができるまでは、AI を解放したらゲームオーバー、それ以外はクリア
            EndingResult result = EvaluateFinalResult();
            bool isGameOver = result == EndingResult.ReleasedAI;
            string nextScene = isGameOver ? gameOverSceneName : clearSceneName;
            Debug.Log($"<color=gold>全日程が終了しました。判定結果: {result}（{nextScene}）へ遷移します。</color>");

            if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
        }
    }
}