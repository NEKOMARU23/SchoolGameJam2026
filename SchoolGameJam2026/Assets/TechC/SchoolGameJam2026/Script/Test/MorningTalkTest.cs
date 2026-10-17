#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 朝の会話（その日の始めに自動で流れるスマホの会話）と、最終日のモニターの札のテスト
    /// </summary>
    public class MorningTalkTest
    {
        private const string TestFlag = "flag_テスト用_朝の会話";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
        }

        #region 朝の会話

        [UnityTest]
        public IEnumerator 日が変わると朝の会話が自動で開きスマホの回数は減らない()
        {
            yield return WithMorningRow(2, () => AdvanceDayAndWaitMorningTalk(2));

            Assert.AreEqual(0, Core.Manager.SmartphoneTalkManager.Instance.TalkCountToday, "朝の会話でスマホの回数が減りました");
            Assert.IsTrue(GameManager.Flags.IsOn(TestFlag), "朝の会話の獲得フラグが ON になっていません");

            yield return CloseMorningTalk();
        }

        [UnityTest]
        public IEnumerator 今日をやり直しても朝の会話は流れず効果は残る()
        {
            yield return WithMorningRow(2, () => AdvanceDayAndWaitMorningTalk(2));
            yield return CloseMorningTalk();

            yield return Click("Reset_Button", "MonitorPanel");
            yield return Click("YesButton", "ResetConfirmDialog");
            yield return null;

            Assert.IsFalse(IsPanelActive("SmartphoneTalkPanel"), "今日をやり直したら朝の会話がもう一度流れました");
            Assert.IsNull(GameManager.TakeMorningTalk(), "今日をやり直したら朝の会話が用意されました");
            Assert.IsTrue(GameManager.Flags.IsOn(TestFlag), "今日をやり直したら朝の会話の獲得フラグが消えました");
        }

        [UnityTest]
        public IEnumerator 朝の会話がない日は何も開かない()
        {
            yield return EndDayByButton();
            yield return WaitUntil(() => GameManager.CurrentDay == 2 && !FindComponent<DayTransitionUI>().IsPlaying);

            Assert.IsTrue(IsPanelActive("MonitorPanel"), "朝の会話がない日なのにモニター画面に戻りません");
            Assert.IsFalse(IsPanelActive("SmartphoneTalkPanel"), "朝の会話がない日なのにスマホの会話が開きました");
        }

        #endregion

        #region 最終日のモニター

        [UnityTest]
        public IEnumerator 最終日のモニターに未判定と判定結果が出る()
        {
            AssertJudgementLabels(null, "1日目");

            Assert.IsTrue(GameManager.JumpToDay(GameManager.MaxDays), "最終日に移動できません");
            yield return null;
            if (IsPanelActive("SmartphoneTalkPanel")) yield return CloseMorningTalk();

            AssertJudgementLabels(new[] { "未判定", "未判定", "未判定", "未判定" }, "最終日の朝");

            yield return JudgeOnFinalDay("D", confirmAI: true);
            yield return JudgeOnFinalDay("A", confirmAI: false);
            FindComponent<ScreenNavigationUI>().ShowMonitorPanel();
            yield return null;

            AssertJudgementLabels(new[] { "解放", "未判定", "未判定", "AI判定" }, "2人を判定したあと");
        }

        /// <param name="expected">A〜D の順に、出ているはずの札（null なら全員なし）</param>
        private static void AssertJudgementLabels(string[] expected, string when)
        {
            Transform grid = FindComponent<MonitorUI>().transform.Find("MonitorGrid");
            string[] ids = { "A", "B", "C", "D" };
            for (int i = 0; i < ids.Length; i++)
            {
                Transform room = grid.Find("MonitorRoom_" + ids[i]);
                GameObject unjudged = room.Find("UnjudgedLabel").gameObject;
                GameObject judged = room.Find("JudgedLabel").gameObject;
                string label = expected?[i];

                Assert.AreEqual(label == "未判定", unjudged.activeSelf, $"{when}: 部屋 {ids[i]} の「未判定」の表示が違います");
                bool isJudged = label == "AI判定" || label == "解放";
                Assert.AreEqual(isJudged, judged.activeSelf, $"{when}: 部屋 {ids[i]} の判定結果の表示が違います");
                if (isJudged) Assert.AreEqual(label, judged.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text);
            }
        }

        #endregion

        #region 補助

        /// <summary>
        /// シナリオのデータに、その日の「朝」の行（獲得フラグつき）を一時的に足してテストし、終わったら元に戻す
        /// </summary>
        private static IEnumerator WithMorningRow(int day, Func<IEnumerator> body)
        {
            var row = new ScenarioConditionRow
            {
                day = day,
                action = ScenarioAction.Morning,
                priority = 1,
                dialogueId = "MORNING_D7",
                gainFlags = { TestFlag },
            };
            Assert.IsNotNull(GameManager.Scenario.FindDialogue(row.dialogueId), "前提: 朝の会話の本文がある");

            GameManager.Scenario.conditions.Add(row);
            try
            {
                IEnumerator e = body();
                while (e.MoveNext()) yield return e.Current;
            }
            finally
            {
                GameManager.Scenario.conditions.Remove(row);
            }
        }

        private static IEnumerator AdvanceDayAndWaitMorningTalk(int day)
        {
            yield return EndDayByButton();
            yield return WaitUntil(() => GameManager.CurrentDay == day && IsPanelActive("SmartphoneTalkPanel"));
            Assert.IsFalse(IsPanelActive("MonitorPanel"), "朝の会話の間にモニター画面が出ています");
        }

        /// <summary> 朝の会話を閉じ、日が変わる演出でモニター画面に戻るのを待つ </summary>
        private static IEnumerator CloseMorningTalk()
        {
            yield return Click("CloseButton", "SmartphoneTalkPanel");
            yield return WaitUntil(() => IsPanelActive("MonitorPanel") && !FindComponent<DayTransitionUI>().IsPlaying);
            Assert.IsFalse(IsPanelActive("SmartphoneTalkPanel"), "朝の会話を閉じてもスマホの会話画面が残っています");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, int maxFrames = 300)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail("待っても条件を満たしませんでした");
        }

        #endregion
    }
}
#endif
