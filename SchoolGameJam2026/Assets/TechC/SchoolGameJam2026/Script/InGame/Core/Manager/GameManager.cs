using System;
using System.Collections.Generic;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// バックアップ用のプロフィール解禁状態データ構造体
    /// </summary>
    public readonly struct ProfileUnlockStateBackup
    {
        public bool IsBasicUnlocked { get; }
        public List<string> HistoryKeys { get; }

        public ProfileUnlockStateBackup(bool isBasicUnlocked, List<string> historyKeys)
        {
            IsBasicUnlocked = isBasicUnlocked;
            HistoryKeys = historyKeys != null ? new List<string>(historyKeys) : new List<string>();
        }
    }

    /// <summary>
    /// ゲーム全体の進行管理、日数、および「その日の朝に戻す」リセット機能を管理するマネージャー (Singleton)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("ゲーム進行設定")]
        [Tooltip("現在の進行日数 (1〜)")]
        [SerializeField] private int currentDay = 1;

        [Tooltip("最大日数")]
        [SerializeField] private int maxDays = 7;

        [Header("キャラクター一覧 (A, B, C, D)")]
        [SerializeField] private List<CharacterInstance> allCharacters = new List<CharacterInstance>();

        [Header("日別のシナリオデータリスト (Day1, Day2, ...)")]
        [SerializeField] private List<DayScenarioSO> dayScenarios = new List<DayScenarioSO>();

        #region リセット用バックアップデータ
        private int backupPoints;
        private int backupDailyActions;
        private List<string> backupInterrogatedCharacterIds = new List<string>();
        private Dictionary<string, Dictionary<InterrogationActionType, int>> backupActionCounts = new Dictionary<string, Dictionary<InterrogationActionType, int>>();
        private readonly Dictionary<string, int> backupAffections = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> backupReleased = new Dictionary<string, bool>();
        private readonly Dictionary<string, ProfileUnlockStateBackup> backupProfileStates = new Dictionary<string, ProfileUnlockStateBackup>();
        #endregion

        #region プロパティ
        public int CurrentDay => currentDay;
        public int MaxDays => maxDays;
        public List<CharacterInstance> AllCharacters => allCharacters;
        #endregion

        #region イベント
        public event Action<int> OnDayStarted;
        public event Action OnDayReset;
        public event Action OnNightPhaseStarted;
        #endregion

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            StartNewDay();
        }

        /// <summary> 今日のシナリオデータ(DayScenarioSO)を取得する </summary>
        public DayScenarioSO GetTodayScenario()
        {
            return dayScenarios.Find(s => s != null && s.dayNumber == currentDay);
        }

        /// <summary> 全キャラクターの好感度を辞書で取得する </summary>
        public Dictionary<string, int> GetAllCharacterAffections()
        {
            var dict = new Dictionary<string, int>();
            foreach (var character in allCharacters)
            {
                if (character != null && !string.IsNullOrEmpty(character.CharacterId))
                {
                    dict[character.CharacterId] = character.CurrentAffection;
                }
            }
            return dict;
        }

        /// <summary> 新しい1日（朝）を開始する </summary>
        public void StartNewDay()
        {
            Debug.Log($"<color=cyan>=== {currentDay} 日目の朝が始まりました ===</color>");

            InterrogationManager.Instance?.ResetDailyActions(0);

            // 朝の時点の状態をバックアップ保存
            SaveDayBackup();

            OnDayStarted?.Invoke(currentDay);
        }

        /// <summary> 「今日を最初からやり直す」リセット機能 </summary>
        public void ResetCurrentDay()
        {
            Debug.Log($"<color=yellow>⚠️ {currentDay} 日目の朝の状態にリセットします。</color>");

            // 1. ポイント・行動回数の復元
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.SetPoints(backupPoints);
                InterrogationManager.Instance.ResetDailyActions(backupDailyActions);
                InterrogationManager.Instance.RestoreInterrogatedCharacterIdsToday(backupInterrogatedCharacterIds);
                InterrogationManager.Instance.RestoreCharacterActionCounts(backupActionCounts);
            }

            // 2. キャラクター状態（好感度・解放状態・プロフィール）の復元
            foreach (var character in allCharacters)
            {
                if (character == null || string.IsNullOrEmpty(character.CharacterId)) continue;

                if (backupAffections.TryGetValue(character.CharacterId, out int restoredAffection))
                {
                    character.SetAffection(restoredAffection);
                }

                if (backupReleased.TryGetValue(character.CharacterId, out bool restoredReleased))
                {
                    character.SetReleaseFlag(restoredReleased);
                }

                if (backupProfileStates.TryGetValue(character.CharacterId, out var backupState))
                {
                    character.RestoreUnlockStates(backupState.IsBasicUnlocked, backupState.HistoryKeys);
                }
            }

            OnDayReset?.Invoke();
        }

        /// <summary> 朝の時点の状態を一時記憶する </summary>
        private void SaveDayBackup()
        {
            if (InterrogationManager.Instance != null)
            {
                backupPoints = InterrogationManager.Instance.CurrentPoints;
                backupDailyActions = InterrogationManager.Instance.CurrentDailyActions;
                backupInterrogatedCharacterIds = InterrogationManager.Instance.GetInterrogatedCharacterIdsToday();
                backupActionCounts = InterrogationManager.Instance.GetCharacterActionCounts();
            }

            backupAffections.Clear();
            backupReleased.Clear();
            backupProfileStates.Clear();

            foreach (var character in allCharacters)
            {
                if (character == null || string.IsNullOrEmpty(character.CharacterId)) continue;

                // 好感度・解放状態バックアップ
                backupAffections[character.CharacterId] = character.CurrentAffection;
                backupReleased[character.CharacterId] = character.IsReleased;

                // プロフィール状態バックアップ
                var (basicUnlocked, historyKeys) = character.GetUnlockStates();
                backupProfileStates[character.CharacterId] = new ProfileUnlockStateBackup(basicUnlocked, historyKeys);
            }
        }

        /// <summary> 夜フェーズ（1日の行動終了後）への移行処理 </summary>
        public void TransitionToNightPhase()
        {
            Debug.Log("<color=purple>--- 1日の取り調べが終了しました。夜フェーズへ移行します ---</color>");
            OnNightPhaseStarted?.Invoke();
        }

        /// <summary> 現在の日を終了し、次の日（朝）へ移行する </summary>
        public void AdvanceToNextDay()
        {
            int nextDay = currentDay + 1;

            if (nextDay > maxDays)
            {
                Debug.LogWarning($"最終日（{maxDays}日目）を超えて日付を進めることはできません。");
                return;
            }

            // 日付を進める前に確認し、シナリオ欠落時に行動回数が未リセットのまま進むのを防ぐ
            if (dayScenarios.Find(s => s != null && s.dayNumber == nextDay) == null)
            {
                Debug.LogError($"Day {nextDay} のシナリオが見つかりません。");
                return;
            }

            currentDay = nextDay;
            StartNewDay();
        }

        /// <summary> 現在が最終日かどうか </summary>
        public bool IsFinalDay => currentDay >= maxDays;
    }
}