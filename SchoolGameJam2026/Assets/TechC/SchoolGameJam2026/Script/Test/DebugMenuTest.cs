#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.DebugTools;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// デバッグメニュー（F1）で使う処理のテスト
    /// 会話の選ばれ方の記録・エンディングの再生・記録の削除
    /// </summary>
    public class DebugMenuTest
    {
        private string saveFolder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
            saveFolder = Path.Combine(Application.temporaryCachePath, "test_debug_saves");
            if (Directory.Exists(saveFolder)) Directory.Delete(saveFolder, true);
            SaveStore.SetFolderForTest(saveFolder);
        }

        [TearDown]
        public void TearDown()
        {
            SaveStore.SetFolderForTest(null);
        }

        #region 会話の選ばれ方

        [Test]
        public void 取り調べで会話を選ぶとどの行が選ばれたかと候補の行が記録される()
        {
            Assert.IsNull(GameManager.LastSelection, "前提: まだ会話をしていない");
            ScenarioDatabase db = GameManager.Scenario;
            var rows = db.GetConditionRows(1, "A", ScenarioAction.Talk);
            ScenarioConditionRow expected = ScenarioSelector.Select(rows, GameManager.CreateSelectorContext(GetCharacter("A"), 0));

            InterrogationManager.ExecuteAction(GetCharacter("A"), InterrogationActionType.Talk);

            Core.Manager.ScenarioSelectionRecord selection = GameManager.LastSelection;
            Assert.IsNotNull(selection, "会話を選んだ記録が残っていません");
            Assert.AreEqual(1, selection.Day);
            StringAssert.Contains("対話", selection.Scene);
            Assert.AreSame(expected, selection.Chosen, "選ばれた行の記録が、実際に選ばれた行と違います");
            Assert.AreEqual(rows.Count, selection.Candidates.Count, "候補の行がすべて記録されていません");
            Assert.IsTrue(selection.Candidates.Find(c => c.Row == expected).Satisfied, "選ばれた行が「条件を満たす」になっていません");
        }

        [Test]
        public void スマホの会話でも選ばれ方が記録される()
        {
            Assert.IsTrue(Core.Manager.SmartphoneTalkManager.Instance.TryStartTalk(out _), "前提: スマホで話せる");
            StringAssert.Contains("スマホ 1回目", GameManager.LastSelection.Scene);
            Assert.IsNotNull(GameManager.LastSelection.Chosen);
        }

        [Test]
        public void 条件の説明が日本語で出る()
        {
            var row = new ScenarioConditionRow
            {
                hasSelfAffection = true, selfAffection = 2,
                otherAffections = { new AffectionCondition { characterId = "B", minValue = 1 } },
                hasActionCount = true, actionCount = 1,
                requiredFlags = { "info_x" }, ngFlags = { "flag_y" },
            };
            Assert.AreEqual("本人の好感度 2以上・B の好感度 1以上・この行動 1回以上・info_x が ON・flag_y が OFF", DebugMenu.DescribeConditions(row));
            Assert.AreEqual("条件なし", DebugMenu.DescribeConditions(new ScenarioConditionRow()));
        }

        #endregion

        #region エンディングの再生

        [UnityTest]
        public IEnumerator エンディングをすぐ再生でき見たことには記録されない()
        {
            ScenarioDatabase db = GameManager.Scenario;
            Assert.IsNotEmpty(db.endings, "前提: エンディングがある");
            ScenarioEnding ending = db.endings[0];

            Assert.IsTrue(DebugMenu.PlayEnding(ending, db), "エンディングを再生できません");
            var player = FindComponent<EndingPlayerUI>();
            Assert.IsTrue(player.IsPlaying);
            Assert.IsTrue(player.gameObject.activeInHierarchy, "エンディングの画面が表示されていません");

            for (int i = 0; i < 100 && player.IsPlaying; i++)
            {
                player.Advance();
                yield return null;
            }
            Assert.IsFalse(player.IsPlaying, "エンディングが最後まで進みません");
            Assert.IsFalse(EndingRecordStore.IsSeen(ending.endingId), "デバッグで再生したエンディングが、見たことになっています");
            Assert.AreEqual(InGameSceneName, SceneManager.GetActiveScene().name, "再生したあと、ゲームに戻っていません");
        }

        #endregion

        #region 記録を消す

        [Test]
        public void セーブをすべて消せる()
        {
            Assert.IsTrue(SaveStore.Save(1, new SaveData { current = new GameStateSnapshot() }));
            Assert.IsTrue(SaveStore.Save(20, new SaveData { current = new GameStateSnapshot() }));

            SaveStore.DeleteAll();
            Assert.IsFalse(SaveStore.Exists(1));
            Assert.IsFalse(SaveStore.Exists(20));
        }

        [Test]
        public void 見たエンディングと既読と手に入れた情報の記録を消せる()
        {
            EndingRecordStore.MarkSeen("END_テスト");
            ReadHistoryStore.MarkRead("T_既読");
            InfoRecordStore.RecordAcquired("info_テスト", 1, 1, "テスト");
            InfoRecordStore.CompletePlaythrough(2);
            Assert.IsTrue(File.Exists(EndingRecordStore.FilePath) && File.Exists(ReadHistoryStore.FilePath) && File.Exists(InfoRecordStore.FilePath), "前提: 記録のファイルがある");

            EndingRecordStore.ClearAll();
            ReadHistoryStore.ClearAll();
            InfoRecordStore.ClearAll();

            Assert.IsFalse(EndingRecordStore.IsSeen("END_テスト"));
            Assert.IsFalse(ReadHistoryStore.IsRead("T_既読"));
            Assert.IsFalse(InfoRecordStore.TryGet("info_テスト", out _));
            Assert.AreEqual(0, InfoRecordStore.CompletedPlaythroughs, "周回の数が消えていません");
            Assert.IsFalse(File.Exists(EndingRecordStore.FilePath), "見たエンディングのファイルが残っています");
            Assert.IsFalse(File.Exists(ReadHistoryStore.FilePath), "既読のファイルが残っています");
            Assert.IsFalse(File.Exists(InfoRecordStore.FilePath), "手に入れた情報のファイルが残っています");
        }

        #endregion
    }
}
#endif
