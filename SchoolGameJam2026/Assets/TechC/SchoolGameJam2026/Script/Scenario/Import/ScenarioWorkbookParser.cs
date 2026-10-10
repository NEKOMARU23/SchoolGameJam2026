#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.Scenario.Import
{
    /// <summary> 素材の種類（Data の中のフォルダ） </summary>
    public enum AssetKind
    {
        Character,
        Background,
        EventCG,
        Effect,
        SE,
        BGM
    }

    /// <summary> 取り込み時のエラー・警告1件 </summary>
    public class ImportMessage
    {
        public bool IsError;
        public string Sheet;
        public int Row;     // Excel の行番号（1始まり。0 はシート全体）
        public string Text;

        public override string ToString()
        {
            string where = Row > 0 ? $"{Sheet} {Row}行目" : Sheet;
            return $"[{(IsError ? "エラー" : "警告")}] {where}: {Text}";
        }
    }

    /// <summary> 取り込みの結果 </summary>
    public class ImportReport
    {
        public readonly List<ImportMessage> Errors = new List<ImportMessage>();
        public readonly List<ImportMessage> Warnings = new List<ImportMessage>();
        public bool HasErrors => Errors.Count > 0;

        public void Error(string sheet, int row, string text) => Errors.Add(new ImportMessage { IsError = true, Sheet = sheet, Row = row, Text = text });
        public void Warn(string sheet, int row, string text) => Warnings.Add(new ImportMessage { IsError = false, Sheet = sheet, Row = row, Text = text });
    }

    /// <summary>
    /// Excel のシートの文字を ScenarioDatabase のデータに変換し、書き間違いをチェックする
    /// 列は1行目の見出しの名前で探す。1列目が # で始まる行と、空の行は読み飛ばす
    /// </summary>
    public static class ScenarioWorkbookParser
    {
        public const string SheetPages = "本文";
        public const string SheetConditions = "条件";
        public const string SheetDaily = "日別設定";
        public const string SheetFlags = "フラグ一覧";
        public const string SheetAI = "AI判定";
        public const string SheetEndings = "エンディング";

        private const string HiddenCharacter = "非表示";

        private static readonly Dictionary<string, ScenarioAction> Actions = new Dictionary<string, ScenarioAction>
        {
            { "拷問", ScenarioAction.Torture }, { "対話", ScenarioAction.Talk }, { "交渉", ScenarioAction.Negotiate },
            { "聞き取り", ScenarioAction.HearAbout }, { "解放", ScenarioAction.Release }, { "AI確定", ScenarioAction.ConfirmAI },
            { "スマホ", ScenarioAction.Smartphone },
        };

        private static readonly Dictionary<string, PageKind> PageKinds = new Dictionary<string, PageKind>
        {
            { "会話", PageKind.Talk }, { "地の文", PageKind.Narration },
        };

        private static readonly Dictionary<string, FlagKind> FlagKinds = new Dictionary<string, FlagKind>
        {
            { "キーアイテム", FlagKind.KeyItem }, { "情報", FlagKind.Info }, { "進行フラグ", FlagKind.Progress },
        };

        private static readonly Dictionary<FlagKind, string> FlagPrefixes = new Dictionary<FlagKind, string>
        {
            { FlagKind.KeyItem, "item_" }, { FlagKind.Info, "info_" }, { FlagKind.Progress, "flag_" },
        };

        private static readonly Dictionary<string, EndingResult> EndingResults = new Dictionary<string, EndingResult>
        {
            { "全員正解", EndingResult.AllCorrect }, { "人間をAI確定した", EndingResult.ConfirmedHuman },
            { "AIを解放した", EndingResult.ReleasedAI }, { "指定なし", EndingResult.Any },
        };

        private static readonly Dictionary<string, EndingKind> EndingKinds = new Dictionary<string, EndingKind>
        {
            { "クリア", EndingKind.Clear }, { "ゲームオーバー", EndingKind.GameOver },
        };

        private static readonly Regex AffectionPattern = new Regex(@"^\s*([A-Za-z0-9_]+)\s*>=\s*(-?\d+)\s*$");

        /// <summary>
        /// シートを読み取って target に書き込む。エラーがあっても途中まで書き込むので、
        /// 呼び出し側はエラーがあれば保存しないこと
        /// </summary>
        /// <param name="assetExists">素材がフォルダにあるかを調べる（警告用）</param>
        public static ImportReport Parse(
            Dictionary<string, List<List<string>>> sheets,
            ScenarioDatabase target,
            Func<AssetKind, string, bool> assetExists,
            ICollection<string> characterIds,
            int maxDays)
        {
            var report = new ImportReport();
            var context = new Context(report, characterIds, maxDays, assetExists);

            target.dialogues.Clear();
            target.conditions.Clear();
            target.dailySettings.Clear();
            target.flags.Clear();
            target.aiJudgements.Clear();
            target.endings.Clear();
            target.ClearCache();

            // 参照される側（フラグ・本文）から先に読む
            Sheet flags = context.Open(sheets, SheetFlags, "フラグ名", "種類");
            Sheet pages = context.Open(sheets, SheetPages, "セリフID", "種類", "本文");
            Sheet conditions = context.Open(sheets, SheetConditions, "日", "キャラ", "行動", "優先度", "セリフID");
            Sheet daily = context.Open(sheets, SheetDaily, "日", "キャラ", "正解行動");
            Sheet ai = context.Open(sheets, SheetAI, "キャラ", "判定日");
            Sheet endings = context.Open(sheets, SheetEndings, "優先度", "判定結果", "種類", "エンディングID");

            if (flags != null) ParseFlags(flags, target, context);
            if (pages != null) ParsePages(pages, target, context);
            if (conditions != null) ParseConditions(conditions, target, context);
            if (daily != null) ParseDaily(daily, target, context);
            if (ai != null) ParseAI(ai, target, context);
            if (endings != null) ParseEndings(endings, target, context);

            return report;
        }

        #region シートごとの読み取り

        private static void ParseFlags(Sheet sheet, ScenarioDatabase db, Context ctx)
        {
            foreach (Row row in sheet.Rows)
            {
                string name = row["フラグ名"];
                if (name.Length == 0) { ctx.Error(row, "フラグ名が空です"); continue; }
                if (ctx.FlagNames.Contains(name)) { ctx.Error(row, $"フラグ名「{name}」が重複しています"); continue; }

                if (!FlagKinds.TryGetValue(row["種類"], out FlagKind kind))
                {
                    ctx.Error(row, $"種類「{row["種類"]}」は使えません（キーアイテム / 情報 / 進行フラグ）");
                    continue;
                }
                if (!name.StartsWith(FlagPrefixes[kind], StringComparison.Ordinal))
                {
                    ctx.Warn(row, $"{row["種類"]}のフラグ名は「{FlagPrefixes[kind]}」で始めてください（{name}）");
                }

                string target = row["表示先"];
                if (target.Length > 0 && target != "その他" && target != "アイテム" && target != "なし" && !ctx.CharacterIds.Contains(target))
                {
                    ctx.Error(row, $"表示先「{target}」は使えません（A〜D / その他 / アイテム / なし）");
                }

                ctx.FlagNames.Add(name);
                db.flags.Add(new ScenarioFlagDefinition
                {
                    name = name,
                    kind = kind,
                    description = row["説明"],
                    profileText = row["プロフィールに表示する文"],
                    displayTarget = target.Length > 0 ? target : "なし",
                });
            }
        }

        private static void ParsePages(Sheet sheet, ScenarioDatabase db, Context ctx)
        {
            var map = new Dictionary<string, ScenarioDialogue>(StringComparer.Ordinal);
            foreach (Row row in sheet.Rows)
            {
                string id = row["セリフID"];
                if (id.Length == 0) { ctx.Error(row, "セリフIDが空です"); continue; }

                if (!PageKinds.TryGetValue(row["種類"], out PageKind kind))
                {
                    ctx.Error(row, $"種類「{row["種類"]}」は使えません（会話 / 地の文）");
                    continue;
                }

                var page = new ScenarioPage
                {
                    kind = kind,
                    speaker = row["話者"],
                    text = row["本文"],
                    se = row["SE"],
                    character = row["立ち絵"],
                    background = row["背景"],
                    eventCG = row["イベント絵"],
                    effect = row["エフェクト"],
                    bgm = row["BGM"],
                };

                ctx.CheckAsset(row, AssetKind.SE, page.se, "SE");
                if (page.character != HiddenCharacter) ctx.CheckAsset(row, AssetKind.Character, page.character, "立ち絵");
                ctx.CheckAsset(row, AssetKind.Background, page.background, "背景");
                ctx.CheckAsset(row, AssetKind.EventCG, page.eventCG, "イベント絵");
                ctx.CheckAsset(row, AssetKind.Effect, page.effect, "エフェクト");
                ctx.CheckAsset(row, AssetKind.BGM, page.bgm, "BGM");

                if (!map.TryGetValue(id, out ScenarioDialogue dialogue))
                {
                    dialogue = new ScenarioDialogue { id = id };
                    map[id] = dialogue;
                    db.dialogues.Add(dialogue);
                }
                dialogue.pages.Add(page);
            }
            ctx.DialogueIds.UnionWith(map.Keys);
        }

        private static void ParseConditions(Sheet sheet, ScenarioDatabase db, Context ctx)
        {
            var groups = new Dictionary<string, List<(int priority, Row row)>>(StringComparer.Ordinal);

            foreach (Row row in sheet.Rows)
            {
                if (!Actions.TryGetValue(row["行動"], out ScenarioAction action))
                {
                    ctx.Error(row, $"行動「{row["行動"]}」は使えません（拷問 / 対話 / 交渉 / 聞き取り / 解放 / AI確定 / スマホ）");
                    continue;
                }

                var c = new ScenarioConditionRow { action = action, sourceLocation = $"{SheetConditions} {row.Number}行目" };
                bool ok = ctx.ReadDay(row, "日", out c.day);
                ok &= ctx.ReadInt(row, "優先度", out c.priority, required: true, min: 1);

                if (action == ScenarioAction.Smartphone)
                {
                    ok &= ctx.ReadInt(row, "回目", out c.talkNumber, required: true, min: 1);
                }
                else
                {
                    ok &= ctx.ReadCharacter(row, "キャラ", out c.characterId, required: true);
                }

                if (action == ScenarioAction.HearAbout)
                {
                    ok &= ctx.ReadCharacter(row, "聞き取り相手", out c.hearingTargetId, required: true);
                }

                c.hasSelfAffection = row["本人の好感度"].Length > 0;
                if (c.hasSelfAffection) ok &= ctx.ReadInt(row, "本人の好感度", out c.selfAffection, required: true, min: int.MinValue);
                c.hasActionCount = row["行動回数"].Length > 0;
                if (c.hasActionCount) ok &= ctx.ReadInt(row, "行動回数", out c.actionCount, required: true, min: 0);
                ok &= ReadOtherAffections(row, c, ctx);

                c.requiredFlags = ctx.ReadFlags(row, "必要フラグ");
                c.ngFlags = ctx.ReadFlags(row, "NGフラグ");
                c.gainFlags = ctx.ReadFlags(row, "獲得フラグ");
                c.aiTargets = ctx.ReadCharacterList(row, "AI化");

                c.dialogueId = row["セリフID"];
                if (c.dialogueId.Length == 0) { ctx.Error(row, "セリフIDが空です"); ok = false; }
                else if (!ctx.DialogueIds.Contains(c.dialogueId)) { ctx.Error(row, $"セリフID「{c.dialogueId}」が本文シートにありません"); ok = false; }

                if (!ok) continue;
                db.conditions.Add(c);

                string key = $"{c.day}|{c.characterId}|{c.action}|{c.hearingTargetId}|{c.talkNumber}";
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<(int, Row)>();
                list.Add((c.priority, row));
            }

            foreach (var group in groups.Values) ctx.CheckPriorities(group);
        }

        private static bool ReadOtherAffections(Row row, ScenarioConditionRow c, Context ctx)
        {
            string text = row["他キャラの好感度"];
            if (text.Length == 0) return true;

            bool ok = true;
            foreach (string part in text.Split(new[] { ',', '、' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match m = AffectionPattern.Match(part.Replace('＞', '>').Replace('＝', '='));
                if (!m.Success)
                {
                    ctx.Error(row, $"他キャラの好感度「{part.Trim()}」の書き方が違います（例: B>=2）");
                    ok = false;
                    continue;
                }
                string id = m.Groups[1].Value;
                if (!ctx.CharacterIds.Contains(id))
                {
                    ctx.Error(row, $"他キャラの好感度のキャラ「{id}」は使えません（A〜D）");
                    ok = false;
                    continue;
                }
                c.otherAffections.Add(new AffectionCondition { characterId = id, minValue = int.Parse(m.Groups[2].Value) });
            }
            return ok;
        }

        private static void ParseDaily(Sheet sheet, ScenarioDatabase db, Context ctx)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Row row in sheet.Rows)
            {
                var s = new ScenarioDailySetting();
                bool ok = ctx.ReadDay(row, "日", out s.day);
                ok &= ctx.ReadCharacter(row, "キャラ", out s.characterId, required: true);
                ok &= ctx.ReadInterrogationAction(row, "正解行動", out s.correctAction, required: true, out _);
                ok &= ctx.ReadInterrogationAction(row, "NG行動", out s.ngAction, required: false, out s.hasNgAction);
                if (!ok) continue;

                if (!seen.Add($"{s.day}|{s.characterId}"))
                {
                    ctx.Error(row, $"{s.day}日目の{s.characterId}の設定が重複しています");
                    continue;
                }
                db.dailySettings.Add(s);
            }
        }

        private static void ParseAI(Sheet sheet, ScenarioDatabase db, Context ctx)
        {
            foreach (Row row in sheet.Rows)
            {
                var j = new ScenarioAIJudgement();
                bool ok = ctx.ReadCharacter(row, "キャラ", out j.characterId, required: true);
                ok &= ctx.ReadDay(row, "判定日", out j.day);
                j.avoidFlags = ctx.ReadFlags(row, "回避フラグ");
                if (ok) db.aiJudgements.Add(j);
            }
        }

        private static void ParseEndings(Sheet sheet, ScenarioDatabase db, Context ctx)
        {
            var priorities = new List<(int, Row)>();
            foreach (Row row in sheet.Rows)
            {
                var e = new ScenarioEnding();
                bool ok = ctx.ReadInt(row, "優先度", out e.priority, required: true, min: 1);

                if (!EndingResults.TryGetValue(row["判定結果"], out e.result))
                {
                    ctx.Error(row, $"判定結果「{row["判定結果"]}」は使えません（全員正解 / 人間をAI確定した / AIを解放した / 指定なし）");
                    ok = false;
                }
                if (!EndingKinds.TryGetValue(row["種類"], out e.kind))
                {
                    ctx.Error(row, $"種類「{row["種類"]}」は使えません（クリア / ゲームオーバー）");
                    ok = false;
                }

                e.requiredFlags = ctx.ReadFlags(row, "必要フラグ");
                e.ngFlags = ctx.ReadFlags(row, "NGフラグ");
                e.endingId = row["エンディングID"];
                e.listName = row["一覧に表示する名前"];

                if (e.endingId.Length == 0) { ctx.Error(row, "エンディングIDが空です"); ok = false; }
                else if (!ctx.DialogueIds.Contains(e.endingId)) { ctx.Error(row, $"エンディングID「{e.endingId}」が本文シートにありません"); ok = false; }

                if (!ok) continue;
                db.endings.Add(e);
                priorities.Add((e.priority, row));
            }
            ctx.CheckPriorities(priorities);
            db.endings.Sort((a, b) => a.priority.CompareTo(b.priority));
        }

        #endregion

        #region シートと行

        private class Row
        {
            public int Number;
            public Sheet Sheet;
            public List<string> Cells;

            /// <summary> 見出しの名前でセルの文字を取り出す（列がない・空なら ""） </summary>
            public string this[string header]
            {
                get
                {
                    if (!Sheet.Columns.TryGetValue(header, out int col) || col >= Cells.Count) return "";
                    return (Cells[col] ?? "").Trim();
                }
            }
        }

        private class Sheet
        {
            public string Name;
            public Dictionary<string, int> Columns = new Dictionary<string, int>(StringComparer.Ordinal);
            public List<Row> Rows = new List<Row>();
        }

        private class Context
        {
            public readonly ImportReport Report;
            public readonly HashSet<string> CharacterIds;
            public readonly int MaxDays;
            public readonly Func<AssetKind, string, bool> AssetExists;
            public readonly HashSet<string> FlagNames = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> DialogueIds = new HashSet<string>(StringComparer.Ordinal);

            public Context(ImportReport report, ICollection<string> characterIds, int maxDays, Func<AssetKind, string, bool> assetExists)
            {
                Report = report;
                CharacterIds = new HashSet<string>(characterIds, StringComparer.Ordinal);
                MaxDays = maxDays;
                AssetExists = assetExists;
            }

            public Sheet Open(Dictionary<string, List<List<string>>> sheets, string name, params string[] requiredHeaders)
            {
                if (!sheets.TryGetValue(name, out List<List<string>> raw))
                {
                    Report.Error(name, 0, "シートがありません");
                    return null;
                }

                var sheet = new Sheet { Name = name };
                if (raw.Count > 0)
                {
                    for (int i = 0; i < raw[0].Count; i++)
                    {
                        string header = (raw[0][i] ?? "").Trim();
                        if (header.Length > 0 && !sheet.Columns.ContainsKey(header)) sheet.Columns[header] = i;
                    }
                }

                bool missing = false;
                foreach (string header in requiredHeaders)
                {
                    if (!sheet.Columns.ContainsKey(header))
                    {
                        Report.Error(name, 1, $"見出し「{header}」の列がありません");
                        missing = true;
                    }
                }
                if (missing) return null;

                for (int i = 1; i < raw.Count; i++)
                {
                    List<string> cells = raw[i] ?? new List<string>();
                    bool empty = cells.TrueForAll(c => string.IsNullOrWhiteSpace(c));
                    if (empty) continue;
                    if (cells.Count > 0 && (cells[0] ?? "").TrimStart().StartsWith("#", StringComparison.Ordinal)) continue; // コメント行
                    sheet.Rows.Add(new Row { Number = i + 1, Sheet = sheet, Cells = cells });
                }
                return sheet;
            }

            public void Error(Row row, string text) => Report.Error(row.Sheet.Name, row.Number, text);
            public void Warn(Row row, string text) => Report.Warn(row.Sheet.Name, row.Number, text);

            public bool ReadInt(Row row, string header, out int value, bool required, int min)
            {
                value = 0;
                string text = row[header];
                if (text.Length == 0)
                {
                    if (required) Error(row, $"{header}が空です");
                    return !required;
                }
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) || Math.Abs(d - Math.Round(d)) > 1e-9)
                {
                    Error(row, $"{header}「{text}」は整数で書いてください");
                    return false;
                }
                value = (int)Math.Round(d);
                if (value < min)
                {
                    Error(row, $"{header}は{min}以上にしてください（{value}）");
                    return false;
                }
                return true;
            }

            public bool ReadDay(Row row, string header, out int day)
            {
                if (!ReadInt(row, header, out day, required: true, min: 1)) return false;
                if (day > MaxDays)
                {
                    Error(row, $"{header}は1〜{MaxDays}にしてください（{day}）");
                    return false;
                }
                return true;
            }

            public bool ReadCharacter(Row row, string header, out string id, bool required)
            {
                id = row[header];
                if (id.Length == 0)
                {
                    if (required) Error(row, $"{header}が空です");
                    return !required;
                }
                if (!CharacterIds.Contains(id))
                {
                    Error(row, $"{header}「{id}」は使えません（A〜D）");
                    return false;
                }
                return true;
            }

            public List<string> ReadCharacterList(Row row, string header)
            {
                var list = new List<string>();
                foreach (string id in SplitList(row[header]))
                {
                    if (CharacterIds.Contains(id)) list.Add(id);
                    else Error(row, $"{header}のキャラ「{id}」は使えません（A〜D）");
                }
                return list;
            }

            public bool ReadInterrogationAction(Row row, string header, out InterrogationActionType action, bool required, out bool hasValue)
            {
                action = InterrogationActionType.Talk;
                string text = row[header];
                hasValue = text.Length > 0;
                if (!hasValue)
                {
                    if (required) Error(row, $"{header}が空です");
                    return !required;
                }
                if (!Actions.TryGetValue(text, out ScenarioAction a) || a == ScenarioAction.Smartphone)
                {
                    Error(row, $"{header}「{text}」は使えません（拷問 / 対話 / 交渉 / 聞き取り / 解放 / AI確定）");
                    return false;
                }
                action = (InterrogationActionType)(int)a;
                return true;
            }

            /// <summary> カンマ区切りのフラグを読み、フラグ一覧にない名前はエラーにする </summary>
            public List<string> ReadFlags(Row row, string header)
            {
                var list = new List<string>();
                foreach (string name in SplitList(row[header]))
                {
                    if (!FlagNames.Contains(name)) Error(row, $"{header}の「{name}」がフラグ一覧にありません（打ち間違い？）");
                    list.Add(name);
                }
                return list;
            }

            public void CheckAsset(Row row, AssetKind kind, string name, string label)
            {
                if (string.IsNullOrEmpty(name) || AssetExists == null) return;
                if (!AssetExists(kind, name)) Warn(row, $"{label}「{name}」の素材が Data フォルダにありません（素材がそろうまでは表示・再生されません）");
            }

            /// <summary> 優先度が 1, 2, 3 … と抜け・重複なく並んでいるか </summary>
            public void CheckPriorities(List<(int priority, Row row)> group)
            {
                group.Sort((a, b) => a.priority.CompareTo(b.priority));
                for (int i = 0; i < group.Count; i++)
                {
                    int expected = i + 1;
                    if (group[i].priority == expected) continue;

                    bool duplicated = i > 0 && group[i].priority == group[i - 1].priority;
                    Error(group[i].row, duplicated
                        ? $"優先度 {group[i].priority} が同じ場面の中で重複しています"
                        : $"優先度が抜けています（{expected} がなく {group[i].priority} があります）");
                    return;
                }
            }

            private static IEnumerable<string> SplitList(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) yield break;
                foreach (string part in text.Split(new[] { ',', '、' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length > 0) yield return trimmed;
                }
            }
        }

        #endregion
    }
}
#endif
