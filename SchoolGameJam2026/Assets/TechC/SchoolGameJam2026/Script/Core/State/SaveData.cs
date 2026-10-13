using System;
using System.Collections.Generic;

namespace TechC.SchoolGameJam2026.Scripts.Core.State
{
    /// <summary>
    /// セーブ枠1つ分のデータ（JsonUtility でそのままファイルに書く）
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;

        // --- セーブ枠の一覧に出す情報 ---
        public string savedAt = "";         // 保存した日時（yyyy/MM/dd HH:mm）
        public int day = 1;
        public int remainingActions;

        // --- ゲームの状態 ---
        public GameStateSnapshot current;   // 保存した時点の状態
        public GameStateSnapshot morning;   // その日の朝の状態（ロード後も「今日をやり直す」で朝に戻れるように）
        public List<LogEntry> log = new List<LogEntry>();
    }

    /// <summary> ログ1件 </summary>
    [Serializable]
    public class LogEntry
    {
        public string speaker = "";
        public string text = "";
    }
}
