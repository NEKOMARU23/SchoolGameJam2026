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
        /// 指定キャラに対して本日アクション可能か判定する
        /// </summary>
        /// <param name="rejectReason">不可の場合にプレイヤーへ表示する理由</param>
        public bool CanExecuteAction(CharacterInstance targetChar, out string rejectReason)
        {
            if (targetChar == null)
            {
                rejectReason = null;
                return false;
            }

            if (targetChar.IsReleased)
            {
                rejectReason = "（このキャラクターはすでに解放されているため、取り調べできません）";
                return false;
            }

            if (IsActionLimitReached)
            {
                rejectReason = "（本日の取り調べ可能回数を消化しました）";
                return false;
            }

            if (HasInterrogatedToday(targetChar.CharacterId))
            {
                rejectReason = "（このキャラクターへの本日の取り調べは既に終了しています）";
                return false;
            }

            rejectReason = null;
            return true;
        }

        /// <summary>
        /// 取り調べアクションを実行する
        /// AI確定は行動回数を消費しないため <see cref="GetConfirmAIDialogue"/> を使う
        /// </summary>
        public string ExecuteAction(CharacterInstance targetChar, InterrogationActionType actionType, string hearingTargetId = "")
        {
            // 1. ガード節（事前チェック）
            if (targetChar == null) return "";

            if (!CanExecuteAction(targetChar, out string rejectReason))
            {
                Debug.LogWarning($"[InterrogationManager] {targetChar.CharacterId} へのアクションは実行できません: {rejectReason}");
                return rejectReason;
            }

            CharacterDailySetting dailySetting = GetTodaySetting(targetChar.CharacterId);
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

            // 4. 解放はセリフ取得・行動回数の計上が済んでから確定する
            if (actionType == InterrogationActionType.Release)
            {
                targetChar.SetReleaseFlag(true);
                if (targetChar.IsAI)
                {
                    Debug.LogWarning($"<color=red>【警告】AIである {targetChar.CharacterId} が解放されました。</color>");
                }
            }

            // 5. 夜フェーズ移行チェック
            if (IsActionLimitReached)
            {
                Debug.Log("[InterrogationManager] 行動制限到達。夜フェーズへ移行します。");
                GameManager.Instance?.TransitionToNightPhase();
            }

            return responseDialogue;
        }

        /// <summary>
        /// AI確定時のセリフを取得する（最終判定のため行動回数・当日の取り調べ済みに関係なく実行できる）
        /// </summary>
        public string GetConfirmAIDialogue(CharacterInstance targetChar)
        {
            if (targetChar == null) return "";

            CharacterDailySetting dailySetting = GetTodaySetting(targetChar.CharacterId);
            if (dailySetting == null) return "";

            Dictionary<string, int> otherAffections = GameManager.Instance?.GetAllCharacterAffections() ?? new Dictionary<string, int>();
            int actionCount = GetCharacterActionCount(targetChar.CharacterId, InterrogationActionType.ConfirmAI);
            return dailySetting.GetResponse(InterrogationActionType.ConfirmAI, targetChar.CurrentAffection, otherAffections, actionCount);
        }

        /// <summary>
        /// ポイントを消費してプロフィール項目を解禁する
        /// </summary>
        /// <param name="itemKey"><see cref="ProfileDataSO.BasicInfoKey"/>（基礎情報）または過去情報の itemKey</param>
        /// <returns>解禁できた場合 true（解禁済み・ポイント不足・項目なしの場合は false）</returns>
        public bool TryUnlockProfile(CharacterInstance targetChar, string itemKey)
        {
            if (targetChar == null || targetChar.ProfileData == null || string.IsNullOrEmpty(itemKey)) return false;

            // --- 1. 基礎情報 ---
            if (itemKey.Equals(ProfileDataSO.BasicInfoKey, StringComparison.OrdinalIgnoreCase))
            {
                if (targetChar.IsBasicInfoUnlocked) return false;

                int cost = targetChar.ProfileData.basicInfo != null ? targetChar.ProfileData.basicInfo.unlockCost : 0;
                if (!TrySpendPoints(cost)) return false;

                targetChar.SetBasicInfoUnlocked(true);
                return true;
            }

            // --- 2. 過去情報 ---
            if (targetChar.IsHistoryUnlocked(itemKey)) return false;

            ProfileItem item = targetChar.ProfileData.historyInfoList?.Find(i => i.itemKey == itemKey);
            if (item == null)
            {
                Debug.LogWarning($"[InterrogationManager] {targetChar.CharacterId} のプロフィールに itemKey '{itemKey}' が存在しません。");
                return false;
            }

            if (!TrySpendPoints(item.unlockCost)) return false;

            targetChar.UnlockHistoryItem(itemKey);
            return true;
        }

        private CharacterDailySetting GetTodaySetting(string characterId)
        {
            DayScenarioSO todayScenario = GameManager.Instance != null ? GameManager.Instance.GetTodayScenario() : null;
            return todayScenario?.GetCharacterSetting(characterId);
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
                // 所持ポイントは0未満にならないため、実際に減った分だけを計上する
                TodayEarnedPoints -= ConsumePoints(failPointPenalty);
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

        #region ポイント操作
        public void AddPoints(int amount)
        {
            if (amount <= 0) return;
            currentPoints += amount;
            OnPointsChanged?.Invoke(currentPoints);
        }

        /// <summary>
        /// ポイントを消費する（0未満にはならない）
        /// </summary>
        /// <returns>実際に減少したポイント</returns>
        public int ConsumePoints(int amount)
        {
            if (amount <= 0) return 0;
            int consumed = Mathf.Min(amount, currentPoints);
            currentPoints -= consumed;
            OnPointsChanged?.Invoke(currentPoints);
            return consumed;
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

        /// <summary> キャラ別アクション通算回数のコピーを取得する（バックアップ用） </summary>
        public Dictionary<string, Dictionary<InterrogationActionType, int>> GetCharacterActionCounts() => CopyActionCounts(characterActionCounts);

        public void RestoreCharacterActionCounts(Dictionary<string, Dictionary<InterrogationActionType, int>> backupData)
        {
            characterActionCounts = CopyActionCounts(backupData);
        }

        private static Dictionary<string, Dictionary<InterrogationActionType, int>> CopyActionCounts(Dictionary<string, Dictionary<InterrogationActionType, int>> source)
        {
            var copy = new Dictionary<string, Dictionary<InterrogationActionType, int>>();
            if (source == null) return copy;

            foreach (var pair in source)
            {
                copy[pair.Key] = new Dictionary<InterrogationActionType, int>(pair.Value);
            }
            return copy;
        }

        public List<string> GetInterrogatedCharacterIdsToday() => new List<string>(interrogatedCharacterIdsToday);

        public void RestoreInterrogatedCharacterIdsToday(List<string> backupList)
        {
            interrogatedCharacterIdsToday = backupList != null ? new List<string>(backupList) : new List<string>();
        }
        #endregion
    }
}