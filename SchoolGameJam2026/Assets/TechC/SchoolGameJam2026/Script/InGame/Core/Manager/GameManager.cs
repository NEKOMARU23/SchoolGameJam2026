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
    public struct ProfileUnlockStateBackup
    {
        public bool isBasicUnlocked;
        public List<string> historyKeys;

        public ProfileUnlockStateBackup(bool isBasicUnlocked, List<string> historyKeys)
        {
            this.isBasicUnlocked = isBasicUnlocked;
            this.historyKeys = historyKeys;
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
        [SerializeField] private int maxDays = 5;

        [Header("キャラクター一覧 (A, B, C, D)")]
        [SerializeField] private List<CharacterInstance> allCharacters = new List<CharacterInstance>();

        [Header("日別のシナリオデータリスト (Day1, Day2, ...)")]
        [SerializeField] private List<DayScenarioSO> dayScenarios = new List<DayScenarioSO>();

        #region リセット用バックアップデータ
        private int backupPoints;
        private int backupDailyActions;
        private List<string> backupInterrogatedCharacterIds = new List<string>();
        private Dictionary<string, int> backupAffections = new Dictionary<string, int>();
        
        // ★ ProfileDataSO ではなく キャラクターID をキーにして解禁状態を保持
        private Dictionary<string, ProfileUnlockStateBackup> backupProfileStates = new Dictionary<string, ProfileUnlockStateBackup>();
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

            // 行動回数および本日取り調べたキャラリストをリセット
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.ResetDailyActions(0);
            }

            // 朝の時点の状態をバックアップ保存
            SaveDayBackup();

            OnDayStarted?.Invoke(currentDay);
        }

        /// <summary>
        /// ★ 「今日を最初からやり直す」リセット機能
        /// </summary>
        public void ResetCurrentDay()
        {
            Debug.Log($"<color=yellow>⚠️ {currentDay} 日目の朝の状態にリセットします。</color>");

            // 1. ポイント、行動回数、および「本日行動済みキャラIDリスト」の復元
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.SetPoints(backupPoints);
                InterrogationManager.Instance.ResetDailyActions(backupDailyActions);
                InterrogationManager.Instance.RestoreInterrogatedCharacterIdsToday(backupInterrogatedCharacterIds);
            }

            // 2. キャラクター好感度の復元
            foreach (var character in allCharacters)
            {
                if (character != null && backupAffections.TryGetValue(character.CharacterId, out int restoredAffection))
                {
                    character.SetAffection(restoredAffection);
                }
            }

            // 3. プロフィール解禁状態の復元（★ CharacterInstance 経由で復元）
            foreach (var character in allCharacters)
            {
                if (character != null && backupProfileStates.TryGetValue(character.CharacterId, out var backupState))
                {
                    character.RestoreUnlockStates(backupState.isBasicUnlocked, backupState.historyKeys);
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
            // ポイント、行動回数、行動済みキャラIDリストのバックアップ
            if (InterrogationManager.Instance != null)
            {
                backupPoints = InterrogationManager.Instance.CurrentPoints;
                backupDailyActions = InterrogationManager.Instance.CurrentDailyActions;
                backupInterrogatedCharacterIds = InterrogationManager.Instance.GetInterrogatedCharacterIdsToday();
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

            // プロフィール解禁状態のバックアップ（★ CharacterInstance 経由で取得）
            backupProfileStates.Clear();
            foreach (var character in allCharacters)
            {
                if (character != null && !string.IsNullOrEmpty(character.CharacterId))
                {
                    var (basicUnlocked, historyKeys) = character.GetUnlockStates();
                    backupProfileStates[character.CharacterId] = new ProfileUnlockStateBackup(basicUnlocked, historyKeys);
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
        /// 現在の日を終了し、次の日（朝）へ移行する
        /// </summary>
        public void AdvanceToNextDay()
        {
            currentDay++;

            // 最大日数を超えた場合の処理（ゲームクリアなど）
            if (currentDay > maxDays)
            {
                Debug.Log("<color=gold>全日程が終了しました！ゲームクリア画面へ移行します。</color>");
                return;
            }

            // シナリオが存在するか確認
            DayScenarioSO nextScenario = GetTodayScenario();
            if (nextScenario == null)
            {
                Debug.LogWarning($"Day {currentDay} のシナリオが見つかりません。");
                return;
            }

            // 新しい日の朝を開始
            StartNewDay();
        }
    }
}