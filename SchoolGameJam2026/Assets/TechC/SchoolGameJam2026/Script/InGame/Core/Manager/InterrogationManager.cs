using System;
using System.Collections.Generic;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// 取り調べの実行、判定、ポイント計算、行動制限を管理するマネージャー（Singleton）
    /// </summary>
    public class InterrogationManager : MonoBehaviour
    {
        public static InterrogationManager Instance { get; private set; }

        [Header("ポイント設定")]
        [SerializeField] private int currentPoints = 0;
        [SerializeField] private int successPointReward = 20;
        [SerializeField] private int failPointPenalty = 5;
        [SerializeField] private int fixedHearAboutPoint = 10;

        [Header("好感度調整設定")]
        [SerializeField] private int correctAffectionReward = 1;
        [SerializeField] private int failAffectionPenalty = 1;

        [Header("行動制限設定")]
        [SerializeField] private int dailyActionLimit = 4;
        [SerializeField] private int currentDailyActions = 0;

        // キャラID -> ( アクション種別 -> 通算実行回数 )
        private Dictionary<string, Dictionary<InterrogationActionType, int>> characterActionCounts 
            = new Dictionary<string, Dictionary<InterrogationActionType, int>>();

        private List<string> interrogatedCharacterIdsToday = new List<string>();

        #region プロパティ / リザルトデータ
        public Dictionary<string, InterrogationActionType> TodayActionHistory { get; private set; } = new Dictionary<string, InterrogationActionType>();
        public int TodayEarnedPoints { get; private set; } = 0;

        public int CurrentPoints => currentPoints;
        public int DailyActionLimit => dailyActionLimit;
        public int CurrentDailyActions => currentDailyActions;
        public int RemainingDailyActions => Mathf.Max(0, dailyActionLimit - currentDailyActions);
        public bool IsActionLimitReached => currentDailyActions >= dailyActionLimit;
        #endregion

        #region イベント
        public event Action<int> OnPointsChanged;
        public event Action<int, int> OnActionCountChanged;
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

        public bool HasInterrogatedToday(string characterId) => interrogatedCharacterIdsToday.Contains(characterId);

        public int GetCharacterActionCount(string characterId, InterrogationActionType actionType)
        {
            if (characterActionCounts.TryGetValue(characterId, out var actionMap))
            {
                if (actionMap.TryGetValue(actionType, out int count)) return count;
            }
            return 0;
        }

        /// <summary>
        /// 取り調べアクションを実行する
        /// </summary>
        public string ExecuteAction(CharacterInstance targetChar, InterrogationActionType actionType, string hearingTargetId = "")
        {
            // 1. ガード節（事前チェック）
            if (IsActionLimitReached)
            {
                Debug.LogWarning("[InterrogationManager] 本日の行動上限に達しています。");
                return "（本日の取り調べ可能回数を消化しました）";
            }

            if (HasInterrogatedToday(targetChar.CharacterId))
            {
                Debug.LogWarning($"[InterrogationManager] {targetChar.CharacterId} には本日すでに取り調べを行っています。");
                return "（このキャラクターへの本日の取り調べは既に終了しています）";
            }

            DayScenarioSO todayScenario = GameManager.Instance != null ? GameManager.Instance.GetTodayScenario() : null;
            CharacterDailySetting dailySetting = todayScenario?.GetCharacterSetting(targetChar.CharacterId);
            if (dailySetting == null)
            {
                Debug.LogError($"[InterrogationManager] シナリオ設定が見つかりません。 (ID: {targetChar.CharacterId})");
                return "「……」";
            }

            // 2. 対話テキスト取得と報酬/ペナルティ処理
            Dictionary<string, int> otherAffections = GameManager.Instance?.GetAllCharacterAffections() ?? new Dictionary<string, int>();
            int actionCount = GetCharacterActionCount(targetChar.CharacterId, actionType);

            string responseDialogue = ProcessActionEffect(targetChar, actionType, dailySetting, otherAffections, actionCount, hearingTargetId);

            // 3. 状態更新（カウントアップ・履歴保持）
            IncrementActionCount(targetChar.CharacterId, actionType);
            interrogatedCharacterIdsToday.Add(targetChar.CharacterId);
            TodayActionHistory[targetChar.CharacterId] = actionType;

            currentDailyActions++;
            OnActionCountChanged?.Invoke(currentDailyActions, dailyActionLimit);

            // 4. 夜フェーズ移行チェック
            if (IsActionLimitReached)
            {
                Debug.Log("[InterrogationManager] 行動制限到達。夜フェーズへ移行します。");
                GameManager.Instance?.TransitionToNightPhase();
            }

            return responseDialogue;
        }

        /// <summary>
        /// アクションごとの評価・ポイント/好感度の計算処理
        /// </summary>
        private string ProcessActionEffect(
            CharacterInstance targetChar, 
            InterrogationActionType actionType, 
            CharacterDailySetting dailySetting, 
            Dictionary<string, int> otherAffections, 
            int actionCount, 
            string hearingTargetId)
        {
            if (actionType == InterrogationActionType.HearAbout)
            {
                AddPoints(fixedHearAboutPoint);
                TodayEarnedPoints += fixedHearAboutPoint;
                return dailySetting.GetResponseWithIndex(actionType, targetChar.CurrentAffection, otherAffections, actionCount, hearingTargetId).dialogueText;
            }

            if (actionType == InterrogationActionType.Release || actionType == InterrogationActionType.ConfirmAI)
            {
                return dailySetting.GetResponseWithIndex(actionType, targetChar.CurrentAffection, otherAffections, actionCount).dialogueText;
            }

            // 通常取り調べ（正解/不正解判定）
            var (dialogueText, elementIndex) = dailySetting.GetResponseWithIndex(actionType, targetChar.CurrentAffection, otherAffections, actionCount);
            
            bool isCorrect = (elementIndex == 0 && actionType == dailySetting.correctAction);
            bool isPenaltyAction = (actionType == dailySetting.penaltyAction);

            if (isCorrect)
            {
                AddPoints(successPointReward);
                TodayEarnedPoints += successPointReward;
                if (correctAffectionReward != 0) targetChar.AddAffection(correctAffectionReward);
            }
            else
            {
                ConsumePoints(failPointPenalty);
                TodayEarnedPoints -= failPointPenalty;
                if (isPenaltyAction && failAffectionPenalty != 0) targetChar.AddAffection(-failAffectionPenalty);
            }

            return dialogueText;
        }

        private void IncrementActionCount(string characterId, InterrogationActionType actionType)
        {
            if (!characterActionCounts.ContainsKey(characterId))
            {
                characterActionCounts[characterId] = new Dictionary<InterrogationActionType, int>();
            }

            if (!characterActionCounts[characterId].ContainsKey(actionType))
            {
                characterActionCounts[characterId][actionType] = 0;
            }

            characterActionCounts[characterId][actionType]++;
        }

        #region ポイント操作（バグ修正済み）
        public void AddPoints(int amount)
        {
            if (amount <= 0) return;
            currentPoints += amount;
            OnPointsChanged?.Invoke(currentPoints);
        }

        public void ConsumePoints(int amount)
        {
            if (amount <= 0) return;
            // 修正：代入ではなく減算を行う
            currentPoints = Mathf.Max(0, currentPoints - amount);
            OnPointsChanged?.Invoke(currentPoints);
        }

        public void SetPoints(int value)
        {
            currentPoints = Mathf.Max(0, value);
            OnPointsChanged?.Invoke(currentPoints);
        }

        public bool TrySpendPoints(int cost)
        {
            if (currentPoints >= cost)
            {
                ConsumePoints(cost);
                return true;
            }
            return false;
        }
        #endregion

        #region 日次リセット・復元用
        public void ResetDailyActions(int actionCount = 0)
        {
            currentDailyActions = actionCount;
            interrogatedCharacterIdsToday.Clear();
            TodayActionHistory.Clear();
            TodayEarnedPoints = 0;

            OnActionCountChanged?.Invoke(currentDailyActions, dailyActionLimit);
        }

        public Dictionary<string, Dictionary<InterrogationActionType, int>> GetCharacterActionCounts() => characterActionCounts;

        public void RestoreCharacterActionCounts(Dictionary<string, Dictionary<InterrogationActionType, int>> backupData)
        {
            characterActionCounts = backupData ?? new Dictionary<string, Dictionary<InterrogationActionType, int>>();
        }

        public List<string> GetInterrogatedCharacterIdsToday() => new List<string>(interrogatedCharacterIdsToday);

        public void RestoreInterrogatedCharacterIdsToday(List<string> backupList)
        {
            interrogatedCharacterIdsToday = backupList != null ? new List<string>(backupList) : new List<string>();
        }
        #endregion
    }
}