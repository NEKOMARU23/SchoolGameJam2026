#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.UI;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// InGame シーンの PlayMode テスト用の共通処理
    /// </summary>
    public static class InGameTestUtility
    {
        public const string InGameSceneName = "InGame";
        public const string ClearSceneName = "ClearScene";
        public const string GameOverSceneName = "GameOverScene";

        // セリフ送りやシーン遷移を待つ上限フレーム数（無限ループ防止）
        private const int MaxWaitFrames = 600;

        public static GameManager GameManager => GameManager.Instance;
        public static InterrogationManager InterrogationManager => InterrogationManager.Instance;

        /// <summary>
        /// InGame シーンを読み込み、各コンポーネントの Start が終わるまで待つ
        /// </summary>
        public static IEnumerator LoadInGameScene()
        {
            UseTestEndingRecord();
            yield return SceneManager.LoadSceneAsync(InGameSceneName, LoadSceneMode.Single);

            // Awake → Start を確実に通すため数フレーム待つ
            yield return null;
            yield return null;

            Assert.IsNotNull(GameManager.Instance, "GameManager が見つかりません");
            Assert.IsNotNull(InterrogationManager.Instance, "InterrogationManager が見つかりません");

            // 日付切り替えの演出はテスト時間を延ばすだけなので省略する（演出自体は専用のテストで確認）
            FindComponent<DayTransitionUI>().SetDurations(0f, 0f);
        }

        /// <summary>
        /// 見たエンディングと既読の記録を、本物のファイルではなくテスト用の空のファイルに書くようにする
        /// </summary>
        public static void UseTestEndingRecord()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "test_ending_record.json");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            EndingRecordStore.SetFilePathForTest(path);

            string readPath = System.IO.Path.Combine(Application.temporaryCachePath, "test_read_record.json");
            if (System.IO.File.Exists(readPath)) System.IO.File.Delete(readPath);
            ReadHistoryStore.SetFilePathForTest(readPath);
        }

        public static T FindComponent<T>() where T : Object
        {
            T component = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            Assert.IsNotNull(component, $"{typeof(T).Name} がシーン内に見つかりません");
            return component;
        }

        /// <summary>
        /// 非アクティブも含めてボタンを探す
        /// </summary>
        /// <param name="name">ボタンの GameObject 名</param>
        /// <param name="parentPathContains">同名ボタンを区別するための親階層の一部（例: "MonitorPanel"）</param>
        public static Button FindButton(string name, string parentPathContains = null)
        {
            Button[] candidates = Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
                .Where(b => b.gameObject.name == name)
                .Where(b => parentPathContains == null || GetPath(b.transform).Contains(parentPathContains))
                .ToArray();

            Assert.AreEqual(1, candidates.Length, $"ボタン '{name}'（親: {parentPathContains ?? "指定なし"}）が {candidates.Length} 個見つかりました。1 個に特定できません");
            return candidates[0];
        }

        /// <summary>
        /// プレイヤーと同じ条件（表示中・押下可能）でボタンを押し、1フレーム進める
        /// </summary>
        public static IEnumerator Click(string name, string parentPathContains = null)
        {
            Button button = FindButton(name, parentPathContains);

            Assert.IsTrue(button.gameObject.activeInHierarchy, $"ボタン '{GetPath(button.transform)}' が画面に表示されていません");
            Assert.IsTrue(button.interactable, $"ボタン '{GetPath(button.transform)}' が押せない状態です");

            button.onClick.Invoke();
            yield return null;
        }

        /// <summary>
        /// 会話が終わるまでセリフ送りボタンを押し続ける
        /// </summary>
        public static IEnumerator SkipDialogueByButton()
        {
            DialogueUI dialogueUI = FindComponent<DialogueUI>();

            for (int i = 0; i < MaxWaitFrames && dialogueUI.IsDialoguePlaying; i++)
            {
                yield return Click("TextSkipButton", "Interrogation_Chatbox");
            }

            Assert.IsFalse(dialogueUI.IsDialoguePlaying, "セリフ送りを続けても会話が終わりません");
        }

        /// <summary>
        /// 会話が終わるまで DialogueUI を直接送る（ボタン配線を問わない高速版）
        /// </summary>
        public static IEnumerator SkipDialogue()
        {
            DialogueUI dialogueUI = FindComponent<DialogueUI>();

            for (int i = 0; i < MaxWaitFrames && dialogueUI.IsDialoguePlaying; i++)
            {
                dialogueUI.OnClickDialogueArea();
                yield return null;
            }

            Assert.IsFalse(dialogueUI.IsDialoguePlaying, "セリフ送りを続けても会話が終わりません");
        }

        /// <summary>
        /// 指定シーンがアクティブになるまで待つ
        /// </summary>
        public static IEnumerator WaitForScene(string sceneName)
        {
            for (int i = 0; i < MaxWaitFrames && SceneManager.GetActiveScene().name != sceneName; i++)
            {
                // 最終日の後のエンディングの会話は、送りながら待つ
                EndingPlayerUI ending = Object.FindAnyObjectByType<EndingPlayerUI>();
                if (ending != null && ending.IsPlaying) ending.Advance();
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, $"{sceneName} に遷移しませんでした");
        }

        public static bool IsPanelActive(string panelName)
        {
            GameObject panel = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include)
                .Select(t => t.gameObject)
                .FirstOrDefault(go => go.name == panelName);

            Assert.IsNotNull(panel, $"パネル '{panelName}' が見つかりません");
            return panel.activeInHierarchy;
        }

        public static CharacterInstance GetCharacter(string characterId)
        {
            CharacterInstance character = GameManager.AllCharacters.Find(c => c.CharacterId == characterId);
            Assert.IsNotNull(character, $"キャラクター '{characterId}' が見つかりません");
            return character;
        }

        public const string ReleaseButtonName = "Button_Liberation";
        public const string ConfirmAIButtonName = "Button_AI confirmed (1)";

        /// <summary> 確認ダイアログ（AI確定・解放・プロフィール解禁）の親の階層 </summary>
        public const string ConfirmDialogPath = "Canvas/ConfirmDialog";

        /// <summary>
        /// 確認ダイアログが出ていることを確かめて「はい」を押す
        /// </summary>
        public static IEnumerator ClickConfirmYes()
        {
            Assert.IsTrue(IsConfirmDialogOpen(ConfirmDialogPath), "確認ダイアログが出ていません");
            yield return Click("YesButton", ConfirmDialogPath);
            Assert.IsFalse(IsConfirmDialogOpen(ConfirmDialogPath), "「はい」を押しても確認ダイアログが閉じません");
        }

        /// <summary>
        /// 確認ダイアログを、置き場所（例: "Canvas/ConfirmDialog"）で探す
        /// ゲーム中の確認とセーブ・ロードの確認は同じ名前なので、名前だけでは区別できない
        /// </summary>
        public static ConfirmDialogUI GetConfirmDialog(string pathContains)
        {
            ConfirmDialogUI[] found = Object.FindObjectsByType<ConfirmDialogUI>(FindObjectsInactive.Include)
                .Where(d => GetPath(d.transform).Contains(pathContains))
                .ToArray();
            Assert.AreEqual(1, found.Length, $"確認ダイアログ（{pathContains}）が {found.Length} 個見つかりました");
            return found[0];
        }

        public static bool IsConfirmDialogOpen(string pathContains)
        {
            return GetConfirmDialog(pathContains).gameObject.activeInHierarchy;
        }

        /// <summary>
        /// 最終日に、対象キャラの部屋で解放か AI確定のボタンを押し、確認で「はい」を押して会話を最後まで送る
        /// </summary>
        public static IEnumerator JudgeOnFinalDay(string characterId, bool confirmAI)
        {
            Assert.IsTrue(GameManager.IsFinalDay, "前提: 最終日である");

            FindComponent<GameUIController>().SelectCharacter(characterId);
            yield return null;

            yield return Click(confirmAI ? ConfirmAIButtonName : ReleaseButtonName);
            yield return ClickConfirmYes();
            yield return SkipDialogue();

            CharacterInstance character = GetCharacter(characterId);
            Assert.IsTrue(confirmAI ? character.IsConfirmedAI : character.IsReleased, $"{characterId} の判定が反映されていません");
        }

        /// <summary>
        /// モニター画面の「1日を終える」→ 確認で「はい」→ リザルトの「次へ」を押す
        /// </summary>
        public static IEnumerator EndDayByButton()
        {
            FindComponent<ScreenNavigationUI>().ShowMonitorPanel();
            yield return null;
            yield return Click("Day_Button", "MonitorPanel");
            yield return Click("YesButton", "DayEndConfirmDialog");
            Assert.IsTrue(IsPanelActive("ResultPanel"), "リザルト画面が表示されません");
            yield return Click("NextDay_Button", "ResultPanel");
        }

        private static string GetPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
