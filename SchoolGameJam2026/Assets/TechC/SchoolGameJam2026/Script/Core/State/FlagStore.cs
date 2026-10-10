using System;
using System.Collections.Generic;

namespace TechC.SchoolGameJam2026.Scripts.Core.State
{
    /// <summary>
    /// キーアイテム・情報・進行フラグを「名前付きの ON / OFF」として記録する
    /// 名前の先頭で種類を表す: item_（キーアイテム）/ info_（情報）/ flag_（進行フラグ）
    /// </summary>
    public class FlagStore
    {
        public const string ItemPrefix = "item_";
        public const string InfoPrefix = "info_";
        public const string FlagPrefix = "flag_";

        private readonly HashSet<string> onFlags = new HashSet<string>(StringComparer.Ordinal);

        /// <summary> フラグが ON / OFF に変わったときに通知（名前, ON になったか） </summary>
        public event Action<string, bool> OnFlagChanged;

        /// <summary> ON になっているフラグの数 </summary>
        public int Count => onFlags.Count;

        public bool IsOn(string name)
        {
            return !string.IsNullOrEmpty(name) && onFlags.Contains(Normalize(name));
        }

        /// <summary>
        /// フラグを ON にする
        /// </summary>
        /// <returns>OFF から ON に変わった場合 true（すでに ON なら false）</returns>
        public bool TurnOn(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string key = Normalize(name);
            if (!onFlags.Add(key)) return false;
            OnFlagChanged?.Invoke(key, true);
            return true;
        }

        /// <summary>
        /// フラグを OFF にする（デバッグメニューやリセット用。通常の進行では OFF にしない）
        /// </summary>
        /// <returns>ON から OFF に変わった場合 true</returns>
        public bool TurnOff(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string key = Normalize(name);
            if (!onFlags.Remove(key)) return false;
            OnFlagChanged?.Invoke(key, false);
            return true;
        }

        /// <summary> 書いたフラグがすべて ON か（空なら true）。条件シートの「必要フラグ」用 </summary>
        public bool AreAllOn(IEnumerable<string> names)
        {
            if (names == null) return true;
            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name) && !IsOn(name)) return false;
            }
            return true;
        }

        /// <summary> 書いたフラグがすべて OFF か（空なら true）。条件シートの「NGフラグ」用 </summary>
        public bool AreAllOff(IEnumerable<string> names)
        {
            if (names == null) return true;
            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name) && IsOn(name)) return false;
            }
            return true;
        }

        /// <summary> ON のフラグの一覧（名前順）。セーブ・スナップショット用 </summary>
        public List<string> ToList()
        {
            var list = new List<string>(onFlags);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>
        /// 一覧の状態に置き換える（リセット・ロード用）。変化したフラグごとに通知する
        /// </summary>
        public void RestoreFrom(IEnumerable<string> names)
        {
            var next = new HashSet<string>(StringComparer.Ordinal);
            if (names != null)
            {
                foreach (string name in names)
                {
                    if (!string.IsNullOrWhiteSpace(name)) next.Add(Normalize(name));
                }
            }

            foreach (string name in new List<string>(onFlags))
            {
                if (!next.Contains(name)) TurnOff(name);
            }
            foreach (string name in next)
            {
                TurnOn(name);
            }
        }

        /// <summary>
        /// カンマ区切りの文字列をフラグ名の一覧にする（例: "item_鍵, info_アリバイ"）
        /// </summary>
        public static List<string> ParseList(string commaSeparated)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(commaSeparated)) return list;

            foreach (string part in commaSeparated.Split(new[] { ',', '、' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0) list.Add(name);
            }
            return list;
        }

        private static string Normalize(string name)
        {
            return name.Trim();
        }
    }
}
