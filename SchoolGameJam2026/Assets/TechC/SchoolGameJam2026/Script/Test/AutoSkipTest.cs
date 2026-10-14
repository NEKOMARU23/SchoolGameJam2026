#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.UI;
using TechC.SchoolGameJam2026.Scripts.UI.Dialogue;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 文字送りのオート・スキップと、既読の記録のテスト
    /// </summary>
    public class AutoSkipTest
    {
        private string backupSettingsJson;
        private Core.Manager.GameSettings originalSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            backupSettingsJson = File.Exists(Core.Manager.SettingsManager.FilePath) ? File.ReadAllText(Core.Manager.SettingsManager.FilePath) : null;
            yield return LoadInGameScene();
            originalSettings = Core.Manager.SettingsManager.Instance.Current;
            Core.Manager.SettingsManager.Instance.SetAutoWait(Core.Manager.AutoWait.Short);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 設定を元に戻す（Manager シーンはテストのあいだ残るので、ファイルとメモリの両方を戻す）
            Core.Manager.SettingsManager.Instance.SetAutoWait(originalSettings.autoWait);
            Core.Manager.SettingsManager.Instance.SetSkipMode(originalSettings.skipMode);
            if (backupSettingsJson != null) File.WriteAllText(Core.Manager.SettingsManager.FilePath, backupSettingsJson);
            else if (File.Exists(Core.Manager.SettingsManager.FilePath)) File.Delete(Core.Manager.SettingsManager.FilePath);
            yield return null;
        }

        [Test]
        public void 既読の記録はファイルに保存され読み込み直しても残る()
        {
            UseTestEndingRecord();
            string path = ReadHistoryStore.FilePath;

            Assert.IsFalse(ReadHistoryStore.IsRead("A_D1_対話_1"));
            ReadHistoryStore.MarkRead("A_D1_対話_1");
            Assert.IsTrue(File.Exists(path));

            ReadHistoryStore.SetFilePathForTest(path);
            Assert.IsTrue(ReadHistoryStore.IsRead("A_D1_対話_1"));
            Assert.IsFalse(ReadHistoryStore.IsRead("A_D1_対話_2"));
        }

        [UnityTest]
        public IEnumerator オートボタンで取調室とスマホの両方がオートになる()
        {
            yield return OpenRoom("A");
            yield return Click("AutoButton", "Interrogation_Chatbox");

            Assert.IsTrue(FindComponent<GameUIController>().IsAutoOn);
            Assert.IsTrue(FindComponent<DialogueUI>().IsAuto, "取調室の会話がオートになっていません");
            Assert.IsTrue(FindComponent<DialogueClickable>().IsAuto, "スマホの会話がオートになっていません");

            yield return Click("AutoButton", "Interrogation_Chatbox");
            Assert.IsFalse(FindComponent<DialogueUI>().IsAuto, "もう一度押してもオートが切れません");
        }

        [UnityTest]
        public IEnumerator オートなら押さなくても最後のページまで進む()
        {
            yield return OpenRoom("A");
            DialogueUI dialogue = FindComponent<DialogueUI>();
            dialogue.SetTextSpeed(0f);
            dialogue.SetAuto(true);
            dialogue.StartDialogue("テスト", ThreePages());

            yield return WaitWhile(() => dialogue.IsDialoguePlaying, 5f);
            Assert.IsFalse(dialogue.IsDialoguePlaying, "オートで会話が最後まで進みません");
        }

        [UnityTest]
        public IEnumerator スキップは既読の会話を飛ばし未読の会話で止まる()
        {
            yield return OpenRoom("A");
            DialogueUI dialogue = FindComponent<DialogueUI>();
            bool stopped = false;
            dialogue.OnSkipStopped += () => stopped = true;

            // 既読（スキップしてよい）会話は、すぐに最後まで進む
            dialogue.SetSkip(true);
            dialogue.StartDialogue("テスト", ThreePages(), skippable: true);
            yield return WaitWhile(() => dialogue.IsDialoguePlaying, 2f);
            Assert.IsFalse(dialogue.IsDialoguePlaying, "既読の会話がスキップされません");

            // 未読の会話では止まって、プレイヤーに読ませる
            dialogue.StartDialogue("テスト", ThreePages(), skippable: false);
            yield return null;
            yield return null;
            Assert.IsTrue(stopped, "未読の会話でスキップが止まりません");
            Assert.IsFalse(dialogue.IsSkipping);
            Assert.IsTrue(dialogue.IsDialoguePlaying, "未読の会話が飛ばされました");
        }

        [UnityTest]
        public IEnumerator 既読のみのとき未読の会話でスキップボタンが切れる()
        {
            Core.Manager.SettingsManager.Instance.SetSkipMode(Core.Manager.SkipMode.ReadOnly);
            var controller = FindComponent<GameUIController>();
            yield return OpenRoom("A");
            yield return Click("SkipButton", "Interrogation_Chatbox");
            Assert.IsTrue(controller.IsSkipOn);

            // 初めて聞く会話（未読）なので、スキップが切れて会話が残る
            controller.ExecuteInterrogationAction((int)InterrogationActionType.Talk);
            yield return null;
            yield return null;
            Assert.IsFalse(controller.IsSkipOn, "未読の会話なのにスキップが切れません");
            Assert.IsTrue(FindComponent<DialogueUI>().IsDialoguePlaying, "未読の会話が飛ばされました");
        }

        [UnityTest]
        public IEnumerator すべてのときは未読の会話も飛ばし最後まで読んだ会話は既読になる()
        {
            Core.Manager.SettingsManager.Instance.SetSkipMode(Core.Manager.SkipMode.All);
            var controller = FindComponent<GameUIController>();
            yield return OpenRoom("A");
            yield return Click("SkipButton", "Interrogation_Chatbox");

            controller.ExecuteInterrogationAction((int)InterrogationActionType.Talk);
            DialogueUI dialogue = FindComponent<DialogueUI>();
            yield return WaitWhile(() => dialogue.IsDialoguePlaying, 5f);
            Assert.IsFalse(dialogue.IsDialoguePlaying, "「すべて」なのに未読の会話が飛ばされません");
            Assert.IsTrue(controller.IsSkipOn, "スキップが途中で切れました");

            // 出た会話が既読として記録されている
            bool anyRead = false;
            foreach (ScenarioConditionRow row in GameManager.Scenario.GetConditionRows(1, "A", ScenarioAction.Talk))
            {
                if (ReadHistoryStore.IsRead(row.dialogueId)) anyRead = true;
            }
            Assert.IsTrue(anyRead, "最後まで読んだ会話が既読になっていません");
        }

        [UnityTest]
        public IEnumerator スマホの会話もオートで最後まで進んでモニターに戻る()
        {
            var controller = FindComponent<GameUIController>();
            controller.ToggleAuto();
            DialogueClickable talk = FindComponent<DialogueClickable>();
            talk.SetCharacterDisplayInterval(0f);

            yield return Click("Button_Help", "MonitorPanel");
            Assert.IsTrue(talk.IsOpen);
            yield return WaitWhile(() => talk.IsOpen, 5f);

            Assert.IsFalse(talk.IsOpen, "オートでスマホの会話が終わりません");
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "会話が終わってもモニターに戻りません");
        }

        #region ヘルパー

        private static IEnumerator OpenRoom(string characterId)
        {
            FindComponent<GameUIController>().SelectCharacter(characterId);
            yield return null;
        }

        private static List<DialoguePage> ThreePages()
        {
            return new List<DialoguePage>
            {
                new DialoguePage(false, null, "1ページ目"),
                new DialoguePage(false, null, "2ページ目"),
                new DialoguePage(false, null, "3ページ目"),
            };
        }

        private static IEnumerator WaitWhile(System.Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        #endregion
    }
}
#endif
