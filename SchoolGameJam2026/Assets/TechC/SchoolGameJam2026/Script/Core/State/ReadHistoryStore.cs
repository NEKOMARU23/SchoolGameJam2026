using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core.State
{
    /// <summary>
    /// 一度読んだ会話（Excel のセリフID）の記録。スキップの「既読のみ」で使う
    /// セーブ枠とは別のファイル（read_record.json）に保存するので、どのセーブで読んでも既読になる
    /// </summary>
    public static class ReadHistoryStore
    {
        [Serializable]
        private class RecordData
        {
            public List<string> readDialogueIds = new List<string>();
        }

        private const string FileName = "read_record.json";

        private static RecordData data;
        private static HashSet<string> lookup;
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

        public static bool IsRead(string dialogueId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(dialogueId) && lookup.Contains(dialogueId);
        }

        /// <summary> 会話を最後まで読んだことを記録してすぐ保存する </summary>
        public static void MarkRead(string dialogueId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(dialogueId) || !lookup.Add(dialogueId)) return;
            data.readDialogueIds.Add(dialogueId);
            Save();
        }

        private static void EnsureLoaded()
        {
            if (data != null) return;
            data = Load();
            lookup = new HashSet<string>(data.readDialogueIds, StringComparer.Ordinal);
        }

        private static RecordData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonUtility.FromJson<RecordData>(File.ReadAllText(FilePath));
                    if (loaded != null && loaded.readDialogueIds != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReadHistoryStore] 既読の記録を読み込めませんでした: {e.Message}");
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
                Debug.LogWarning($"[ReadHistoryStore] 既読の記録を保存できませんでした: {e.Message}");
            }
        }
    }
}
