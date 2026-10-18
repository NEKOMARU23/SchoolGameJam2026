#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.SO;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 全ルートの自動テスト（仕様書16番）
    /// Excel を取り込んだあとに実行すると、シナリオのデータで次のことを確かめる
    /// ・どの場面・どの状態でも、会話が出ずに止まることがない
    /// ・すべてのエンディングにたどり着ける
    /// ・AI化・回避の条件が、書いたとおりに動く
    /// ・いろいろな遊び方で7日間を通して遊び、最後までエンディングが決まる
    /// </summary>
    public class RouteTest
    {
        private static readonly string[] Characters = { "A", "B", "C", "D" };

        /// <summary> 見つかった問題を、多すぎないように並べる </summary>
        private static string Report(List<string> problems, string title)
        {
            const int MaxLines = 20;
            string lines = string.Join("\n", problems.Take(MaxLines).Select(p => "・" + p));
            string more = problems.Count > MaxLines ? $"\n…ほか {problems.Count - MaxLines} 件" : "";
            return $"{title}（{problems.Count} 件）\n{lines}{more}";
        }

        #region 調べ方のテスト（作ったデータで、問題を見つけられるか）

        private static ScenarioDatabase CreateDatabase()
        {
            var db = ScriptableObject.CreateInstance<ScenarioDatabase>();
            db.dialogues.Add(new ScenarioDialogue { id = "T_1", pages = { new ScenarioPage { text = "会話1" } } });
            db.dialogues.Add(new ScenarioDialogue { id = "T_2", pages = { new ScenarioPage { text = "会話2" } } });
            return db;
        }

        private static List<string> ProblemsOf(ScenarioDatabase db, string sceneName)
        {
            // 3日間（3日目が最終日）、キャラ2人のゲームとして調べる
            return new ScenarioRouteChecker(db, 3, new[] { "A", "B" }).FindScenesWithoutDialogue()
                .Where(p => p.StartsWith(sceneName, StringComparison.Ordinal)).ToList();
        }

        [Test]
        public void 調べ方_条件を満たす行がない状態を見つける()
        {
            var db = CreateDatabase();
            db.conditions.Add(new ScenarioConditionRow { day = 1, characterId = "A", action = ScenarioAction.Talk, priority = 1, hasSelfAffection = true, selfAffection = 1, dialogueId = "T_1" });

            List<string> problems = ProblemsOf(db, "1日目 A に 対話");
            Assert.AreEqual(1, problems.Count, "好感度が足りないときに会話が出ないことを見つけられません");
            StringAssert.Contains("好感度", problems[0]);

            // 条件のない行を足せば、どの状態でも会話が出る
            db.conditions.Add(new ScenarioConditionRow { day = 1, characterId = "A", action = ScenarioAction.Talk, priority = 2, dialogueId = "T_2" });
            CollectionAssert.IsEmpty(ProblemsOf(db, "1日目 A に 対話"));
        }

        [Test]
        public void 調べ方_フラグの有無で分かれる行は止まらないと分かる()
        {
            var db = CreateDatabase();
            db.conditions.Add(new ScenarioConditionRow { day = 1, characterId = "A", action = ScenarioAction.Talk, priority = 1, requiredFlags = { "info_x" }, dialogueId = "T_1" });
            db.conditions.Add(new ScenarioConditionRow { day = 1, characterId = "A", action = ScenarioAction.Talk, priority = 2, ngFlags = { "info_x" }, dialogueId = "T_2" });
            // info_x は B との会話で手に入る
            db.conditions.Add(new ScenarioConditionRow { day = 1, characterId = "B", action = ScenarioAction.Talk, priority = 1, gainFlags = { "info_x" }, dialogueId = "T_1" });

            CollectionAssert.IsEmpty(ProblemsOf(db, "1日目 A に 対話"), "フラグが ON でも OFF でも会話が出るのに、問題として見つけています");
        }

        [Test]
        public void 調べ方_行動回数の条件で2日目に会話が出ない状態を見つける()
        {
            var db = CreateDatabase();
            // 2日目に「この行動をした回数が1回以上」の行しかないと、1日目にしていなければ会話が出ない
            db.conditions.Add(new ScenarioConditionRow { day = 2, characterId = "A", action = ScenarioAction.Talk, priority = 1, hasActionCount = true, actionCount = 1, dialogueId = "T_1" });

            List<string> problems = ProblemsOf(db, "2日目 A に 対話");
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("0回", problems[0]);
        }

        [Test]
        public void 調べ方_セリフIDがない行と行がない場面を見つける()
        {
            var db = CreateDatabase();
            db.conditions.Add(new ScenarioConditionRow { day = 1, characterId = "A", action = ScenarioAction.Talk, priority = 1, dialogueId = "T_ない", sourceLocation = "条件 2行目" });

            List<string> problems = ProblemsOf(db, "1日目 A に 対話");
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("T_ない", problems[0]);

            CollectionAssert.IsNotEmpty(ProblemsOf(db, "1日目 A に 拷問"), "条件シートに行がない場面を見つけられません");
        }

        [Test]
        public void 調べ方_たどり着けないエンディングを見つける()
        {
            var db = CreateDatabase();
            db.dialogues.Add(new ScenarioDialogue { id = "END_A", pages = { new ScenarioPage { text = "終わり" } } });
            db.dialogues.Add(new ScenarioDialogue { id = "END_B", pages = { new ScenarioPage { text = "終わり" } } });
            db.dialogues.Add(new ScenarioDialogue { id = "END_C", pages = { new ScenarioPage { text = "終わり" } } });
            db.endings.Add(new ScenarioEnding { priority = 1, result = EndingResult.Any, endingId = "END_A", listName = "どれでも" });
            db.endings.Add(new ScenarioEnding { priority = 2, result = EndingResult.AllCorrect, endingId = "END_B", listName = "隠れている" });
            db.endings.Add(new ScenarioEnding { priority = 0, result = EndingResult.AllCorrect, endingId = "END_C", listName = "真エンド", requiredFlags = { "flag_ない" } });

            List<string> problems = new ScenarioRouteChecker(db, 2, new[] { "A", "B" }).FindUnreachableEndings();
            Assert.AreEqual(2, problems.Count, Report(problems, "見つかった問題"));
            Assert.IsTrue(problems.Exists(p => p.Contains("隠れている") && p.Contains("優先度")), "先に別のエンディングに当てはまることを見つけられません");
            Assert.IsTrue(problems.Exists(p => p.Contains("真エンド") && p.Contains("flag_ない")), "手に入らないフラグが必要なことを見つけられません");
        }

        #endregion

        #region シナリオのデータを調べる

        private ScenarioRouteChecker CreateChecker()
        {
            Assert.IsNotNull(GameManager.Scenario, "前提: シナリオのデータがある");
            return new ScenarioRouteChecker(GameManager.Scenario, GameManager.MaxDays, GameManager.AllCharacters.Select(c => c.CharacterId).ToList());
        }

        [UnityTest]
        public IEnumerator どの場面のどの状態でも会話が出る()
        {
            yield return LoadInGameScene();
            List<string> problems = CreateChecker().FindScenesWithoutDialogue();
            Assert.IsEmpty(problems, Report(problems, "会話が出ずに止まる場面があります。条件シートに、条件のない行（いちばん大きい優先度）を足してください"));
        }

        [UnityTest]
        public IEnumerator どの判定結果でもエンディングが決まる()
        {
            yield return LoadInGameScene();
            var noEnding = new List<string>();
            CreateChecker().CollectReachableEndings(new[] { EndingResult.AllCorrect, EndingResult.ConfirmedHuman, EndingResult.ReleasedAI }, noEnding);
            Assert.IsEmpty(noEnding, Report(noEnding, "エンディングシートのどの行にも当てはまらず、エンディングの会話なしで終わる場合があります"));
        }

        [UnityTest]
        public IEnumerator すべてのエンディングにたどり着ける()
        {
            yield return LoadInGameScene();
            List<string> problems = CreateChecker().FindUnreachableEndings();
            Assert.IsEmpty(problems, Report(problems, "たどり着けないエンディングがあります"));
        }

        [UnityTest]
        public IEnumerator AIを解放したときのエンディングのためにAIになる道がある()
        {
            yield return LoadInGameScene();
            ScenarioRouteChecker checker = CreateChecker();
            List<ScenarioEnding> needAI = checker.EndingsOnlyWhenAIReleased();
            if (needAI.Count == 0 || checker.HasAIRoute) yield break;

            // シナリオがまだ書きかけのあいだは失敗にせず、まだ確かめられないことを知らせる
            Assert.Ignore($"だれも AI にならないので、「{string.Join("」「", needAI.Select(e => e.listName))}」にはたどり着けません。" +
                "条件シート・選択肢シートの「AI化」か、AI判定シートを書くと確かめられます");
        }

        #endregion

        #region AI化・回避

        [UnityTest]
        public IEnumerator AI判定シートのとおりに回避フラグがないとAIになりそろっているとならない()
        {
            yield return LoadInGameScene();
            ScenarioDatabase db = GameManager.Scenario;
            if (db.aiJudgements.Count == 0) Assert.Pass("AI判定シートに行がないので、確かめるものがありません");

            Core.State.GameStateSnapshot start = GameManager.CaptureState();
            foreach (var group in db.aiJudgements.GroupBy(j => (j.characterId, j.day)))
            {
                string where = $"AI判定シート: {group.Key.day}日目の {group.Key.characterId}";
                List<string> avoidFlags = group.SelectMany(j => j.avoidFlags).Distinct().ToList();

                // 回避フラグがそろっていない → AI になる
                GameManager.RestoreState(start);
                GameManager.ApplyAIJudgements(group.Key.day);
                Assert.IsTrue(GetCharacter(group.Key.characterId).IsAI, $"{where}: 回避フラグがないのに AI になりません");

                // 回避フラグがそろっている → AI にならない
                GameManager.RestoreState(start);
                foreach (string flag in avoidFlags) GameManager.Flags.TurnOn(flag);
                GameManager.ApplyAIJudgements(group.Key.day);
                Assert.IsFalse(GetCharacter(group.Key.characterId).IsAI, $"{where}: 回避フラグ {string.Join("・", avoidFlags)} がそろっているのに AI になりました");
            }
            GameManager.RestoreState(start);
        }

        [UnityTest]
        public IEnumerator 会話と選択肢のAI化と獲得フラグが書いたとおりに動く()
        {
            yield return LoadInGameScene();
            ScenarioDatabase db = GameManager.Scenario;
            List<ScenarioConditionRow> rows = db.conditions.Where(r => r.aiTargets.Count > 0 || r.gainFlags.Count > 0).ToList();
            List<ScenarioChoice> choices = db.choices.Where(c => c.aiTargets.Count > 0 || c.gainFlags.Count > 0).ToList();
            if (rows.Count == 0 && choices.Count == 0) Assert.Pass("「AI化」「獲得フラグ」を書いた行がないので、確かめるものがありません");

            Core.State.GameStateSnapshot start = GameManager.CaptureState();
            foreach (ScenarioConditionRow row in rows)
            {
                GameManager.RestoreState(start);
                GameManager.ApplyScenarioEffects(row);
                AssertEffects(row.aiTargets, row.gainFlags, row.sourceLocation);
            }
            foreach (ScenarioChoice choice in choices)
            {
                GameManager.RestoreState(start);
                GameManager.SelectChoice(choice, null);
                AssertEffects(choice.aiTargets, choice.gainFlags, choice.sourceLocation);
            }
            GameManager.RestoreState(start);
        }

        private static void AssertEffects(List<string> aiTargets, List<string> gainFlags, string where)
        {
            foreach (string id in aiTargets) Assert.IsTrue(GetCharacter(id).IsAI, $"{where}: 「AI化」の {id} が AI になりません");
            foreach (string flag in gainFlags) Assert.IsTrue(GameManager.Flags.IsOn(flag), $"{where}: 「獲得フラグ」の {flag} が ON になりません");
        }

        #endregion

        #region 7日間を通して遊ぶ

        /// <summary> 遊び方: その日のそのキャラにどの行動をするか、選択肢をどれにするか、最終日にどう判定するか </summary>
        private class PlayStyle
        {
            public string Name;
            public Func<int, int, InterrogationActionType> ChooseAction;    // (日, キャラの番号) → 行動
            public Func<int, int, int> ChooseOption = (count, _) => 0;     // (選択肢の数, 何回目) → 選ぶ番号
            public Func<int, bool> ConfirmAI = _ => false;                  // キャラの番号 → AI確定するか（false なら解放）
        }

        private static InterrogationActionType DailyAction(int day, int index, bool correct)
        {
            ScenarioDailySetting setting = GameManager.Scenario.GetDailySetting(day, Characters[index]);
            if (setting == null) return InterrogationActionType.Talk;
            if (correct) return setting.correctAction;
            return setting.hasNgAction ? setting.ngAction : InterrogationActionType.Torture;
        }

        private static IEnumerable<PlayStyle> PlayStyles()
        {
            InterrogationActionType[] all = { InterrogationActionType.Torture, InterrogationActionType.Talk, InterrogationActionType.Negotiate, InterrogationActionType.HearAbout };
            yield return new PlayStyle { Name = "毎日全員に対話・全員を解放", ChooseAction = (d, i) => InterrogationActionType.Talk };
            yield return new PlayStyle { Name = "毎日全員に拷問・全員を AI確定", ChooseAction = (d, i) => InterrogationActionType.Torture, ConfirmAI = _ => true };
            yield return new PlayStyle { Name = "毎日全員に交渉・A だけ AI確定", ChooseAction = (d, i) => InterrogationActionType.Negotiate, ConfirmAI = i => i == 0 };
            yield return new PlayStyle { Name = "毎日全員に聞き取り・最後の選択肢", ChooseAction = (d, i) => InterrogationActionType.HearAbout, ChooseOption = (n, _) => n - 1 };
            yield return new PlayStyle { Name = "毎日正解の行動", ChooseAction = (d, i) => DailyAction(d, i, correct: true) };
            yield return new PlayStyle { Name = "毎日 NG の行動", ChooseAction = (d, i) => DailyAction(d, i, correct: false), ConfirmAI = i => i % 2 == 1 };
            yield return new PlayStyle { Name = "日とキャラで行動を順に変える", ChooseAction = (d, i) => all[(d + i) % all.Length], ChooseOption = (n, k) => k % n };

            for (int seed = 1; seed <= 3; seed++)
            {
                var random = new System.Random(seed);
                yield return new PlayStyle
                {
                    Name = $"でたらめ（{seed}）",
                    ChooseAction = (d, i) => all[random.Next(all.Length)],
                    ChooseOption = (n, _) => random.Next(n),
                    ConfirmAI = _ => random.Next(2) == 0,
                };
            }
        }

        [UnityTest]
        public IEnumerator いろいろな遊び方で七日間を通して遊ぶと最後まで会話が出てエンディングが決まる()
        {
            var problems = new List<string>();
            var endings = new List<string>();

            foreach (PlayStyle style in PlayStyles())
            {
                yield return LoadInGameScene();
                PlayThrough(style, problems);

                Core.Manager.EndingDecision decision = GameManager.DecideEnding();
                if (decision.Ending == null) problems.Add($"{style.Name}: エンディングが決まりません（判定結果: {ScenarioRouteChecker.ResultName(decision.Result)}）");
                else if (decision.Dialogue == null || decision.Dialogue.pages.Count == 0) problems.Add($"{style.Name}: エンディング「{decision.Ending.listName}」の会話がありません");
                else endings.Add($"{style.Name} → {decision.Ending.listName}");
            }

            Debug.Log("[RouteTest] たどり着いたエンディング\n" + string.Join("\n", endings));
            Assert.IsEmpty(problems, Report(problems, "通して遊ぶと問題が起きました"));
        }

        /// <summary> その遊び方で、1日目から最終日の判定まで遊ぶ（画面は使わず、マネージャーを直接動かす） </summary>
        private static void PlayThrough(PlayStyle style, List<string> problems)
        {
            int choiceCount = 0;

            void FollowChoices(ScenarioDialogue dialogue, CharacterInstance partner, string where)
            {
                for (int guard = 0; dialogue != null && guard < 20; guard++)
                {
                    List<ScenarioChoice> available = GameManager.GetAvailableChoices(dialogue, partner);
                    if (available.Count == 0) return;

                    ScenarioChoice choice = available[Mathf.Clamp(style.ChooseOption(available.Count, choiceCount++), 0, available.Count - 1)];
                    dialogue = GameManager.SelectChoice(choice, partner);
                    if (dialogue == null && !string.IsNullOrEmpty(choice.nextDialogueId))
                    {
                        problems.Add($"{style.Name}: {where} の選択肢「{choice.text}」の次の会話「{choice.nextDialogueId}」がありません");
                    }
                }
            }

            while (true)
            {
                int day = GameManager.CurrentDay;
                bool isFinalDay = GameManager.IsFinalDay;

                // スマホは、その日に聞ける回数を全部使う
                while (Core.Manager.SmartphoneTalkManager.Instance.TryStartTalk(out ScenarioDialogue talk))
                {
                    if (talk == null || talk.pages.Count == 0) problems.Add($"{style.Name}: {day}日目のスマホの会話がありません");
                    FollowChoices(talk, null, $"{day}日目のスマホ");
                }

                for (int i = 0; i < Characters.Length; i++)
                {
                    CharacterInstance target = GetCharacter(Characters[i]);
                    InterrogationActionType action = isFinalDay
                        ? (style.ConfirmAI(i) ? InterrogationActionType.ConfirmAI : InterrogationActionType.Release)
                        : style.ChooseAction(day, i);
                    string hearingTarget = action == InterrogationActionType.HearAbout ? Characters[(i + day) % Characters.Length] : "";
                    if (hearingTarget == Characters[i]) hearingTarget = Characters[(i + 1) % Characters.Length];

                    string where = $"{day}日目 {Characters[i]} に {action}{(hearingTarget.Length > 0 ? $"（{hearingTarget} について）" : "")}";
                    Core.Manager.ActionOutcome outcome = InterrogationManager.ExecuteAction(target, action, hearingTarget);
                    if (!outcome.HasDialogue)
                    {
                        problems.Add($"{style.Name}: {where} で会話が出ません（{outcome.Text}）");
                        continue;
                    }
                    FollowChoices(outcome.Dialogue, target, where);
                }

                if (isFinalDay) return;

                GameManager.AdvanceToNextDay();
                if (GameManager.CurrentDay != day + 1)
                {
                    problems.Add($"{style.Name}: {day}日目から次の日に進めません");
                    return;
                }
            }
        }

        #endregion
    }
}
#endif
