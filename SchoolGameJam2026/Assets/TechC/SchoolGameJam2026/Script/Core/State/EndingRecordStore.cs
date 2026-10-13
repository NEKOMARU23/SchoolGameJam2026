using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core.State
{
    /// <summary>
    /// 見たエンディングの記録（エンディング一覧用）
    /// セーブ枠とは別のファイル（ending_record.json）に保存するので、どのセーブで見ても記録される
    /// </summary>
    public static class EndingRecordStore
    {
        [Serializable]
        private class RecordData
        {
            public List<string> seenEndingIds = new List<string>();
        }

        private const string FileName = "ending_record.json";

        private static RecordData data;
        private static string filePathOverride;

        /// <summary> 保存先（テストでは別の場所に変える） </summary>
        public static string FilePath => filePathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// 【テスト用】保存先を変える（null で元に戻す）。読み込み済みの記録は捨てる
        /// </summary>
        public static void SetFilePathForTest(string path)
        {
            filePathOverride = path;
            data = null;
        }

        public static bool IsSeen(string endingId)
        {
            return !string.IsNullOrEmpty(endingId) && Data.seenEndingIds.Contains(endingId);
        }

        public static IReadOnlyList<string> SeenEndingIds => Data.seenEndingIds;

        /// <summary> エンディングを見たことを記録してすぐ保存する </summary>
        public static void MarkSeen(string endingId)
        {
            if (string.IsNullOrEmpty(endingId) || Data.seenEndingIds.Contains(endingId)) return;
            Data.seenEndingIds.Add(endingId);
            Save();
        }

        private static RecordData Data
        {
            get
            {
                if (data == null) data = Load();
                return data;
            }
        }

        private static RecordData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonUtility.FromJson<RecordData>(File.ReadAllText(FilePath));
                    if (loaded != null && loaded.seenEndingIds != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EndingRecordStore] エンディングの記録を読み込めませんでした: {e.Message}");
            }
            return new RecordData();
        }

        private static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EndingRecordStore] エンディングの記録を保存できませんでした: {e.Message}");
            }
        }
    }
}
