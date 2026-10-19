#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.UI;

namespace TechC.SchoolGameJam2026.Scripts.DebugTools
{
    /// <summary>
    /// 開発中だけ使えるデバッグメニュー（F1 で開閉）
    /// Unity エディタと開発用ビルドでのみ、起動時に自動で作られる（完成版には含まれない）
    /// 状態を変えたら GameManager の RestoreState を通して、画面を描き直す
    /// </summary>
    public class DebugMenu : MonoBehaviour
    {
        private const float BaseHeight = 720f;
        private const int WindowId = 20261010;

        private bool isOpen;
        private Rect windowRect = new Rect(16, 16, 560, 680);
        private Vector2 scroll;
        private string flagInput = "";
        private string pendingDelete;   // 「本当に消しますか？」を出している記録の名前

        /// <summary> デバッグメニューが開いているか </summary>
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnStartup()
        {
            var go = new GameObject("[DebugMenu]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugMenu>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                isOpen = !isOpen;
                IsOpen = isOpen;
            }
        }

        private void OnDestroy()
        {
            IsOpen = false;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            // 画面の大きさに合わせて拡大する
            float scale = Mathf.Max(1f, Screen.height / BaseHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "デバッグメニュー（F1 で閉じる）");
        }

        private void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);

            GameManager game = GameManager.Instance;
            InterrogationManager interrogation = InterrogationManager.Instance;
            if (game != null && interrogation != null)
            {
                DrawStatus(game, interrogation);
                DrawSelection(game);
                DrawDayControls(game);
                DrawPointAndActionControls(game, interrogation);
                DrawCharacterControls(game);
                DrawFlagControls(game);
            }
            else
            {
                GUILayout.Label($"今のシーン: {SceneManager.GetActiveScene().name}");
                GUILayout.Label("状態を変える項目は、ゲーム本編（InGame）でだけ使えます。");
            }

            // エンディングの再生と記録の削除は、タイトル画面でも使える
            DrawEndingControls(game);
            DrawRecordControls();

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        #region 各項目

        private void DrawStatus(GameManager game, InterrogationManager interrogation)
        {
            Header("状態");
            int talkCount = SmartphoneTalkManager.Instance != null ? SmartphoneTalkManager.Instance.TalkCountToday : 0;
            GUILayout.Label($"{game.CurrentDay} 日目 / ポイント {interrogation.CurrentPoints} / 行動 {interrogation.CurrentDailyActions}/{interrogation.DailyActionLimit} / スマホ {talkCount} 回");

            string aiNames = "";
            foreach (CharacterInstance c in game.AllCharacters)
            {
                if (c != null && c.IsAI) aiNames += (aiNames.Length > 0 ? ", " : "") + Name(c);
            }
            GUILayout.Label($"AI: {(aiNames.Length > 0 ? aiNames : "なし")}");
        }

        private void DrawDayControls(GameManager game)
        {
            Header("日の移動（その日の朝から始め直す）");
            GUILayout.BeginHorizontal();
            for (int day = 1; day <= game.MaxDays; day++)
            {
                if (GUILayout.Button(day.ToString())) game.JumpToDay(day);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("今日の朝に戻す（今日をやり直す）")) game.ResetCurrentDay();
        }

