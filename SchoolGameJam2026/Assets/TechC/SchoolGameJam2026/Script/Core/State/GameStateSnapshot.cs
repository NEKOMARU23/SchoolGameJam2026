using System;
using System.Collections.Generic;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.Core.State
{
    /// <summary>
    /// ある時点のゲームの状態をまとめたデータ
    /// 「今日をやり直す」（朝の状態に戻す）と、セーブ・ロードの両方で使う
    /// JsonUtility でそのまま保存できるよう、辞書ではなくリストで持つ
    /// </summary>
    [Serializable]
    public class GameStateSnapshot
    {
        public int day = 1;

        // --- 取り調べ（InterrogationManager） ---
        public int points;
        public int dailyActions;
        public int todayEarnedPoints;
        public List<string> interrogatedTodayIds = new List<string>();
        public List<ActionHistoryEntry> todayActionHistory = new List<ActionHistoryEntry>();
        public List<ActionCountEntry> actionCounts = new List<ActionCountEntry>();

        // --- キャラクター ---
        public List<CharacterStateEntry> characters = new List<CharacterStateEntry>();

        // --- スマホ ---
        public int smartphoneTalkCount;

        // --- フラグ（キーアイテム・情報・進行フラグ。ON のものだけ） ---
        public List<string> flags = new List<string>();
    }

    /// <summary> その日に、だれにどの行動をしたか </summary>
    [Serializable]
    public class ActionHistoryEntry
    {
        public string characterId;
        public InterrogationActionType action;
    }

    /// <summary> キャラごと・行動ごとの通算回数 </summary>
    [Serializable]
    public class ActionCountEntry
    {
        public string characterId;
        public InterrogationActionType action;
        public int count;
    }

    /// <summary> 1キャラ分の状態 </summary>
    [Serializable]
    public class CharacterStateEntry
    {
        public string characterId;
        public int affection;
        public bool isReleased;
        public bool isAI;
        public bool isConfirmedAI;
        public bool isBasicInfoUnlocked;
        public List<string> unlockedHistoryKeys = new List<string>();
    }
}
