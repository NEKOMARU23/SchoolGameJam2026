using System;
using System.Collections.Generic;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
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
        [SerializeField] private int maxDays = 5;

        [Header("キャラクター一覧 (A, B, C, D)")]
        [SerializeField] private List<CharacterInstance> allCharacters = new List<CharacterInstance>();

        [Header("日別のシナリオデータリスト (Day1, Day2, ...)")]
        [SerializeField] private List<DayScenarioSO> dayScenarios = new List<DayScenarioSO>();

        #region リセット用バックアップデータ
        private int backupPoints;
        private int backupDailyActions;
        private Dictionary<string, int> backupAffections = new Dictionary<string, int>();
        private Dictionary<ProfileDataSO, List<bool>> backupProfileStates = new Dictionary<ProfileDataSO, List<bool>>();
        #endregion

        #region プロパティ
        /// <summary> 現在の日数 </summary>
        public int CurrentDay => currentDay;

        /// <summary> 最大日数 </summary>
        public int MaxDays => maxDays;

        /// <summary> 全キャラクターリスト </summary>
        public List<CharacterInstance> AllCharacters => allCharacters;
        #endregion

        #region イベント
        /// <summary> 新しい日が始まった時のイベント (引数: 新しい日数) </summary>
        public event Action<int> OnDayStarted;

        /// <summary> 今日をリセットした時のイベント </summary>
        public event Action OnDayReset;

        /// <summary> 夜フェーズ（状況変化）へ移行した時のイベント </summary>
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

        /// <summary>
        /// 今日のシナリオデータ(DayScenarioSO)を取得する
        /// </summary>
        public DayScenarioSO GetTodayScenario()
        {
            return dayScenarios.Find(s => s.dayNumber == currentDay);
        }

        /// <summary>
        /// 全キャラクターの好感度を辞書（Key: ID, Value: 好感度）で取得する
        /// </summary>
        public Dictionary<string, int> GetAllCharacterAffections()
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            foreach (var character in allCharacters)
            {
                if (character != null && !string.IsNullOrEmpty(character.CharacterId))
                {
                    dict[character.CharacterId] = character.CurrentAffection;
                }
            }
            return dict;
        }

        /// <summary>
        /// 新しい1日（朝）を開始する
        /// </summary>
        public void StartNewDay()
        {
            Debug.Log($"<color=cyan>=== {currentDay} 日目の朝が始まりました ===</color>");

            // 行動回数をリセット
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.ResetDailyActions(0);
            }

            // ★ 朝の時点の状態をバックアップ保存
            SaveDayBackup();

            OnDayStarted?.Invoke(currentDay);
        }

        /// <summary>
        /// ★ 「今日を最初からやり直す」リセット機能
        /// </summary>
        public void ResetCurrentDay()
        {
            Debug.Log($"<color=yellow>⚠️ {currentDay} 日目の朝の状態にリセットします。</color>");

            // 1. ポイントの復元
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.SetPoints(backupPoints);
                InterrogationManager.Instance.ResetDailyActions(backupDailyActions);
            }

            // 2. キャラクター好感度の復元
            foreach (var character in allCharacters)
            {
                if (character != null && backupAffections.TryGetValue(character.CharacterId, out int restoredAffection))
                {
                    character.SetAffection(restoredAffection);
                }
            }

            // 3. プロフィール解禁状態の復元
            foreach (var pair in backupProfileStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.RestoreUnlockStates(pair.Value);
                }
            }

            // 4. UI等へリセット完了を通知
            OnDayReset?.Invoke();
        }

        /// <summary>
        /// 朝の時点の状態を一時記憶する内部メソッド
        /// </summary>
        private void SaveDayBackup()
        {
            // ポイントと行動回数のバックアップ
            if (InterrogationManager.Instance != null)
            {
                backupPoints = InterrogationManager.Instance.CurrentPoints;
                backupDailyActions = InterrogationManager.Instance.CurrentDailyActions;
            }

            // 好感度のバックアップ
            backupAffections.Clear();
            foreach (var character in allCharacters)
            {
                if (character != null)
                {
                    backupAffections[character.CharacterId] = character.CurrentAffection;
                }
            }

            // プロフィール解禁状態のバックアップ
            backupProfileStates.Clear();
            foreach (var character in allCharacters)
            {
                if (character != null && character.ProfileData != null)
                {
                    backupProfileStates[character.ProfileData] = character.ProfileData.GetUnlockStates();
                }
            }
        }

        /// <summary>
        /// 夜フェーズ（1日の行動終了後）への移行処理
        /// </summary>
        public void TransitionToNightPhase()
        {
            Debug.Log("<color=purple>--- 1日の取り調べが終了しました。夜フェーズへ移行します ---</color>");
            OnNightPhaseStarted?.Invoke();
        }

        /// <summary>
        /// 次の日に進む
        /// </summary>
        public void AdvanceToNextDay()
        {
            if (currentDay < maxDays)
            {
                currentDay++;
                StartNewDay();
            }
            else
            {
                Debug.Log("規定日数に達しました。判定フェーズ（エンディング）へ移行します。");
                // TODO: 最終判定・勝敗処理
            }
        }
    }
}