        private void DrawPointAndActionControls(GameManager game, InterrogationManager interrogation)
        {
            Header("ポイント・行動回数");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10")) interrogation.AddPoints(10);
            if (GUILayout.Button("+100")) interrogation.AddPoints(100);
            if (GUILayout.Button("+1000")) interrogation.AddPoints(1000);
            if (GUILayout.Button("0 にする")) interrogation.SetPoints(0);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("今日の行動回数を 0 に戻す"))
            {
                // 取り調べ済みの記録も消えるので、取調室のボタンの状態を描き直す
                interrogation.ResetDailyActions(0);
                Refresh(game);
            }
            if (GUILayout.Button("スマホの会話回数を 0 に戻す") && SmartphoneTalkManager.Instance != null)
            {
                SmartphoneTalkManager.Instance.ResetForNewDay();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCharacterControls(GameManager game)
        {
            Header("キャラクター");
            foreach (CharacterInstance c in game.AllCharacters)
            {
                if (c == null) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{c.CharacterId} {Name(c)}  好感度 {c.CurrentAffection}", GUILayout.Width(220));
                if (GUILayout.Button("-", GUILayout.Width(30))) { c.AddAffection(-1); Refresh(game); }
                if (GUILayout.Button("+", GUILayout.Width(30))) { c.AddAffection(1); Refresh(game); }

                bool isAI = GUILayout.Toggle(c.IsAI, "AI", GUILayout.Width(50));
                if (isAI != c.IsAI) { c.SetAIFlag(isAI); Refresh(game); }

                bool isReleased = GUILayout.Toggle(c.IsReleased, "解放", GUILayout.Width(60));
                if (isReleased != c.IsReleased) { c.SetReleaseFlag(isReleased); Refresh(game); }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFlagControls(GameManager game)
        {
            Header($"フラグ（ON のもの: {game.Flags.Count} 個）");

            GUILayout.BeginHorizontal();
            flagInput = GUILayout.TextField(flagInput, GUILayout.MinWidth(260));
            if (GUILayout.Button("ON にする", GUILayout.Width(100)) && !string.IsNullOrWhiteSpace(flagInput))
            {
                game.Flags.TurnOn(flagInput);
                flagInput = "";
                Refresh(game);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("例: item_鍵 / info_アリバイ / flag_ココアと和解");

            foreach (string flag in game.Flags.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(flag);
                if (GUILayout.Button("OFF", GUILayout.Width(60)))
                {
                    game.Flags.TurnOff(flag);
                    Refresh(game);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawSelection(GameManager game)
        {
            Header("直前の会話の選ばれ方");
            ScenarioSelectionRecord selection = game.LastSelection;
            if (selection == null)
            {
                GUILayout.Label("まだ会話をしていません");
                return;
            }

            GUILayout.Label($"{selection.Day}日目 {selection.Scene}");
            if (selection.Choice != null)
            {
                ScenarioChoice choice = selection.Choice;
                GUILayout.Label($"{choice.sourceLocation}  獲得フラグ: {Join(choice.gainFlags)}  AI化: {Join(choice.aiTargets)}  次の会話: {(string.IsNullOrEmpty(choice.nextDialogueId) ? "なし" : choice.nextDialogueId)}");
                return;
            }
            if (selection.Candidates.Count == 0)
            {
                GUILayout.Label("条件シートにこの場面の行がありません");
                return;
            }

            foreach (ScenarioSelectionRecord.Candidate candidate in selection.Candidates)
            {
                string mark = candidate.Row == selection.Chosen ? "▶ 選ばれた" : candidate.Satisfied ? "　 満たす" : "　 満たさない";
                GUILayout.Label($"{mark}  {candidate.Row.sourceLocation}（優先度 {candidate.Row.priority}）{DescribeConditions(candidate.Row)} → {candidate.Row.dialogueId}");
            }
            if (selection.Chosen == null) GUILayout.Label("どの行も条件を満たさなかったので、会話の代わりに「……」が出ました");
        }

        private void DrawEndingControls(GameManager game)
        {
            Header("エンディング");
            ScenarioDatabase db = FindDatabase(game);
            if (db == null)
            {
                GUILayout.Label("シナリオのデータが見つかりません（本編かタイトル画面で使えます）");
                return;
            }

            if (game != null)
            {
                EndingDecision decision = game.DecideEnding();
                GUILayout.Label($"今の状態で決まるエンディング: {(decision.Ending != null ? decision.Ending.listName : "なし")}（判定結果: {ResultName(decision.Result)}）");
            }

            var endings = new List<ScenarioEnding>(db.endings);
            endings.Sort((a, b) => a.priority.CompareTo(b.priority));
            foreach (ScenarioEnding ending in endings)
            {
                if (GUILayout.Button($"「{ending.listName}」を再生（{ending.endingId}）") && PlayEnding(ending, db))
                {
                    // メニューが会話のクリックをさえぎらないように閉じる
                    isOpen = false;
                    IsOpen = false;
                }
            }
            GUILayout.Label("再生しても、見たエンディングには記録されません。終わるとゲームに戻ります");
        }

        private void DrawRecordControls()
        {
            Header("記録を消す");
            DeleteButton("セーブデータ（20枠すべて）", SaveStore.DeleteAll);
            DeleteButton("見たエンディングの記録", EndingRecordStore.ClearAll);
            DeleteButton("既読の記録", ReadHistoryStore.ClearAll);
            DeleteButton("手に入れた情報の記録（周回の数も）", InfoRecordStore.ClearAll);
            GUILayout.Label("消したあと、タイトル画面などの表示は開き直すと変わります");
        }

        /// <summary> 「消す」ボタン。押すと「本当に消しますか？」を出し、もう一度押すと消す </summary>
        private void DeleteButton(string label, Action delete)
        {
            GUILayout.BeginHorizontal();
            if (pendingDelete == label)
            {
                GUILayout.Label($"{label}を本当に消しますか？", GUILayout.Width(330));
                if (GUILayout.Button("消す", GUILayout.Width(80)))
                {
                    delete();
                    pendingDelete = null;
                    Debug.Log($"[DebugMenu] {label}を消しました");
                }
                if (GUILayout.Button("やめる", GUILayout.Width(80))) pendingDelete = null;
            }
            else
            {
                GUILayout.Label(label, GUILayout.Width(330));
                if (GUILayout.Button("消す", GUILayout.Width(80))) pendingDelete = label;
            }
            GUILayout.EndHorizontal();
        }

        #endregion

        #region デバッグメニューの処理（テストからも呼ぶ）

        /// <summary>
        /// エンディングの会話をすぐに再生する（見たエンディングには記録しない。終わったら元の画面に戻る）
        /// </summary>
        /// <returns>再生を始められたら true（会話がない・再生する画面がないときは false）</returns>
        public static bool PlayEnding(ScenarioEnding ending, ScenarioDatabase db)
        {
            if (ending == null || db == null) return false;

            ScenarioDialogue dialogue = db.FindDialogue(ending.endingId);
            EndingPlayerUI player = FindAnyObjectByType<EndingPlayerUI>(FindObjectsInactive.Include);
            if (dialogue == null || dialogue.pages.Count == 0 || player == null) return false;

            player.Play(dialogue, db, null);
            return true;
        }

        /// <summary> エンディングを探すシナリオのデータ（本編なら GameManager、タイトル画面ならエンディング一覧から） </summary>
        private static ScenarioDatabase FindDatabase(GameManager game)
        {
            if (game != null && game.Scenario != null) return game.Scenario;
            EndingListUI list = FindAnyObjectByType<EndingListUI>(FindObjectsInactive.Include);
            return list != null ? list.Database : null;
        }

        /// <summary> 条件シートの行の条件を、短い文にする（例: 本人の好感度 2以上・info_x が ON） </summary>
        public static string DescribeConditions(ScenarioConditionRow row)
        {
            var parts = new List<string>();
            if (row.hasSelfAffection) parts.Add($"本人の好感度 {row.selfAffection}以上");
            foreach (AffectionCondition other in row.otherAffections) parts.Add($"{other.characterId} の好感度 {other.minValue}以上");
            if (row.hasActionCount) parts.Add($"この行動 {row.actionCount}回以上");
            foreach (string flag in row.requiredFlags) parts.Add($"{flag} が ON");
            foreach (string flag in row.ngFlags) parts.Add($"{flag} が OFF");
            return parts.Count > 0 ? string.Join("・", parts) : "条件なし";
        }

        private static string ResultName(EndingResult result)
        {
            switch (result)
            {
                case EndingResult.AllCorrect: return "全員正解";
                case EndingResult.ConfirmedHuman: return "人間をAI確定した";
                case EndingResult.ReleasedAI: return "AIを解放した";
                default: return "指定なし";
            }
        }

        private static string Join(List<string> values) => values.Count > 0 ? string.Join("・", values) : "なし";

        #endregion

        /// <summary>
        /// 変えた状態を、画面に反映する（状態をそのまま書き出して戻すことで、画面の描き直しの通知を出す）
        /// </summary>
        private static void Refresh(GameManager game)
        {
            game.RestoreState(game.CaptureState());
        }

        private static void Header(string text)
        {
            GUILayout.Space(8);
            GUILayout.Label(text, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        }

        private static string Name(CharacterInstance c)
        {
            return c.ProfileData != null ? c.ProfileData.basicInfo.characterName : c.CharacterId;
        }
    }
}
#endif
