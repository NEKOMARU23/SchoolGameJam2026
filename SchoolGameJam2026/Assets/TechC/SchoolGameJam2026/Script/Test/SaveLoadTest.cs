#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// セーブ・ロード（20枠・手動セーブ）のテスト
    /// セーブは本物の保存先ではなく、テスト用のフォルダに書く
    /// </summary>
    public class SaveLoadTest
    {
        private const string SlotDialogPath = "SaveSlotPanel/ConfirmDialog";

        [SetUp]
        public void SetUp()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "test_saves");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            SaveStore.SetFolderForTest(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (OptionUI.IsOpen) OptionUI.Instance.Close();
            SaveStore.SetFolderForTest(null);
        }

        #region セーブ枠のファイル

        [Test]
        public void セーブ枠に保存して読み込み削除できる()
        {
            Assert.AreEqual(20, SaveStore.SlotCount);
            Assert.IsFalse(SaveStore.Exists(1));
            Assert.IsNull(SaveStore.Load(1));

            var data = new SaveData { day = 3, remainingActions = 2, current = new GameStateSnapshot { day = 3, points = 120 } };
            Assert.IsTrue(SaveStore.Save(1, data));
            Assert.AreEqual(1, SaveStore.CurrentSlot, "セーブした枠が今のゲームの枠になっていません");

            SaveData loaded = SaveStore.Load(1);
            Assert.AreEqual(3, loaded.day);
            Assert.AreEqual(120, loaded.current.points);
            Assert.IsNotEmpty(loaded.savedAt, "保存した日時が入っていません");

            Assert.IsFalse(SaveStore.Save(0, data), "0番には保存できない");
            Assert.IsFalse(SaveStore.Save(21, data), "21番には保存できない");

            SaveStore.Delete(1);
            Assert.IsFalse(SaveStore.Exists(1));
            Assert.AreEqual(0, SaveStore.CurrentSlot);
        }

        #endregion

        #region ゲーム中

        [UnityTest]
        public IEnumerator ゲーム中にセーブしてロードするとその時点に戻り朝にもやり直せる()
        {
            yield return LoadInGameScene();
            Assert.IsTrue(GameManager.JumpToDay(2));
            yield return null;

            // セーブする時点: 2日目、A と対話済み、フラグ ON
            yield return TalkTo("A");
            GameManager.Flags.TurnOn("info_セーブ前の情報");
            int pointsAtSave = InterrogationManager.CurrentPoints;

            yield return OpenOption();
            yield return Click("SaveButton");
            Assert.IsTrue(FindComponent<SaveLoadController>().IsOpen, "セーブの枠が開きません");
            yield return Click("SaveSlot_03");
            Assert.IsTrue(SaveStore.Exists(3), "3番にセーブされていません");
            StringAssert.Contains("3番にセーブしました", FindComponent<SaveSlotUI>().Notice);
            StringAssert.Contains("2日目", FindComponent<SaveSlotUI>().ShownLabels[2], "枠に日数が表示されていません");
            OptionUI.Instance.Close();
            yield return null;

            // セーブの後に進める
            yield return TalkTo("B");
            GameManager.Flags.TurnOn("info_セーブ後の情報");
            InterrogationManager.SetPoints(pointsAtSave + 500);

            // ロードは確認してから
            yield return OpenOption();
            yield return Click("LoadButton");
            yield return Click("SaveSlot_03");
            Assert.IsTrue(InterrogationManager.HasInterrogatedToday("B"), "確認の前にロードされました");
            yield return Click("YesButton", SlotDialogPath);
            yield return null;

            Assert.AreEqual(2, GameManager.CurrentDay);
            Assert.AreEqual(1, InterrogationManager.CurrentDailyActions, "行動回数がセーブした時点に戻っていません");
            Assert.IsTrue(InterrogationManager.HasInterrogatedToday("A"));
            Assert.IsFalse(InterrogationManager.HasInterrogatedToday("B"), "セーブの後の行動が残っています");
            Assert.AreEqual(pointsAtSave, InterrogationManager.CurrentPoints);
            Assert.IsTrue(GameManager.Flags.IsOn("info_セーブ前の情報"));
            Assert.IsFalse(GameManager.Flags.IsOn("info_セーブ後の情報"), "セーブの後のフラグが残っています");
            Assert.IsFalse(OptionUI.IsOpen, "ロードの後もオプションが開いたままです");
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "ロードの後にモニター画面に戻っていません");

            // ロードの後も「今日をやり直す」で2日目の朝に戻れる
            FindComponent<GameUIController>().OnConfirmResetDay();
            yield return null;
            Assert.AreEqual(2, GameManager.CurrentDay);
            Assert.AreEqual(0, InterrogationManager.CurrentDailyActions, "ロードの後に朝の状態に戻れません");
        }

        [UnityTest]
        public IEnumerator 会話の途中はセーブできない()
        {
            yield return LoadInGameScene();
            FindComponent<GameUIController>().SelectCharacter("A");
            yield return null;
            yield return Click("Button_Talk");
            Assert.IsTrue(FindComponent<DialogueUI>().IsDialoguePlaying, "前提: 会話の途中");

            yield return OpenOption();
            yield return Click("SaveButton");
            yield return Click("SaveSlot_01");

            Assert.IsFalse(SaveStore.Exists(1), "会話の途中なのにセーブできました");
            StringAssert.Contains("会話の途中", FindComponent<SaveSlotUI>().Notice);
        }

        [UnityTest]
        public IEnumerator 上書きと削除は確認してから行う()
        {
            yield return LoadInGameScene();
            yield return OpenOption();
            yield return Click("SaveButton");
            yield return Click("SaveSlot_02");
            string firstSavedAt = SaveStore.Load(2).savedAt;
            Assert.IsTrue(SaveStore.Exists(2));

            // 上書き: 「いいえ」なら上書きしない
            GameManager.Flags.TurnOn("info_上書き後");
            yield return Click("SaveSlot_02");
            Assert.IsTrue(IsConfirmDialogOpen(SlotDialogPath), "上書きの確認が出ません");
            yield return Click("NoButton", SlotDialogPath);
            Assert.IsFalse(SaveStore.Load(2).current.flags.Contains("info_上書き後"), "「いいえ」なのに上書きされました");

            yield return Click("SaveSlot_02");
            yield return Click("YesButton", SlotDialogPath);
            Assert.IsTrue(SaveStore.Load(2).current.flags.Contains("info_上書き後"), "「はい」で上書きされません");

            // 削除
            yield return Click("DeleteButton", "SaveSlot_02");
            Assert.IsTrue(SaveStore.Exists(2), "確認の前に消えました");
            yield return Click("YesButton", SlotDialogPath);
            Assert.IsFalse(SaveStore.Exists(2), "削除されません");
            StringAssert.Contains("（空き）", FindComponent<SaveSlotUI>().ShownLabels[1]);
            Assert.IsNotEmpty(firstSavedAt);
        }

        #endregion

        #region タイトル・クリア画面

        [UnityTest]
        public IEnumerator タイトルのつづきからでセーブした続きから始まる()
        {
            // 4日目・フラグありのセーブを作っておく
            yield return LoadInGameScene();
            Assert.IsTrue(GameManager.JumpToDay(4));
            yield return null;
            GameManager.Flags.TurnOn("info_つづきから");
            Assert.IsTrue(SaveStore.Save(5, FindComponent<GameUIController>().CreateSaveData()));

            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;

            // タイトルから開いたオプションには、セーブ・ロードのボタンは出さない
            OptionUI.Instance.Open();
            yield return null;
            Assert.IsFalse(FindButton("SaveButton").gameObject.activeInHierarchy, "タイトルのオプションにセーブボタンが出ています");
            OptionUI.Instance.Close();

            yield return Click("ContinueButton");
            SaveSlotUI slots = FindComponent<SaveSlotUI>();
            Assert.IsTrue(slots.IsOpen, "「つづきから」でロードの枠が開きません");
            Assert.IsFalse(FindButton("SaveSlot_01").interactable, "空き枠を選べます");
            StringAssert.Contains("4日目", slots.ShownLabels[4]);

            yield return Click("SaveSlot_05");
            yield return WaitForScene(InGameSceneName);
            yield return null;
            yield return null;

            Assert.AreEqual(4, GameManager.CurrentDay, "セーブした日から始まっていません");
            Assert.IsTrue(GameManager.Flags.IsOn("info_つづきから"));
            Assert.AreEqual(5, SaveStore.CurrentSlot);
        }

        [UnityTest]
        public IEnumerator クリア画面で今回のセーブを消すか選べる()
        {
            SaveStore.Save(7, new SaveData { current = new GameStateSnapshot() });
            yield return SceneManager.LoadSceneAsync(ClearSceneName, LoadSceneMode.Single);
            yield return null;

            SaveDeletePromptUI prompt = FindComponent<SaveDeletePromptUI>();
            Assert.IsTrue(prompt.IsOpen, "セーブを消すか聞かれません");
            yield return Click("YesButton", "SaveDeletePrompt");
            Assert.IsFalse(SaveStore.Exists(7), "「消す」でセーブが消えません");
            Assert.IsFalse(prompt.IsOpen);
        }

        [UnityTest]
        public IEnumerator セーブしていないゲームなら消すか聞かない()
        {
            SaveStore.CurrentSlot = 0;
            yield return SceneManager.LoadSceneAsync(GameOverSceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsFalse(FindComponent<SaveDeletePromptUI>().IsOpen);
        }

        #endregion

        #region ヘルパー

        private static IEnumerator TalkTo(string characterId)
        {
            var controller = FindComponent<GameUIController>();
            controller.SelectCharacter(characterId);
            yield return null;
            controller.ExecuteInterrogationAction((int)InterrogationActionType.Talk);
            yield return SkipDialogue();

            var choice = FindComponent<ChoiceUI>();
            if (choice.IsOpen) choice.Select(0);
            yield return SkipDialogue();
        }

        private static IEnumerator OpenOption()
        {
            OptionUI.Instance.Open();
            yield return null;
            Assert.IsTrue(OptionUI.IsOpen);
        }

        #endregion
    }
}
#endif
