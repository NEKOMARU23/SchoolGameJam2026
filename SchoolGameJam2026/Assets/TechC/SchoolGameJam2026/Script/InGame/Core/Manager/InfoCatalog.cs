using System.Collections.Generic;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// 資料画面に出す、情報・キーアイテム1つ分のデータ（画面はこれを表示するだけ）
    /// </summary>
    public class InfoEntry
    {
        public string FlagName;
        public string Title;            // 一覧や窓に出す名前（フラグ一覧の「見出し」）
        public string Text;             // 詳しい表示の文（フラグ一覧の「プロフィールに表示する文」）
        public FlagKind Kind;
        public string DisplayTarget;    // A〜D / その他 / アイテム
        public List<string> RelatedCharacters;
        public string Image;            // アイテムの絵の名前（Data/Item）
        public int UnlockCost;          // ポイントで解禁する情報なら、そのポイント（0 なら会話で手に入る）

        public bool IsOwned;            // この周回で持っている（ゲームのフラグが ON）
        public bool IsKnown;            // どこかの周回で一度でも手に入れた（資料画面で読める）
        public bool IsNew;              // この周回で初めて手に入れた（前の周回では手に入れていない）
        public bool IsUnseen;           // この周回で手に入れて、まだ資料画面で見ていない（タブの赤い丸）

        public int AcquiredDay;         // 手に入れた日（この周回で持っていればこの周回の日、なければ初めて手に入れたときの日）
        public string AcquiredMethod = "";
        public int AcquiredPlaythrough; // 手に入れた周回

        public bool IsPointUnlock => UnlockCost > 0;
    }

    /// <summary>
    /// 資料画面に出すデータを、フラグ一覧・この周回の記録・周回をまたぐ記録から作る
    /// 前の周回で手に入れた情報は「読める」だけで、ゲームのフラグ（会話の出し分けなど）には使わない
    /// </summary>
    public static class InfoCatalog
    {
        /// <summary>
        /// そのタブ（A〜D / その他 / アイテム）に出す情報を、新しい順に並べて返す
        /// 並び: この周回で手に入れたもの（新しい順）→ 前の周回で手に入れたもの → まだ手に入れていないもの
        /// </summary>
        public static List<InfoEntry> GetEntries(GameManager game, string displayTarget)
        {
            var owned = new List<(int order, InfoEntry entry)>();
            var known = new List<InfoEntry>();
            var unknown = new List<InfoEntry>();
            if (game == null || game.Scenario == null) return new List<InfoEntry>();

            // この周回で手に入れた順番（新しいほど大きい）
            var orderOf = new Dictionary<string, int>();
            for (int i = 0; i < game.Acquisitions.Count; i++) orderOf[game.Acquisitions[i].flag] = i;

            foreach (ScenarioFlagDefinition def in game.Scenario.flags)
            {
                if (def.kind == FlagKind.Progress || def.displayTarget != displayTarget) continue;

                InfoEntry entry = Create(game, def);
                if (entry.IsOwned) owned.Add((orderOf.TryGetValue(def.name, out int order) ? order : -1, entry));
                else if (entry.IsKnown) known.Add(entry);
                else unknown.Add(entry);
            }

            owned.Sort((a, b) => b.order.CompareTo(a.order));
            known.Sort((a, b) => b.AcquiredPlaythrough != a.AcquiredPlaythrough
                ? b.AcquiredPlaythrough.CompareTo(a.AcquiredPlaythrough)
                : b.AcquiredDay.CompareTo(a.AcquiredDay));

            var result = new List<InfoEntry>();
            foreach (var o in owned) result.Add(o.entry);
            result.AddRange(known);
            result.AddRange(unknown);
            return result;
        }

        /// <summary> 「12 / 20」の数（どこかの周回で手に入れた数 / そのタブの情報の全部の数） </summary>
        public static void Count(GameManager game, string displayTarget, out int known, out int total)
        {
            known = 0;
            total = 0;
            foreach (InfoEntry e in GetEntries(game, displayTarget))
            {
                total++;
                if (e.IsKnown) known++;
            }
        }

        /// <summary> そのタブに、まだ見ていない新しい情報があるか（タブの赤い丸） </summary>
        public static bool HasUnseen(GameManager game, string displayTarget)
        {
            foreach (InfoEntry e in GetEntries(game, displayTarget))
            {
                if (e.IsUnseen) return true;
            }
            return false;
        }

        /// <summary> そのタブを開いたときに呼ぶ。この周回で手に入れた情報を「見た」ことにする </summary>
        public static void MarkSeen(GameManager game, string displayTarget)
        {
            if (game == null) return;
            var flags = new List<string>();
            foreach (InfoEntry e in GetEntries(game, displayTarget))
            {
                if (e.IsOwned) flags.Add(e.FlagName);
            }
            InfoRecordStore.MarkSeen(flags, game.CurrentPlaythrough);
        }

        private static InfoEntry Create(GameManager game, ScenarioFlagDefinition def)
        {
            bool owned = game.Flags.IsOn(def.name);
            bool hasRecord = InfoRecordStore.TryGet(def.name, out InfoRecordStore.Entry record);

            var entry = new InfoEntry
            {
                FlagName = def.name,
                Title = string.IsNullOrEmpty(def.title) ? def.name : def.title,
                Text = def.profileText,
                Kind = def.kind,
                DisplayTarget = def.displayTarget,
                RelatedCharacters = new List<string>(def.relatedCharacters),
                Image = def.image,
                UnlockCost = def.unlockCost,
                IsOwned = owned,
                IsKnown = owned || hasRecord,
                IsNew = owned && hasRecord && record.firstPlaythrough == game.CurrentPlaythrough,
                IsUnseen = owned && hasRecord && record.seenPlaythrough != game.CurrentPlaythrough,
            };

            InfoAcquisition thisRun = null;
            foreach (InfoAcquisition a in game.Acquisitions)
            {
                if (a.flag == def.name) thisRun = a;
            }

            if (owned && thisRun != null)
            {
                entry.AcquiredDay = thisRun.day;
                entry.AcquiredMethod = thisRun.method ?? "";
                entry.AcquiredPlaythrough = game.CurrentPlaythrough;
            }
            else if (hasRecord)
            {
                entry.AcquiredDay = record.firstDay;
                entry.AcquiredMethod = record.firstMethod ?? "";
                entry.AcquiredPlaythrough = record.firstPlaythrough;
            }
            return entry;
        }
    }
}
