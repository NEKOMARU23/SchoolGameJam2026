using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core.State
{
    /// <summary>
    /// 周回をまたいで残る、手に入れた情報（キーアイテム・情報）の記録
    /// セーブ枠とは別のファイル（info_record.json）に保存するので、どのセーブで手に入れても残る
    /// ゲームのフラグ（会話の出し分けなど）には使わない。資料画面に「前の周回で手に入れた情報」を出すためだけに使う
    /// </summary>
    public static class InfoRecordStore
    {
        /// <summary> 情報1つ分の記録 </summary>
        [Serializable]
        public class Entry
        {
            public string flag = "";
            public int firstPlaythrough;    // 初めて手に入れた周回（1, 2, …）
            public int firstDay;            // そのときの日
            public string firstMethod = ""; // そのときの手に入れ方（例: ココアに対話）
            public int seenPlaythrough;     // 資料画面で最後に見た周回（0 ならまだ見ていない）
        }

        [Serializable]
        private class RecordData
        {
            public int completedPlaythroughs;   // エンディングまで行った周回の数
            public List<Entry> entries = new List<Entry>();
        }

        private const string FileName = "info_record.json";

        private static RecordData data;
        private static Dictionary<string, Entry> lookup;
        private static string filePathOverride;

        /// <summary> 保存先（テストでは別の場所に変える） </summary>
        public static string FilePath => filePathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

        /// <summary> 【テスト用】保存先を変える（null で元に戻す）。読み込み済みの記録は捨てる </summary>
        public static void SetFilePathForTest(string path)
        {
            filePathOverride = path;
            data = null;
            lookup = null;
        }

        /// <summary> エンディングまで行った周回の数（はじめからの周回は、これ + 1） </summary>
        public static int CompletedPlaythroughs
        {
            get { EnsureLoaded(); return data.completedPlaythroughs; }
        }

        /// <summary> 一度でも手に入れたことがあれば、その記録を返す </summary>
        public static bool TryGet(string flag, out Entry entry)
        {
            EnsureLoaded();
            entry = null;
            return !string.IsNullOrEmpty(flag) && lookup.TryGetValue(flag, out entry);
        }

        /// <summary>
        /// 手に入れたことを記録してすぐ保存する（初めてのときだけ。2回目以降は最初の記録を残す）
        /// </summary>
        public static void RecordAcquired(string flag, int playthrough, int day, string method)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(flag) || lookup.ContainsKey(flag)) return;

            var entry = new Entry { flag = flag, firstPlaythrough = playthrough, firstDay = day, firstMethod = method ?? "" };
            data.entries.Add(entry);
            lookup[flag] = entry;
            Save();
        }

        /// <summary> 資料画面で見たことを記録する（タブの赤い丸を消すのに使う） </summary>
        public static void MarkSeen(IEnumerable<string> flags, int playthrough)
        {
            EnsureLoaded();
            bool changed = false;
            foreach (string flag in flags)
            {
                if (flag != null && lookup.TryGetValue(flag, out Entry entry) && entry.seenPlaythrough != playthrough)
                {
                    entry.seenPlaythrough = playthrough;
                    changed = true;
                }
            }
            if (changed) Save();
        }

        /// <summary> エンディングまで行ったことを記録する（次に「はじめから」を始めると、次の周回になる） </summary>
        public static void CompletePlaythrough(int playthrough)
        {
            EnsureLoaded();
            if (playthrough <= data.completedPlaythroughs) return;
            data.completedPlaythroughs = playthrough;
            Save();
        }

        private static void EnsureLoaded()
        {
            if (data != null) return;
            data = Load();
            lookup = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (Entry e in data.entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.flag)) lookup[e.flag] = e;
            }
        }

        private static RecordData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonUtility.FromJson<RecordData>(File.ReadAllText(FilePath));
                    if (loaded != null && loaded.entries != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InfoRecordStore] 手に入れた情報の記録を読み込めませんでした: {e.Message}");
            }
            return new RecordData();
        }

        private static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(FilePath, JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InfoRecordStore] 手に入れた情報の記録を保存できませんでした: {e.Message}");
            }
        }
    }
}
