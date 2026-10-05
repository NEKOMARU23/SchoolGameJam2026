#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
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
            yield return SceneManager.LoadSceneAsync(InGameSceneName, LoadSceneMode.Single);

            // Awake → Start を確実に通すため数フレーム待つ
            yield return null;
            yield return null;

            Assert.IsNotNull(GameManager.Instance, "GameManager が見つかりません");
            Assert.IsNotNull(InterrogationManager.Instance, "InterrogationManager が見つかりません");
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

        public static CharacterInstance GetAICharacter()
        {
            CharacterInstance ai = GameManager.AllCharacters.Find(c => c.IsAI);
            Assert.IsNotNull(ai, "AI（isAI = true）のキャラクターが設定されていません");
            return ai;
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
