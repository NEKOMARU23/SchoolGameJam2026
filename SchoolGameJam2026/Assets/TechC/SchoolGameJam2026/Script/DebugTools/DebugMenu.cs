#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;

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
        private Rect windowRect = new Rect(16, 16, 520, 680);
        private Vector2 scroll;
        private string flagInput = "";

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
            if (game == null || interrogation == null)
            {
                GUILayout.Label($"今のシーン: {SceneManager.GetActiveScene().name}");
                GUILayout.Label("ゲーム本編（InGame）でのみ使えます。");
                GUILayout.EndScrollView();
                GUI.DragWindow();
                return;
            }

            DrawStatus(game, interrogation);
            DrawDayControls(game);
            DrawPointAndActionControls(game, interrogation);
            DrawCharacterControls(game);
            DrawFlagControls(game);

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
