#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 資料画面に出す「手に入れた情報」の記録（手に入れた日・手に入れ方・周回、周回をまたぐ記録、ポイントでの解禁）のテスト
    /// シナリオのデータに、テスト用の情報を一時的に足して確かめる
    /// </summary>
    public class InfoRecordTest
    {
        private const string TalkInfo = "info_テスト用_会話の情報";
        private const string PointInfo = "info_テスト用_ポイントの情報";

        private readonly List<ScenarioFlagDefinition> added = new List<ScenarioFlagDefinition>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
            AddFlag(new ScenarioFlagDefinition { name = TalkInfo, kind = FlagKind.Info, displayTarget = "A", title = "テスト用の会話の情報", profileText = "会話で手に入る" });
            AddFlag(new ScenarioFlagDefinition { name = PointInfo, kind = FlagKind.Info, displayTarget = "その他", title = "テスト用のポイントの情報", profileText = "ポイントで解禁する", unlockCost = 30 });
        }

        [TearDown]
        public void TearDown()
        {
            if (GameManager != null && GameManager.Scenario != null)
            {
                foreach (ScenarioFlagDefinition f in added) GameManager.Scenario.flags.Remove(f);
            }
            added.Clear();
        }

        private void AddFlag(ScenarioFlagDefinition def)
        {
            GameManager.Scenario.flags.Add(def);
            added.Add(def);
        }

        private static Core.Manager.InfoEntry Entry(string target, string flag)
        {
            Core.Manager.InfoEntry entry = Core.Manager.InfoCatalog.GetEntries(GameManager, target).Find(e => e.FlagName == flag);
            Assert.IsNotNull(entry, $"{target} のタブに {flag} がありません");
            return entry;
        }

        private static void GainByTalk(string method = "ココアに対話")
        {
            GameManager.ApplyScenarioEffects(new ScenarioConditionRow { gainFlags = { TalkInfo } }, method);
        }

        [UnityTest]
        public IEnumerator 会話で手に入れると日と手に入れ方が記録されてNEWと赤い丸が出る()
        {
            Core.Manager.InfoEntry before = Entry("A", TalkInfo);
            Assert.IsFalse(before.IsKnown, "前提: まだ手に入れていない");

            GainByTalk();
            yield return null;

            Core.Manager.InfoEntry entry = Entry("A", TalkInfo);
            Assert.IsTrue(entry.IsOwned && entry.IsKnown, "手に入れた情報になっていません");
            Assert.AreEqual(1, entry.AcquiredDay);
            Assert.AreEqual("ココアに対話", entry.AcquiredMethod);
            Assert.AreEqual(1, entry.AcquiredPlaythrough);
            Assert.IsTrue(entry.IsNew, "初めて手に入れたのに NEW になりません");
            Assert.AreEqual("テスト用の会話の情報", entry.Title);

            Core.Manager.InfoCatalog.Count(GameManager, "A", out int known, out int total);
            Assert.AreEqual(1, known, "「集めた数」が増えていません");
            Assert.GreaterOrEqual(total, 1);

            Assert.IsTrue(Core.Manager.InfoCatalog.HasUnseen(GameManager, "A"), "まだ見ていないのにタブの赤い丸が出ません");
            Core.Manager.InfoCatalog.MarkSeen(GameManager, "A");
            Assert.IsFalse(Core.Manager.InfoCatalog.HasUnseen(GameManager, "A"), "見たのにタブの赤い丸が消えません");
        }

        [UnityTest]
        public IEnumerator 今日をやり直すと持っていない状態に戻るが前に手に入れた情報として残る()
        {
            GainByTalk();
            GameManager.ResetCurrentDay();
            yield return null;

            Core.Manager.InfoEntry entry = Entry("A", TalkInfo);
            Assert.IsFalse(entry.IsOwned, "今日をやり直してもフラグが残っています");
            Assert.IsTrue(entry.IsKnown, "今日をやり直したら、一度手に入れた記録まで消えました");
            Assert.AreEqual(0, GameManager.Acquisitions.Count, "今日をやり直しても、この周回の記録が残っています");
        }

        [UnityTest]
        public IEnumerator ポイントで解禁する情報はポイントが足りるときだけ解禁できる()
        {
            var interrogation = InterrogationManager;
            interrogation.ConsumePoints(interrogation.CurrentPoints);
            interrogation.AddPoints(20);
            Assert.IsFalse(interrogation.CanUnlockInfo(PointInfo, out int cost), "ポイントが足りないのに解禁できます");
            Assert.AreEqual(30, cost);
            Assert.IsFalse(interrogation.TryUnlockInfo(PointInfo));
            Assert.AreEqual(20, interrogation.CurrentPoints, "解禁できなかったのにポイントが減りました");

            interrogation.AddPoints(20);
            Assert.IsTrue(interrogation.TryUnlockInfo(PointInfo), "ポイントが足りるのに解禁できません");
            Assert.AreEqual(10, interrogation.CurrentPoints, "解禁のポイントが減っていません");
            Assert.IsTrue(GameManager.Flags.IsOn(PointInfo));
            Assert.AreEqual("30pt で解禁", Entry("その他", PointInfo).AcquiredMethod);

            Assert.IsFalse(interrogation.TryUnlockInfo(PointInfo), "解禁済みなのにもう一度解禁できます");
            Assert.IsFalse(interrogation.TryUnlockInfo(TalkInfo), "会話で手に入る情報をポイントで解禁できます");
            yield break;
        }

        [UnityTest]
        public IEnumerator 次の周回では資料画面で読めるがフラグはOFFのまま()
        {
            GainByTalk();
            InfoRecordStore.CompletePlaythrough(GameManager.CurrentPlaythrough);   // エンディングまで行った

            // 記録を消さずに、はじめから始め直す
            yield return SceneManager.LoadSceneAsync(InGameSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.AreEqual(2, GameManager.CurrentPlaythrough, "エンディングまで行ったのに2周目になりません");
            Assert.IsFalse(GameManager.Flags.IsOn(TalkInfo), "前の周回のフラグが引き継がれています");

            Core.Manager.InfoEntry entry = Entry("A", TalkInfo);
            Assert.IsTrue(entry.IsKnown, "前の周回で手に入れた情報が資料画面で読めません");
            Assert.IsFalse(entry.IsOwned);
            Assert.AreEqual(1, entry.AcquiredPlaythrough, "前の周回で手に入れたことが分かりません");

            GainByTalk("メリから聞き取り");
            entry = Entry("A", TalkInfo);
            Assert.IsFalse(entry.IsNew, "前の周回でも手に入れた情報が NEW になっています");
            Assert.AreEqual(2, entry.AcquiredPlaythrough, "この周回で手に入れ直したのに、前の周回の記録が出ています");
            Assert.AreEqual("メリから聞き取り", entry.AcquiredMethod);
        }

        [UnityTest]
        public IEnumerator セーブとロードで手に入れた記録と周回が戻る()
        {
            GainByTalk();
            SaveData save = GameManager.CreateSaveData(new List<LogEntry>());
            GameManager.ResetCurrentDay();
            Assert.AreEqual(0, GameManager.Acquisitions.Count, "前提: 今日をやり直すと記録が消える");

            GameManager.ApplySave(save);
            yield return null;

            Assert.AreEqual(1, GameManager.Acquisitions.Count, "ロードしても手に入れた記録が戻りません");
            Assert.AreEqual("ココアに対話", GameManager.Acquisitions[0].method);
            Assert.AreEqual(1, GameManager.CurrentPlaythrough);
            Assert.IsTrue(Entry("A", TalkInfo).IsOwned);
        }
    }
}
#endif
