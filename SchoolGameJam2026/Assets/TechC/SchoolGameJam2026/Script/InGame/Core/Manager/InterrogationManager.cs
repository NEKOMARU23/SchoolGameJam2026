using System;
using System.Collections.Generic;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// 取り調べの行動の結果として表示するもの（Excel の会話か、短い文のどちらか）
    /// </summary>
    public class ActionOutcome
    {
        /// <summary> Excel の本文シートの会話（なければ null） </summary>
        public ScenarioDialogue Dialogue { get; private set; }

        /// <summary> 会話がないときに表示する短い文（行動できない理由など） </summary>
        public string Text { get; private set; } = "";

        /// <summary> この行動で入ったポイント（0以上。画面で「+◯pt」と出す） </summary>
        public int EarnedPoints { get; private set; }

        public bool HasDialogue => Dialogue != null && Dialogue.pages.Count > 0;

        public static ActionOutcome FromDialogue(ScenarioDialogue dialogue, int earnedPoints = 0) => new ActionOutcome { Dialogue = dialogue, EarnedPoints = earnedPoints };
        public static ActionOutcome FromText(string text, int earnedPoints = 0) => new ActionOutcome { Text = text ?? "", EarnedPoints = earnedPoints };
    }

    /// <summary>
    /// 取り調べの実行、判定、ポイント計算、行動制限を管理するマネージャー（Singleton）
    /// </summary>
    public class InterrogationManager : MonoBehaviour
    {
        public static InterrogationManager Instance { get; private set; }

        [Header("ポイント設定（行動で入るポイントは三段階。どれも0以上）")]
        [SerializeField] private int currentPoints = 0;
        [Tooltip("成功: 日別設定の「正解行動」をしたとき")]
        [SerializeField, Min(0)] private int successPointReward = 20;
        [Tooltip("普通: 正解行動でも NG行動でもないとき")]
        [SerializeField, Min(0)] private int normalPointReward = 10;
        [Tooltip("最低: 日別設定の「NG行動」をしたとき")]
        [SerializeField, Min(0)] private int ngPointReward = 0;
        [SerializeField, Min(0)] private int fixedHearAboutPoint = 10;

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
        /// 解放・AI確定（最終日の判定）の行動か
        /// </summary>
        public static bool IsJudgementAction(InterrogationActionType actionType)
        {
            return actionType == InterrogationActionType.Release || actionType == InterrogationActionType.ConfirmAI;
        }

        /// <summary>
        /// 指定キャラに対して本日その行動ができるか判定する
        /// 最終日は判定（解放・AI確定）だけ、それ以外の日は取り調べ（拷問・対話・交渉・聞き取り）だけができる
        /// </summary>
        /// <param name="rejectReason">不可の場合にプレイヤーへ表示する理由</param>
        public bool CanExecuteAction(CharacterInstance targetChar, InterrogationActionType actionType, out string rejectReason)
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

            if (targetChar.IsConfirmedAI)
            {
                rejectReason = "（このキャラクターはすでに AI と判定しています）";
                return false;
            }

            bool isFinalDay = GameManager.Instance != null && GameManager.Instance.IsFinalDay;
            if (isFinalDay && !IsJudgementAction(actionType))
            {
                rejectReason = "（最終日は取り調べできません。AI確定か解放で判定してください）";
                return false;
            }
            if (!isFinalDay && IsJudgementAction(actionType))
            {
                rejectReason = "（解放と AI確定は最終日にだけできます）";
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
        /// 取り調べアクション（最終日は解放・AI確定）を実行する
        /// 条件シートから会話を選び、日別設定で正解を判定し、獲得フラグ・AI化を適用する
        /// </summary>
        public ActionOutcome ExecuteAction(CharacterInstance targetChar, InterrogationActionType actionType, string hearingTargetId = "")
        {
            // 1. ガード節（事前チェック）
            if (targetChar == null) return ActionOutcome.FromText("");

            if (!CanExecuteAction(targetChar, actionType, out string rejectReason))
            {
                Debug.LogWarning($"[InterrogationManager] {targetChar.CharacterId} へのアクションは実行できません: {rejectReason}");
                return ActionOutcome.FromText(rejectReason);
            }

            GameManager game = GameManager.Instance;
            ScenarioDatabase db = game != null ? game.Scenario : null;
            if (db == null)
            {
                Debug.LogError("[InterrogationManager] シナリオのデータ（ScenarioDatabase）が設定されていません。");
                return ActionOutcome.FromText("「……」");
            }

            // 2. 会話を選ぶ（今回の行動による好感度の変化より前の状態で判定する）
            int day = game.CurrentDay;
            int actionCount = GetCharacterActionCount(targetChar.CharacterId, actionType);
            ScenarioConditionRow row = ScenarioSelector.Select(
                db.GetConditionRows(day, targetChar.CharacterId, (ScenarioAction)(int)actionType, hearingTargetId),
                game.CreateSelectorContext(targetChar, actionCount));
            if (row == null)
            {
                Debug.LogWarning($"[InterrogationManager] {day}日目 {targetChar.CharacterId} の「{actionType}」で条件を満たす会話がありません。");
            }

            // 3. 正解の判定・ポイント・好感度、会話の効果（獲得フラグ・AI化）
            int earnedPoints = ApplyScoring(targetChar, actionType, db.GetDailySetting(day, targetChar.CharacterId));
            game.ApplyScenarioEffects(row);

            // 4. 状態更新（カウントアップ・履歴保持）
            IncrementActionCount(targetChar.CharacterId, actionType);
            interrogatedCharacterIdsToday.Add(targetChar.CharacterId);
            TodayActionHistory[targetChar.CharacterId] = actionType;

            currentDailyActions++;
            OnActionCountChanged?.Invoke(currentDailyActions, dailyActionLimit);

            // 5. 判定（解放・AI確定）は会話の選択・行動回数の計上が済んでから確定する
            if (actionType == InterrogationActionType.Release)
            {
                targetChar.SetReleaseFlag(true);
                if (targetChar.IsAI)
                {
                    Debug.LogWarning($"<color=red>【警告】AIである {targetChar.CharacterId} が解放されました。</color>");
                }
            }
            else if (actionType == InterrogationActionType.ConfirmAI)
            {
                targetChar.SetConfirmedAIFlag(true);
                Debug.Log($"[InterrogationManager] {targetChar.CharacterId} を AI と判定しました。（正解: {targetChar.IsAI}）");
            }

            // 6. 夜フェーズ移行チェック
            if (IsActionLimitReached)
            {
                Debug.Log("[InterrogationManager] 行動制限到達。夜フェーズへ移行します。");
                game.TransitionToNightPhase();
            }

            ScenarioDialogue dialogue = row != null ? db.FindDialogue(row.dialogueId) : null;
            return dialogue != null ? ActionOutcome.FromDialogue(dialogue, earnedPoints) : ActionOutcome.FromText("「……」", earnedPoints);
        }

        /// <summary>
        /// 聞き取りで話題にできるキャラのID一覧（取り調べ中の本人以外）
        /// </summary>
        public List<string> GetHearingTargetIds(CharacterInstance targetChar)
        {
            var ids = new List<string>();
            if (GameManager.Instance == null) return ids;

            foreach (CharacterInstance character in GameManager.Instance.AllCharacters)
            {
                if (character == null || character == targetChar) continue;
                ids.Add(character.CharacterId);
            }
            return ids;
        }

        /// <summary>
        /// プロフィール項目の解禁に必要なポイントと、今解禁できるかを調べる（確認ダイアログ用）
        /// </summary>
        /// <param name="cost">必要なポイント</param>
        /// <param name="itemName">項目の名前（確認の文に使う）</param>
        /// <returns>まだ解禁しておらず、ポイントが足りていれば true</returns>
        public bool CanUnlockProfile(CharacterInstance targetChar, string itemKey, out int cost, out string itemName)
        {
            cost = 0;
            itemName = "";
            if (targetChar == null || targetChar.ProfileData == null || string.IsNullOrEmpty(itemKey)) return false;

            if (itemKey.Equals(ProfileDataSO.BasicInfoKey, StringComparison.OrdinalIgnoreCase))
            {
                cost = targetChar.ProfileData.basicInfo != null ? targetChar.ProfileData.basicInfo.unlockCost : 0;
                itemName = "基礎情報";
                return !targetChar.IsBasicInfoUnlocked && currentPoints >= cost;
            }

            ProfileItem item = targetChar.ProfileData.historyInfoList?.Find(i => i.itemKey == itemKey);
            if (item == null) return false;

            cost = item.unlockCost;
            itemName = string.IsNullOrEmpty(item.itemName) ? "過去の情報" : item.itemName;
            return !targetChar.IsHistoryUnlocked(itemKey) && currentPoints >= cost;
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

        /// <summary>
        /// 正解の判定とポイント・好感度の計算
        /// 拷問・対話・交渉は、日別設定で三段階に分ける（どの会話が出たかには関係しない）
        /// 成功＝正解行動、最低＝NG行動、普通＝どちらでもない行動。ポイントは減らない
        /// </summary>
        /// <returns>この行動で入ったポイント</returns>
        private int ApplyScoring(CharacterInstance targetChar, InterrogationActionType actionType, ScenarioDailySetting setting)
        {
            if (actionType == InterrogationActionType.HearAbout) return EarnPoints(fixedHearAboutPoint);

            if (IsJudgementAction(actionType)) return 0;

            if (setting == null)
            {
                Debug.LogWarning($"[InterrogationManager] 日別設定がないため、{targetChar.CharacterId} への行動は正解判定をしません。");
                return 0;
            }

            if (actionType == setting.correctAction)
            {
                if (correctAffectionReward != 0) targetChar.AddAffection(correctAffectionReward);
                return EarnPoints(successPointReward);
            }

            if (setting.hasNgAction && actionType == setting.ngAction)
            {
                if (failAffectionPenalty != 0) targetChar.AddAffection(-failAffectionPenalty);
                return EarnPoints(ngPointReward);
            }

            return EarnPoints(normalPointReward);
        }

        /// <summary> 行動で入ったポイントを足し、今日の獲得ポイントにも計上する </summary>
        private int EarnPoints(int amount)
        {
            if (amount <= 0) return 0;
            AddPoints(amount);
            TodayEarnedPoints += amount;
            return amount;
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

        #endregion

        #region 状態の書き出し・復元（今日をやり直す・セーブ・ロード用）

        /// <summary>
        /// 取り調べに関する今の状態をスナップショットに書き出す
        /// </summary>
        public void CaptureState(GameStateSnapshot snapshot)
        {
            snapshot.points = currentPoints;
            snapshot.dailyActions = currentDailyActions;
            snapshot.todayEarnedPoints = TodayEarnedPoints;
            snapshot.interrogatedTodayIds = new List<string>(interrogatedCharacterIdsToday);

            snapshot.todayActionHistory = new List<ActionHistoryEntry>();
            foreach (var pair in TodayActionHistory)
            {
                snapshot.todayActionHistory.Add(new ActionHistoryEntry { characterId = pair.Key, action = pair.Value });
            }

            snapshot.actionCounts = new List<ActionCountEntry>();
            foreach (var character in characterActionCounts)
            {
                foreach (var action in character.Value)
                {
                    snapshot.actionCounts.Add(new ActionCountEntry { characterId = character.Key, action = action.Key, count = action.Value });
                }
            }
        }

        /// <summary>
        /// スナップショットの状態に戻す
        /// </summary>
        public void RestoreState(GameStateSnapshot snapshot)
        {
            currentPoints = Mathf.Max(0, snapshot.points);
            currentDailyActions = snapshot.dailyActions;
            TodayEarnedPoints = snapshot.todayEarnedPoints;
            interrogatedCharacterIdsToday = new List<string>(snapshot.interrogatedTodayIds ?? new List<string>());

            TodayActionHistory.Clear();
            if (snapshot.todayActionHistory != null)
            {
                foreach (var entry in snapshot.todayActionHistory)
                {
                    TodayActionHistory[entry.characterId] = entry.action;
                }
            }

            characterActionCounts = new Dictionary<string, Dictionary<InterrogationActionType, int>>();
            if (snapshot.actionCounts != null)
            {
                foreach (var entry in snapshot.actionCounts)
                {
                    if (!characterActionCounts.TryGetValue(entry.characterId, out var map))
                    {
                        map = new Dictionary<InterrogationActionType, int>();
                        characterActionCounts[entry.characterId] = map;
                    }
                    map[entry.action] = entry.count;
                }
            }

            OnPointsChanged?.Invoke(currentPoints);
            OnActionCountChanged?.Invoke(currentDailyActions, dailyActionLimit);
        }

        #endregion
    }
}