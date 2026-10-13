#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// AI になる仕組み（AI判定シート）と、最終日の判定（解放・AI確定）・確認ダイアログのテスト
    /// </summary>
    public class JudgementTest
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
        }

        #region 最終日のボタンと判定

        [UnityTest]
        public IEnumerator 最終日だけ判定のボタンが出て取り調べのボタンは出ない()
        {
            var controller = FindComponent<GameUIController>();

            controller.SelectCharacter("A");
            yield return null;
            Assert.IsTrue(FindButton("Button_Talk").gameObject.activeSelf, "1日目に取り調べのボタンが出ていません");
            Assert.IsFalse(FindButton(ReleaseButtonName).gameObject.activeSelf, "1日目に解放ボタンが出ています");
            Assert.IsFalse(FindButton(ConfirmAIButtonName).gameObject.activeSelf, "1日目に AI確定ボタンが出ています");

            yield return GoToFinalDay();
            controller.SelectCharacter("A");
            yield return null;
            foreach (string name in new[] { "Button_Torture", "Button_Talk", "Button_Negotiate", "Button_HearAbout" })
            {
                Assert.IsFalse(FindButton(name).gameObject.activeSelf, $"最終日に {name} が出ています");
            }
            Assert.IsTrue(FindButton(ReleaseButtonName).interactable, "最終日に解放ボタンが押せません");
            Assert.IsTrue(FindButton(ConfirmAIButtonName).interactable, "最終日に AI確定ボタンが押せません");
        }

        [UnityTest]
        public IEnumerator 解放は確認でいいえを押すと取り消せてはいで行動を1回使う()
        {
            yield return GoToFinalDay();
            FindComponent<GameUIController>().SelectCharacter("B");
            yield return null;

            yield return Click(ReleaseButtonName);
            Assert.IsTrue(IsConfirmDialogOpen(ConfirmDialogPath), "解放の確認が出ません");
            StringAssert.Contains("解放しますか", ConfirmMessage());

            yield return Click("NoButton", ConfirmDialogPath);
            Assert.IsFalse(IsConfirmDialogOpen(ConfirmDialogPath), "「いいえ」で確認が閉じません");
            Assert.IsFalse(GetCharacter("B").IsReleased, "「いいえ」なのに解放されました");
            Assert.AreEqual(0, InterrogationManager.CurrentDailyActions);

            yield return Click(ReleaseButtonName);
            yield return ClickConfirmYes();
            yield return SkipDialogue();
            Assert.IsTrue(GetCharacter("B").IsReleased, "「はい」で解放されません");
            Assert.AreEqual(1, InterrogationManager.CurrentDailyActions, "解放で行動回数を使っていません");
        }

        [UnityTest]
        public IEnumerator AI確定は確認してから行い判定した人の部屋には入れない()
        {
            yield return GoToFinalDay();
            FindComponent<GameUIController>().SelectCharacter("C");
            yield return null;

            yield return Click(ConfirmAIButtonName);
            StringAssert.Contains("AI と判定しますか", ConfirmMessage());
            yield return ClickConfirmYes();
            yield return SkipDialogue();

            Assert.IsTrue(GetCharacter("C").IsConfirmedAI);
            Assert.AreEqual(1, InterrogationManager.CurrentDailyActions, "AI確定で行動回数を使っていません");
            Assert.IsFalse(FindButton(ConfirmAIButtonName).interactable, "判定した人をもう一度判定できます");
            Assert.IsFalse(FindButton("SelectRoomC_Button").interactable, "判定した人の部屋に入れます");
        }

        [UnityTest]
        public IEnumerator 判定していない人がいると一日を終えられない()
        {
            yield return GoToFinalDay();
            yield return JudgeOnFinalDay("A", confirmAI: true);
            yield return JudgeOnFinalDay("B", confirmAI: false);
            yield return JudgeOnFinalDay("C", confirmAI: false);

            FindComponent<ScreenNavigationUI>().ShowMonitorPanel();
            yield return null;
            yield return Click("Day_Button", "MonitorPanel");
            Assert.IsFalse(IsPanelActive("DayEndConfirmDialog"), "判定していない人がいるのに1日終了の確認が出ました");
            Assert.IsTrue(IsPanelActive("SmartphoneNotice"), "お知らせが出ません");
            Transform text = FindComponent<MonitorUI>().transform.Find("SmartphoneNotice/SmartphoneNoticeText");
            Assert.AreEqual("まだ判定していない人がいます", text.GetComponent<TMPro.TextMeshProUGUI>().text);

            yield return JudgeOnFinalDay("D", confirmAI: false);
            FindComponent<ScreenNavigationUI>().ShowMonitorPanel();
            yield return null;
            yield return Click("Day_Button", "MonitorPanel");
            Assert.IsTrue(IsPanelActive("DayEndConfirmDialog"), "全員判定しても1日終了の確認が出ません");
        }

        [UnityTest]
        public IEnumerator 最終日の判定は今日をやり直すと戻る()
        {
            yield return GoToFinalDay();
            yield return JudgeOnFinalDay("A", confirmAI: true);
            yield return JudgeOnFinalDay("B", confirmAI: false);

            FindComponent<GameUIController>().OnConfirmResetDay();
            yield return null;

            Assert.IsFalse(GetCharacter("A").IsConfirmedAI, "AI確定が戻っていません");
            Assert.IsFalse(GetCharacter("B").IsReleased, "解放が戻っていません");
            Assert.AreEqual(0, InterrogationManager.CurrentDailyActions);
            Assert.IsTrue(FindButton("SelectRoomA_Button").interactable, "やり直しても A の部屋に入れません");
        }

        #endregion

        #region 判定の結果

        [UnityTest]
        public IEnumerator AIを全員AI確定し人間を全員解放するとクリア()
        {
            GetCharacter("A").SetAIFlag(true);
            GetCharacter("C").SetAIFlag(true);
            yield return GoToFinalDay();

            yield return JudgeOnFinalDay("A", confirmAI: true);
            yield return JudgeOnFinalDay("B", confirmAI: false);
            yield return JudgeOnFinalDay("C", confirmAI: true);
            yield return JudgeOnFinalDay("D", confirmAI: false);
            Assert.AreEqual(EndingResult.AllCorrect, GameManager.EvaluateFinalResult());

            yield return EndDayByButton();
            yield return WaitForScene(ClearSceneName);
        }

        [UnityTest]
        public IEnumerator AIがいないときは全員解放すると全員正解()
        {
            yield return GoToFinalDay();
            foreach (string id in new[] { "A", "B", "C", "D" }) yield return JudgeOnFinalDay(id, confirmAI: false);

            Assert.AreEqual(EndingResult.AllCorrect, GameManager.EvaluateFinalResult());
            yield return EndDayByButton();
            yield return WaitForScene(ClearSceneName);
        }

        [UnityTest]
        public IEnumerator AIを解放するとゲームオーバー()
        {
            GetCharacter("A").SetAIFlag(true);
            yield return GoToFinalDay();

            yield return JudgeOnFinalDay("A", confirmAI: false);
            yield return JudgeOnFinalDay("B", confirmAI: true);
            yield return JudgeOnFinalDay("C", confirmAI: false);
            yield return JudgeOnFinalDay("D", confirmAI: false);
            Assert.AreEqual(EndingResult.ReleasedAI, GameManager.EvaluateFinalResult());

            yield return EndDayByButton();
            yield return WaitForScene(GameOverSceneName);
        }

        [UnityTest]
        public IEnumerator 人間をAI確定してもそれだけではゲームオーバーにならない()
        {
            GetCharacter("A").SetAIFlag(true);
            yield return GoToFinalDay();

            yield return JudgeOnFinalDay("A", confirmAI: true);
            yield return JudgeOnFinalDay("B", confirmAI: true);
            yield return JudgeOnFinalDay("C", confirmAI: false);
            yield return JudgeOnFinalDay("D", confirmAI: false);
            Assert.AreEqual(EndingResult.ConfirmedHuman, GameManager.EvaluateFinalResult());

            yield return EndDayByButton();
            yield return WaitForScene(ClearSceneName);
        }

        #endregion

        #region AI判定シート

        [UnityTest]
        public IEnumerator 判定日の終わりに回避フラグがそろっていなければAIになる()
        {
            var judgements = new List<ScenarioAIJudgement>
            {
                new ScenarioAIJudgement { characterId = "B", day = 1, avoidFlags = { "flag_テストBの回避" } },
                new ScenarioAIJudgement { characterId = "C", day = 1, avoidFlags = { "flag_テスト回避" } },
                new ScenarioAIJudgement { characterId = "D", day = 2, avoidFlags = { "flag_テスト回避" } },
            };
            yield return WithAIJudgements(judgements, Body());

            IEnumerator Body()
            {
                // C の回避フラグだけそろえる
                GameManager.Flags.TurnOn("flag_テスト回避");

                yield return EndDayByButton();
                Assert.AreEqual(2, GameManager.CurrentDay);
                Assert.IsTrue(GetCharacter("B").IsAI, "回避フラグがそろっていない B が AI になっていません");
                Assert.IsFalse(GetCharacter("C").IsAI, "回避フラグがそろっている C が AI になりました");
                Assert.IsFalse(GetCharacter("D").IsAI, "判定日がまだの D が AI になりました");

                // 判定は翌朝の状態に含まれるので、2日目をやり直しても戻らない
                FindComponent<GameUIController>().OnConfirmResetDay();
                yield return null;
                Assert.IsTrue(GetCharacter("B").IsAI, "やり直しで前日の AI判定が戻ってしまいました");
            }
        }

        [UnityTest]
        public IEnumerator 一度AIになったら回避フラグを後でそろえても戻らない()
        {
            var judgements = new List<ScenarioAIJudgement>
            {
                new ScenarioAIJudgement { characterId = "B", day = 1, avoidFlags = { "flag_テスト回避" } },
            };
            yield return WithAIJudgements(judgements, Body());

            IEnumerator Body()
            {
                yield return EndDayByButton();
                Assert.IsTrue(GetCharacter("B").IsAI);

                GameManager.Flags.TurnOn("flag_テスト回避");
                GameManager.ApplyAIJudgements(1);
                Assert.IsTrue(GetCharacter("B").IsAI, "AI から人間に戻りました");
            }
        }

        #endregion

        #region プロフィール解禁の確認

        [UnityTest]
        public IEnumerator プロフィールの解禁は使うポイントを確認してから行う()
        {
            InterrogationManager.SetPoints(1000);
            CharacterInstance a = GetCharacter("A");
            int cost = a.ProfileData.basicInfo.unlockCost;

            yield return Click("Profeofile_Button", "MonitorPanel");
            yield return Click("ACharacterChange_Button");
            yield return Click("Button_UnlockBasicInfo");

            StringAssert.Contains($"{cost}pt を使って『基礎情報』を解禁しますか？", ConfirmMessage());
            Assert.IsFalse(a.IsBasicInfoUnlocked, "確認の前に解禁されています");

            yield return Click("NoButton", ConfirmDialogPath);
            Assert.IsFalse(a.IsBasicInfoUnlocked, "「いいえ」なのに解禁されました");
            Assert.AreEqual(1000, InterrogationManager.CurrentPoints, "「いいえ」なのにポイントが減りました");

            yield return Click("Button_UnlockBasicInfo");
            yield return ClickConfirmYes();
            Assert.IsTrue(a.IsBasicInfoUnlocked);
            Assert.AreEqual(1000 - cost, InterrogationManager.CurrentPoints);
        }

        [UnityTest]
        public IEnumerator ポイントが足りないときは確認を出さない()
        {
            InterrogationManager.SetPoints(0);
            yield return Click("Profeofile_Button", "MonitorPanel");
            yield return Click("ACharacterChange_Button");
            yield return Click("Button_UnlockBasicInfo");

            Assert.IsFalse(IsConfirmDialogOpen(ConfirmDialogPath), "ポイントが足りないのに確認が出ました");
            Assert.IsFalse(GetCharacter("A").IsBasicInfoUnlocked);
        }

        #endregion

        #region ヘルパー

        private static IEnumerator GoToFinalDay()
        {
            Assert.IsTrue(GameManager.JumpToDay(GameManager.MaxDays), "最終日に移動できません");
            yield return null;
            Assert.IsTrue(GameManager.IsFinalDay);
        }

        private static string ConfirmMessage()
        {
            Transform text = GetConfirmDialog(ConfirmDialogPath).transform.Find("MessageText");
            Assert.IsNotNull(text, "確認ダイアログの文字欄が見つかりません");
            return text.GetComponent<TMPro.TextMeshProUGUI>().text;
        }

        /// <summary>
        /// シナリオのデータに AI判定の行を一時的に足してテストし、終わったら元に戻す
        /// </summary>
        private static IEnumerator WithAIJudgements(List<ScenarioAIJudgement> judgements, IEnumerator body)
        {
            List<ScenarioAIJudgement> list = GameManager.Scenario.aiJudgements;
            list.AddRange(judgements);
            try
            {
                while (body.MoveNext()) yield return body.Current;
            }
            finally
            {
                foreach (ScenarioAIJudgement j in judgements) list.Remove(j);
            }
        }

        #endregion
    }
}
#endif
