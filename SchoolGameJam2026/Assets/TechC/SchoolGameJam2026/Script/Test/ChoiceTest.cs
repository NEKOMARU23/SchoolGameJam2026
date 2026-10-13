#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.UI;
using TechC.SchoolGameJam2026.Scripts.UI.Dialogue;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 会話の後に出す選択肢（取調室・スマホ）のテスト
    /// シナリオのデータに選択肢を一時的に足して確認する
    /// </summary>
    public class ChoiceTest
    {
        private const string LetterFlag = "item_テスト用の手紙";
        private const string SilenceFlag = "info_テスト用の沈黙";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
        }

        [UnityTest]
        public IEnumerator 条件を満たさない選択肢は出ない()
        {
            yield return WithTalkChoices(Body());

            IEnumerator Body()
            {
                yield return TalkToA();
                ChoiceUI choiceUI = FindComponent<ChoiceUI>();
                Assert.IsTrue(choiceUI.IsOpen, "会話の後に選択肢が出ません");
                CollectionAssert.AreEqual(new[] { "黙って見つめる" }, choiceUI.ShownTexts, "手紙を持っていないのに手紙の選択肢が出ています");
                Assert.IsFalse(FindButton("Button_Torture").interactable, "選択肢を選ぶ前に次の行動ができます");
            }
        }

        [UnityTest]
        public IEnumerator 条件を満たす選択肢は順番どおりに出る()
        {
            yield return WithTalkChoices(Body());

            IEnumerator Body()
            {
                GameManager.Flags.TurnOn(LetterFlag);
                yield return TalkToA();
                CollectionAssert.AreEqual(new[] { "手紙のことを聞く", "黙って見つめる" }, FindComponent<ChoiceUI>().ShownTexts);
            }
        }

        [UnityTest]
        public IEnumerator 選ぶと効果が反映され行動回数は増えず続けて選べる()
        {
            yield return WithTalkChoices(Body());

            IEnumerator Body()
            {
                yield return TalkToA();
                CharacterInstance a = GetCharacter("A");
                int affectionBefore = a.CurrentAffection;
                Assert.AreEqual(1, InterrogationManager.CurrentDailyActions);

                yield return Click("ChoiceButton_1", "ChoiceUI");
                Assert.AreEqual(affectionBefore - 1, a.CurrentAffection, "好感度の変化が反映されていません");
                Assert.IsTrue(GameManager.Flags.IsOn(SilenceFlag), "獲得フラグが ON になっていません");
                Assert.AreEqual(1, InterrogationManager.CurrentDailyActions, "選択肢で行動回数が増えました");
                Assert.IsTrue(FindComponent<DialogueUI>().IsDialoguePlaying, "選んだ後の会話が始まりません");

                // 選んだ先の会話にも選択肢がある
                yield return SkipDialogue();
                CollectionAssert.AreEqual(new[] { "もう一度問い詰める" }, FindComponent<ChoiceUI>().ShownTexts, "続けて選択肢が出ません");

                // 次の会話がない選択肢を選ぶと、そこで終わる
                yield return Click("ChoiceButton_1", "ChoiceUI");
                Assert.IsFalse(FindComponent<ChoiceUI>().IsOpen);
                Assert.IsFalse(FindComponent<DialogueUI>().IsDialoguePlaying);
                Assert.IsFalse(FindButton("Button_Torture").interactable, "取り調べ済みの相手にまた行動できます");
            }
        }

        [UnityTest]
        public IEnumerator スマホの会話でも選択肢が出て回数は増えない()
        {
            ScenarioDatabase db = GameManager.Scenario;
            ScenarioConditionRow firstTalk = db.GetConditionRows(1, "", ScenarioAction.Smartphone, "", 1).First();
            string next = OtherDialogueIds(firstTalk.dialogueId).First();
            var choices = new List<ScenarioChoice>
            {
                new ScenarioChoice { dialogueId = firstTalk.dialogueId, order = 1, text = "詳しく聞く", gainFlags = { SilenceFlag }, nextDialogueId = next },
            };
            yield return WithChoices(choices, Body());

            IEnumerator Body()
            {
                var talk = FindComponent<DialogueClickable>();
                yield return Click("Button_Help", "MonitorPanel");
                yield return Click("CloseButton", "SmartphoneTalkPanel");

                Assert.IsTrue(FindComponent<ChoiceUI>().IsOpen, "スマホの会話の後に選択肢が出ません");
                Assert.IsTrue(IsPanelActive("SmartphoneTalkPanel"), "選択肢を選ぶ前にスマホの画面が閉じました");

                yield return Click("ChoiceButton_1", "ChoiceUI");
                Assert.IsTrue(talk.IsOpen, "選んだ後の会話がスマホに出ません");
                Assert.IsTrue(GameManager.Flags.IsOn(SilenceFlag));
                Assert.AreEqual(1, Core.Manager.SmartphoneTalkManager.Instance.TalkCountToday, "選択肢でスマホの回数が増えました");

                yield return Click("CloseButton", "SmartphoneTalkPanel");
                Assert.IsTrue(IsPanelActive("MonitorPanel"), "会話が終わってもモニターに戻りません");
            }
        }

        #region ヘルパー

        private static IEnumerator TalkToA()
        {
            FindComponent<GameUIController>().SelectCharacter("A");
            yield return null;
            yield return Click("Button_Talk");
            yield return SkipDialogue();
        }

        /// <summary>
        /// 1日目に A と対話したときに出る会話（どの行が選ばれても）の後に、選択肢を足す
        ///   1. 手紙のことを聞く（手紙を持っているときだけ）
        ///   2. 黙って見つめる（好感度 -1、情報を獲得 → 次の会話でも選択肢「もう一度問い詰める」）
        /// </summary>
        private static IEnumerator WithTalkChoices(IEnumerator body)
        {
            ScenarioDatabase db = GameManager.Scenario;
            List<string> sources = db.GetConditionRows(1, "A", ScenarioAction.Talk).Select(r => r.dialogueId).Distinct().ToList();
            List<string> others = OtherDialogueIds(sources.ToArray()).Take(2).ToList();
            string letterDialogue = others[0];
            string silenceDialogue = others[1];

            var choices = new List<ScenarioChoice>();
            foreach (string source in sources)
            {
                choices.Add(new ScenarioChoice { dialogueId = source, order = 1, text = "手紙のことを聞く", requiredFlags = { LetterFlag }, nextDialogueId = letterDialogue });
                choices.Add(new ScenarioChoice { dialogueId = source, order = 2, text = "黙って見つめる", affectionChange = -1, gainFlags = { SilenceFlag }, nextDialogueId = silenceDialogue });
            }
            choices.Add(new ScenarioChoice { dialogueId = silenceDialogue, order = 1, text = "もう一度問い詰める" });

            yield return WithChoices(choices, body);
        }

        private static IEnumerable<string> OtherDialogueIds(params string[] exclude)
        {
            return GameManager.Scenario.dialogues.Select(d => d.id).Where(id => !exclude.Contains(id) && !id.StartsWith("SP_"));
        }

        private static IEnumerator WithChoices(List<ScenarioChoice> choices, IEnumerator body)
        {
            List<ScenarioChoice> list = GameManager.Scenario.choices;
            list.AddRange(choices);
            try
            {
                while (body.MoveNext()) yield return body.Current;
            }
            finally
            {
                foreach (ScenarioChoice c in choices) list.Remove(c);
            }
        }

        #endregion
    }
}
#endif
