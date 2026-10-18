#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.Scenario.Import;
using TechC.SchoolGameJam2026.Scripts.SO;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// Excel のシナリオ（取り込みのチェック・会話の選び方・会話の効果）のテスト
    /// </summary>
    public class ScenarioTest
    {
        private static readonly string[] CharacterIds = { "A", "B", "C", "D" };

        private static readonly string[] ConditionHeaders =
        {
            "日", "キャラ", "行動", "聞き取り相手", "回目", "優先度", "本人の好感度", "他キャラの好感度",
            "行動回数", "必要フラグ", "NGフラグ", "獲得フラグ", "AI化", "セリフID",
        };

        #region 取り込みのチェック

        [Test]
        public void 正しく書かれたシートを取り込める()
        {
            var sheets = CreateWorkbook();
            ScenarioDatabase db = Parse(sheets, out ImportReport report);

            CollectionAssert.IsEmpty(report.Errors.Select(e => e.ToString()));
            CollectionAssert.IsEmpty(report.Warnings.Select(e => e.ToString()));

            Assert.AreEqual(2, db.FindDialogue("A_D1_対話_1").pages.Count, "同じセリフIDの行は1つの会話のページになる");
            Assert.AreEqual(PageKind.Narration, db.FindDialogue("A_D1_対話_1").pages[1].kind);

            ScenarioConditionRow row = db.GetConditionRows(1, "A", ScenarioAction.Talk)[0];
            Assert.AreEqual("A_D1_対話_2", row.dialogueId, "優先度の小さい行が先に並ぶ");
            Assert.AreEqual("B", row.otherAffections[0].characterId);
            Assert.AreEqual(2, row.otherAffections[0].minValue);
            CollectionAssert.AreEqual(new[] { "info_鍵" }, row.gainFlags);
            CollectionAssert.AreEqual(new[] { "C" }, row.aiTargets);

            Assert.AreEqual(1, db.GetConditionRows(1, "A", ScenarioAction.HearAbout, "B").Count);
            Assert.AreEqual(0, db.GetConditionRows(1, "A", ScenarioAction.HearAbout, "C").Count, "聞き取り相手が違う行は候補にならない");
            Assert.AreEqual(2, db.GetSmartphoneTalkCount(1), "スマホの回数は回目の最大");
            ScenarioConditionRow morning = db.GetConditionRows(1, "", ScenarioAction.Morning).Single();
            Assert.AreEqual("MORNING_D1", morning.dialogueId, "朝の行はキャラ・回目なしで取り込める");
            CollectionAssert.AreEqual(new[] { "flag_和解" }, morning.gainFlags);
            Assert.AreEqual(InterrogationActionType.Torture, db.GetDailySetting(1, "A").correctAction);
            Assert.AreEqual(InterrogationActionType.Negotiate, db.GetDailySetting(1, "A").ngAction);
        }

        [TestCase(1, "必要フラグ", "info_鍵ｘ", "フラグ一覧にありません")]
        [TestCase(1, "セリフID", "A_D1_存在しない", "本文シートにありません")]
        [TestCase(1, "行動", "なぐる", "は使えません")]
        [TestCase(1, "日", "8", "1〜7")]
        [TestCase(1, "キャラ", "E", "は使えません")]
        [TestCase(1, "他キャラの好感度", "B=2", "書き方が違います")]
        [TestCase(1, "優先度", "3", "優先度が抜けています")]
        [TestCase(2, "優先度", "1", "重複しています")]
        public void 書き間違いはエラーになる(int dataRow, string header, string value, string expectedMessage)
        {
            var sheets = CreateWorkbook();
            // 条件シートの行（1行目: A 1日目 対話 優先度1 / 2行目: 同じ場面の優先度2）を書き換える
            SetCell(sheets["条件"], dataRow, header, value);

            Parse(sheets, out ImportReport report);

            Assert.IsTrue(report.HasErrors, $"{header}「{value}」でエラーになりません");
            Assert.IsTrue(report.Errors.Any(e => e.Text.Contains(expectedMessage) && e.Sheet == "条件"),
                $"「{expectedMessage}」のエラーがありません: {string.Join(" / ", report.Errors)}");
        }

        [TestCase("正解行動", "解放")]
        [TestCase("NG行動", "解放")]
        [TestCase("NG行動", "AI確定")]
        [TestCase("NG行動", "聞き取り")]
        public void 日別設定には拷問_対話_交渉だけ書ける(string header, string value)
        {
            var sheets = CreateWorkbook();
            SetCell(sheets["日別設定"], 1, header, value);

            Parse(sheets, out ImportReport report);

            Assert.IsTrue(report.Errors.Any(e => e.Sheet == "日別設定" && e.Text.Contains("は使えません")),
                $"日別設定の{header}に「{value}」を書いてもエラーになりません: {string.Join(" / ", report.Errors)}");
        }

        private static readonly string[] FlagHeaders = { "フラグ名", "種類", "表示先", "説明", "プロフィールに表示する文", "見出し", "解禁ポイント", "関係するキャラ", "画像" };

        /// <summary> フラグ一覧のシートを、資料画面用の列つきに差し替える </summary>
        private static Dictionary<string, List<List<string>>> CreateWorkbookWithFlagColumns(params string[][] extraRows)
        {
            var sheets = CreateWorkbook();
            var rows = new List<string[]>
            {
                FlagHeaders,
                new[] { "info_鍵", "情報", "A", "鍵の話", "鍵を持っていた", "鍵を持っていた", "", "B, C", "" },
                new[] { "flag_和解", "進行フラグ", "なし", "", "", "", "", "", "" },
            };
            rows.AddRange(extraRows);
            sheets["フラグ一覧"] = Table(rows.ToArray());
            return sheets;
        }

        [Test]
        public void フラグ一覧の資料画面用の列を取り込める()
        {
            var sheets = CreateWorkbookWithFlagColumns(
                new[] { "info_停電", "情報", "その他", "", "停電は夜の9時だった", "停電の時刻", "30", "", "" },
                new[] { "item_鍵束", "キーアイテム", "アイテム", "", "現場の鍵束", "鍵束", "", "A", "鍵束の絵" });
            ScenarioDatabase db = Parse(sheets, out ImportReport report);

            CollectionAssert.IsEmpty(report.Errors.Select(e => e.ToString()));
            ScenarioFlagDefinition key = db.FindFlag("info_鍵");
            Assert.AreEqual("鍵を持っていた", key.title);
            CollectionAssert.AreEqual(new[] { "B", "C" }, key.relatedCharacters);
            Assert.IsFalse(key.IsPointUnlock, "解禁ポイントが空なのにポイントで解禁する情報になっています");

            ScenarioFlagDefinition point = db.FindFlag("info_停電");
            Assert.IsTrue(point.IsPointUnlock);
            Assert.AreEqual(30, point.unlockCost);
            Assert.AreEqual("鍵束の絵", db.FindFlag("item_鍵束").image);
        }

        [TestCase("解禁ポイント", "たくさん", "数")]
        [TestCase("関係するキャラ", "E", "は使えません")]
        public void フラグ一覧の資料画面用の列の書き間違いはエラーになる(string header, string value, string expectedMessage)
        {
            var sheets = CreateWorkbookWithFlagColumns();
            SetCell(sheets["フラグ一覧"], 1, header, value);

            Parse(sheets, out ImportReport report);

            Assert.IsTrue(report.Errors.Any(e => e.Sheet == "フラグ一覧" && e.Text.Contains(expectedMessage)),
                $"{header}「{value}」で「{expectedMessage}」のエラーになりません: {string.Join(" / ", report.Errors)}");
        }

        [Test]
        public void 解禁ポイントは情報にだけ書ける()
        {
            var sheets = CreateWorkbookWithFlagColumns(new[] { "item_鍵束", "キーアイテム", "アイテム", "", "", "鍵束", "30", "", "" });
            Parse(sheets, out ImportReport report);
            Assert.IsTrue(report.Errors.Any(e => e.Text.Contains("解禁ポイントは「情報」にだけ")), string.Join(" / ", report.Errors));
        }

        [Test]
        public void 解禁ポイントがある情報を会話の獲得フラグに書くとエラーになる()
        {
            // 条件シートの1行目は info_鍵 を獲得する。その info_鍵 に解禁ポイントを付ける
            var sheets = CreateWorkbookWithFlagColumns();
            SetCell(sheets["フラグ一覧"], 1, "解禁ポイント", "30");

            Parse(sheets, out ImportReport report);
            Assert.IsTrue(report.Errors.Any(e => e.Text.Contains("会話かポイントのどちらか片方だけ")), string.Join(" / ", report.Errors));
        }

        [Test]
        public void アイテムの絵の素材がないと警告になる()
        {
            var sheets = CreateWorkbookWithFlagColumns(new[] { "item_鍵束", "キーアイテム", "アイテム", "", "", "鍵束", "", "", "ない絵" });
            Parse(sheets, out ImportReport report, assetExists: (kind, name) => kind != AssetKind.Item);

            CollectionAssert.IsEmpty(report.Errors.Select(e => e.ToString()));
            Assert.IsTrue(report.Warnings.Any(w => w.Text.Contains("ない絵")), string.Join(" / ", report.Warnings));
        }

        [Test]
        public void 選択肢シートを取り込める()
        {
            ScenarioDatabase db = Parse(CreateWorkbook(), out ImportReport report);

            CollectionAssert.IsEmpty(report.Errors.Select(e => e.ToString()));
            List<ScenarioChoice> choices = db.GetChoices("A_D1_対話_1");
            Assert.AreEqual(2, choices.Count);
            Assert.AreEqual("鍵のことを聞く", choices[0].text);
            CollectionAssert.AreEqual(new[] { "info_鍵" }, choices[0].requiredFlags);
            Assert.AreEqual("B", choices[0].otherAffections[0].characterId);
            Assert.AreEqual(1, choices[0].affectionChange);
            CollectionAssert.AreEqual(new[] { "flag_和解" }, choices[0].gainFlags);
            Assert.AreEqual("A_D1_対話_2", choices[0].nextDialogueId);
            Assert.AreEqual(-1, choices[1].affectionChange);
            CollectionAssert.AreEqual(new[] { "C" }, choices[1].aiTargets);
            Assert.AreEqual("", choices[1].nextDialogueId, "次のセリフIDは空欄でよい");
        }

        [TestCase(1, "セリフID", "A_D1_存在しない", "本文シートにありません")]
        [TestCase(1, "次のセリフID", "A_D1_存在しない", "本文シートにありません")]
        [TestCase(1, "選択肢の文", "", "選択肢の文が空です")]
        [TestCase(1, "必要フラグ", "info_ない", "フラグ一覧にありません")]
        [TestCase(2, "順", "3", "順が抜けています")]
        [TestCase(2, "順", "1", "重複しています")]
        [TestCase(1, "好感度の変化", "たくさん", "整数で書いてください")]
        public void 選択肢シートの書き間違いはエラーになる(int dataRow, string header, string value, string expectedMessage)
        {
            var sheets = CreateWorkbook();
            SetCell(sheets["選択肢"], dataRow, header, value);

            Parse(sheets, out ImportReport report);

            Assert.IsTrue(report.Errors.Any(e => e.Sheet == "選択肢" && e.Text.Contains(expectedMessage)),
                $"「{expectedMessage}」のエラーがありません: {string.Join(" / ", report.Errors)}");
        }

        [Test]
        public void 選択肢シートがない古いファイルも警告だけで取り込める()
        {
            var sheets = CreateWorkbook();
            sheets.Remove("選択肢");

            ScenarioDatabase db = Parse(sheets, out ImportReport report);

            Assert.IsFalse(report.HasErrors, string.Join(" / ", report.Errors));
            Assert.IsTrue(report.Warnings.Exists(w => w.Sheet == "選択肢"));
            CollectionAssert.IsEmpty(db.choices);
        }

        [Test]
        public void 選択肢は好感度とフラグの条件を満たすときだけ出せる()
        {
            var flags = new FlagStore();
            var choice = new ScenarioChoice
            {
                hasSelfAffection = true, selfAffection = 2,
                requiredFlags = { "item_鍵" }, ngFlags = { "flag_和解" },
            };
            var ctx = new ScenarioSelector.Context { Flags = flags, SelfAffection = 2 };

            Assert.IsFalse(ScenarioSelector.IsSatisfied(choice, ctx), "必要フラグがない");
            flags.TurnOn("item_鍵");
            Assert.IsTrue(ScenarioSelector.IsSatisfied(choice, ctx));
            ctx.SelfAffection = 1;
            Assert.IsFalse(ScenarioSelector.IsSatisfied(choice, ctx), "好感度が足りない");
            ctx.SelfAffection = 2;
            flags.TurnOn("flag_和解");
            Assert.IsFalse(ScenarioSelector.IsSatisfied(choice, ctx), "NGフラグが ON");
        }

        [TestCase("6", false)]
        [TestCase("7", true)]
        public void AI判定の判定日に最終日は書けない(string day, bool expectError)
        {
            var sheets = CreateWorkbook();
            sheets["AI判定"].Add(new List<string> { "B", day, "info_鍵" });

            ScenarioDatabase db = Parse(sheets, out ImportReport report);

            Assert.AreEqual(expectError, report.Errors.Exists(e => e.Sheet == "AI判定" && e.Text.Contains("最終日")),
                string.Join(" / ", report.Errors));
            if (!expectError) Assert.AreEqual(1, db.aiJudgements.Count);
        }

        [Test]
        public void 素材がないと警告になり非表示は警告にならない()
        {
            var sheets = CreateWorkbook();
            SetCell(sheets["本文"], 1, "SE", "ドア");
            SetCell(sheets["本文"], 1, "立ち絵", "非表示");

            Parse(sheets, out ImportReport report, assetExists: (kind, name) => false);

            Assert.IsFalse(report.HasErrors, string.Join(" / ", report.Errors));
            Assert.AreEqual(1, report.Warnings.Count, string.Join(" / ", report.Warnings));
            StringAssert.Contains("ドア", report.Warnings[0].Text);
        }

        [Test]
        public void シートや見出しがないとエラーになる()
        {
            var sheets = CreateWorkbook();
            sheets.Remove("日別設定");
            sheets["条件"][0][sheets["条件"][0].IndexOf("優先度")] = "ゆうせんど";

            Parse(sheets, out ImportReport report);

            Assert.IsTrue(report.Errors.Any(e => e.Sheet == "日別設定" && e.Text.Contains("シートがありません")));
            Assert.IsTrue(report.Errors.Any(e => e.Sheet == "条件" && e.Text.Contains("優先度")));
        }

        [Test]
        public void コメント行と空の行は読み飛ばす()
        {
            var sheets = CreateWorkbook();
            sheets["本文"].Add(new List<string> { "# メモ: ここは読まない", "会話じゃない" });
            sheets["本文"].Add(new List<string> { "", "  " });

            Parse(sheets, out ImportReport report);

            Assert.IsFalse(report.HasErrors, string.Join(" / ", report.Errors));
        }

        #endregion

        #region 会話の選び方

        [Test]
        public void 条件を満たす優先度の一番小さい行が選ばれる()
        {
            var flags = new FlagStore();
            var rows = new List<ScenarioConditionRow>
            {
                new ScenarioConditionRow { priority = 1, dialogueId = "好感度", hasSelfAffection = true, selfAffection = 3 },
                new ScenarioConditionRow { priority = 2, dialogueId = "回数", hasActionCount = true, actionCount = 2 },
                new ScenarioConditionRow { priority = 3, dialogueId = "フラグ", requiredFlags = { "info_鍵" }, ngFlags = { "flag_和解" } },
                new ScenarioConditionRow { priority = 4, dialogueId = "他キャラ", otherAffections = { new AffectionCondition { characterId = "B", minValue = 2 } } },
                new ScenarioConditionRow { priority = 5, dialogueId = "いつもの" },
            };
            var affections = new Dictionary<string, int> { { "B", 0 } };
            var ctx = new ScenarioSelector.Context { Flags = flags, AffectionOf = id => affections[id] };

            Assert.AreEqual("いつもの", ScenarioSelector.Select(rows, ctx).dialogueId, "どの条件も満たさなければ条件なしの行");

            affections["B"] = 2;
            Assert.AreEqual("他キャラ", ScenarioSelector.Select(rows, ctx).dialogueId);

            flags.TurnOn("info_鍵");
            Assert.AreEqual("フラグ", ScenarioSelector.Select(rows, ctx).dialogueId);
            flags.TurnOn("flag_和解");
            Assert.AreEqual("他キャラ", ScenarioSelector.Select(rows, ctx).dialogueId, "NGフラグが ON なら選ばれない");

            ctx.ActionCount = 2;
            Assert.AreEqual("回数", ScenarioSelector.Select(rows, ctx).dialogueId);

            ctx.SelfAffection = 3;
            Assert.AreEqual("好感度", ScenarioSelector.Select(rows, ctx).dialogueId);
        }

        [Test]
        public void どの行も満たさなければ何も選ばれない()
        {
            var rows = new List<ScenarioConditionRow>
            {
                new ScenarioConditionRow { priority = 1, hasSelfAffection = true, selfAffection = 5 },
            };
            Assert.IsNull(ScenarioSelector.Select(rows, new ScenarioSelector.Context { Flags = new FlagStore() }));
            Assert.IsNull(ScenarioSelector.Select(null, new ScenarioSelector.Context()));
        }

        #endregion

        #region ゲーム中の動き

        [UnityTest]
        public IEnumerator 行動のポイントは成功_普通_最低の三段階で減ることはない()
        {
            yield return LoadInGameScene();
            ScenarioDatabase db = GameManager.Scenario;
            Assert.IsNotNull(db, "GameManager に ScenarioDatabase がつながっていません");

            // 成功＝日別設定の正解行動、最低＝NG行動、普通＝どちらでもない行動
            ScenarioDailySetting a = db.GetDailySetting(1, "A");
            ScenarioDailySetting b = db.GetDailySetting(1, "B");
            ScenarioDailySetting c = db.GetDailySetting(1, "C");
            Assume.That(b.hasNgAction && !Core.Manager.InterrogationManager.IsJudgementAction(b.ngAction), "前提: 1日目のメリに取り調べの NG行動がある");
            InterrogationActionType normal = NormalAction(c);

            Core.Manager.ActionOutcome success = InterrogationManager.ExecuteAction(GetCharacter("A"), a.correctAction);
            Assert.IsTrue(success.HasDialogue, "Excel の会話が選ばれていません");
            Core.Manager.ActionOutcome middle = InterrogationManager.ExecuteAction(GetCharacter("C"), normal);
            int before = InterrogationManager.CurrentPoints;
            Core.Manager.ActionOutcome lowest = InterrogationManager.ExecuteAction(GetCharacter("B"), b.ngAction);

            Assert.Greater(success.EarnedPoints, middle.EarnedPoints, "成功のポイントが普通より多くありません");
            Assert.Greater(middle.EarnedPoints, lowest.EarnedPoints, "普通のポイントが最低より多くありません");
            Assert.GreaterOrEqual(lowest.EarnedPoints, 0, "最低のポイントが0より小さくなっています");
            Assert.GreaterOrEqual(InterrogationManager.CurrentPoints, before, "NG行動でポイントが減りました");
            Assert.AreEqual(success.EarnedPoints + middle.EarnedPoints + lowest.EarnedPoints, InterrogationManager.TodayEarnedPoints,
                "今日の獲得ポイントが、行動で入ったポイントの合計になっていません");
        }

        /// <summary> 正解行動でも NG行動でもない、取り調べの行動 </summary>
        private static InterrogationActionType NormalAction(ScenarioDailySetting setting)
        {
            foreach (var action in new[] { InterrogationActionType.Torture, InterrogationActionType.Talk, InterrogationActionType.Negotiate })
            {
                if (action != setting.correctAction && !(setting.hasNgAction && action == setting.ngAction)) return action;
            }
            Assert.Fail("普通になる行動がありません");
            return InterrogationActionType.Talk;
        }

        [UnityTest]
        public IEnumerator 会話の獲得フラグとAI化が反映される()
        {
            yield return LoadInGameScene();

            CharacterInstance human = GameManager.AllCharacters.First(c => !c.IsAI);
            var row = new ScenarioConditionRow
            {
                gainFlags = { "info_テスト用の情報" },
                aiTargets = { human.CharacterId },
            };

            GameManager.ApplyScenarioEffects(row);

            Assert.IsTrue(GameManager.Flags.IsOn("info_テスト用の情報"));
            Assert.IsTrue(human.IsAI, "AI化に書いたキャラが AI になりません");
        }

        [UnityTest]
        public IEnumerator スマホはその日の回目の数まで聞ける()
        {
            yield return LoadInGameScene();
            var talk = Core.Manager.SmartphoneTalkManager.Instance;
            int limit = GameManager.Scenario.GetSmartphoneTalkCount(1);
            Assert.Greater(limit, 0, "1日目のスマホの会話がありません");

            for (int i = 1; i <= limit; i++)
            {
                Assert.IsTrue(talk.TryStartTalk(out ScenarioDialogue dialogue), $"{i}回目の会話を始められません");
                Assert.IsNotNull(dialogue);
                Assert.AreEqual(i, talk.TalkCountToday);
            }

            Assert.IsFalse(talk.TryStartTalk(out _), "回数を使い切っても会話を始められました");
            Assert.AreEqual(limit, talk.TalkCountToday, "始められなかった会話で回数が増えました");
        }

        #endregion

        #region テスト用のデータ

        private static ScenarioDatabase Parse(Dictionary<string, List<List<string>>> sheets, out ImportReport report,
            System.Func<AssetKind, string, bool> assetExists = null)
        {
            var db = ScriptableObject.CreateInstance<ScenarioDatabase>();
            report = ScenarioWorkbookParser.Parse(sheets, db, assetExists ?? ((kind, name) => true), CharacterIds, 7);
            return db;
        }

        /// <summary> 書き間違いのない最小のワークブック </summary>
        private static Dictionary<string, List<List<string>>> CreateWorkbook()
        {
            return new Dictionary<string, List<List<string>>>
            {
                ["フラグ一覧"] = Table(
                    new[] { "フラグ名", "種類", "表示先", "説明", "プロフィールに表示する文", "見出し" },
                    new[] { "info_鍵", "情報", "A", "鍵の話", "鍵を持っていた", "鍵を持っていた" },
                    new[] { "flag_和解", "進行フラグ", "なし", "", "", "" }),
                ["本文"] = Table(
                    new[] { "セリフID", "種類", "話者", "本文", "SE", "立ち絵", "背景", "イベント絵", "エフェクト", "BGM" },
                    new[] { "A_D1_対話_1", "会話", "", "こんにちは", "", "A_通常", "", "", "", "" },
                    new[] { "A_D1_対話_1", "地の文", "", "（黙り込んだ）", "", "", "", "", "", "" },
                    new[] { "A_D1_対話_2", "会話", "", "鍵の話をしよう", "", "", "", "", "", "" },
                    new[] { "A_D1_聞き取り_B_1", "会話", "", "B のこと？", "", "", "", "", "", "" },
                    new[] { "SP_D1_1", "会話", "補助ちゃん", "1回目", "", "", "", "", "", "" },
                    new[] { "SP_D1_2", "会話", "補助ちゃん", "2回目", "", "", "", "", "", "" },
                    new[] { "MORNING_D1", "会話", "補助ちゃん", "おはようございます", "", "", "", "", "", "" }),
                ["条件"] = Table(
                    ConditionHeaders,
                    Condition("1", "A", "対話", "", "", "1", dialogueId: "A_D1_対話_2",
                        otherAffection: "B>=2", required: "info_鍵", gain: "info_鍵", ai: "C"),
                    Condition("1", "A", "対話", "", "", "2", dialogueId: "A_D1_対話_1"),
                    Condition("1", "A", "聞き取り", "B", "", "1", dialogueId: "A_D1_聞き取り_B_1"),
                    Condition("1", "", "スマホ", "", "1", "1", dialogueId: "SP_D1_1"),
                    Condition("1", "", "スマホ", "", "2", "1", dialogueId: "SP_D1_2"),
                    Condition("1", "", "朝", "", "", "1", dialogueId: "MORNING_D1", gain: "flag_和解")),
                ["選択肢"] = Table(
                    new[] { "セリフID", "順", "選択肢の文", "必要フラグ", "NGフラグ", "本人の好感度", "他キャラの好感度", "好感度の変化", "獲得フラグ", "AI化", "次のセリフID" },
                    new[] { "A_D1_対話_1", "1", "鍵のことを聞く", "info_鍵", "", "", "B>=1", "1", "flag_和解", "", "A_D1_対話_2" },
                    new[] { "A_D1_対話_1", "2", "黙る", "", "", "", "", "-1", "", "C", "" }),
                ["日別設定"] = Table(
                    new[] { "日", "キャラ", "正解行動", "NG行動" },
                    new[] { "1", "A", "拷問", "交渉" }),
                ["AI判定"] = Table(
                    new[] { "キャラ", "判定日", "回避フラグ" }),
                ["エンディング"] = Table(
                    new[] { "優先度", "判定結果", "種類", "必要フラグ", "NGフラグ", "エンディングID", "一覧に表示する名前" }),
            };
        }

        private static string[] Condition(string day, string character, string action, string hearingTarget, string talkNumber,
            string priority, string dialogueId, string otherAffection = "", string required = "", string gain = "", string ai = "")
        {
            return new[] { day, character, action, hearingTarget, talkNumber, priority, "", otherAffection, "", required, "", gain, ai, dialogueId };
        }

        private static List<List<string>> Table(params string[][] rows)
        {
            return rows.Select(r => r.ToList()).ToList();
        }

        /// <summary> 見出しの名前で、指定したデータ行（1始まり）のセルを書き換える </summary>
        private static void SetCell(List<List<string>> sheet, int dataRow, string header, string value)
        {
            int col = sheet[0].IndexOf(header);
            Assert.GreaterOrEqual(col, 0, $"見出し「{header}」がありません");
            sheet[dataRow][col] = value;
        }

        #endregion
    }
}
#endif
