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
        [Tooltip("現在所持している解析ポイント")]
        [SerializeField] private int currentPoints = 0;

        [Tooltip("正解時の獲得ポイント")]
        [SerializeField] private int successPointReward = 20;

        [Tooltip("不正解時の減少ポイント")]
        [SerializeField] private int failPointPenalty = 5;

        [Tooltip("『他の人の話を聞く』選択時の固定獲得ポイント")]
        [SerializeField] private int fixedHearAboutPoint = 10;

        [Header("好感度調整設定（インスペクターで変更可能）")]
        [Tooltip("Element 0（最優先セリフ）表示時の好感度加算値")]
        [SerializeField] private int correctAffectionReward = 1;

        [Tooltip("特定NG行動（penaltyAction）を実行した時の好感度減少値")]
        [SerializeField] private int failAffectionPenalty = 1;

        [Header("行動制限設定")]
        [Tooltip("1日の最大行動回数")]
        [SerializeField] private int dailyActionLimit = 4;

        [Tooltip("本日実行済みの行動回数")]
        [SerializeField] private int currentDailyActions = 0;

        // キャラID -> ( アクション種別 -> 通算実行回数 )
        private Dictionary<string, Dictionary<InterrogationActionType, int>> characterActionCounts 
            = new Dictionary<string, Dictionary<InterrogationActionType, int>>();

        // 本日すでに取り調べを行ったキャラクターのIDリスト
        private List<string> interrogatedCharacterIdsToday = new List<string>();

        #region リザルト用追加データ
        /// <summary> 本日各キャラクターに対して実行したアクションの履歴 (キャラID -> アクション) </summary>
        public Dictionary<string, InterrogationActionType> TodayActionHistory { get; private set; } = new Dictionary<string, InterrogationActionType>();

        /// <summary> 本日（今日1日）の中で獲得/減少した累積ポイント </summary>
        public int TodayEarnedPoints { get; private set; } = 0;
        #endregion

        #region プロパティ
        /// <summary> 現在の解析ポイント </summary>
        public int CurrentPoints => currentPoints;

        /// <summary> 1日の最大行動回数 </summary>
        public int DailyActionLimit => dailyActionLimit;

        /// <summary> 本日の実行済み行動回数 </summary>
        public int CurrentDailyActions => currentDailyActions;

        /// <summary> 本日の残り行動回数 </summary>
        public int RemainingDailyActions => Mathf.Max(0, dailyActionLimit - currentDailyActions);

        /// <summary> 本日の行動上限に達したかどうか </summary>
        public bool IsActionLimitReached => currentDailyActions >= dailyActionLimit;
        #endregion

        #region イベント
        /// <summary> ポイント数が変化した時のイベント (引数: 変化後のポイント) </summary>
        public event Action<int> OnPointsChanged;

        /// <summary> 行動回数が変化した時のイベント (引数: 実行済み回数, 最大回数) </summary>
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

        /// <summary>
        /// 指定したキャラクターに今日すでに行動（取り調べ）を行ったか確認する
        /// </summary>
        public bool HasInterrogatedToday(string characterId)
        {
            return interrogatedCharacterIdsToday.Contains(characterId);
        }

        /// <summary>
        /// 特定キャラクターの特定アクションにおける通算実行回数を取得する
        /// </summary>
        public int GetCharacterActionCount(string characterId, InterrogationActionType actionType)
        {
            if (characterActionCounts.TryGetValue(characterId, out var actionMap))
            {
                if (actionMap.TryGetValue(actionType, out int count))
                {
                    return count;
                }
            }
            return 0;
        }

        /// <summary>
        /// 取り調べアクションを実行する
        /// </summary>
        public string ExecuteAction(CharacterInstance targetChar, InterrogationActionType actionType, string hearingTargetId = "")
        {
            // 1. 1日の全体行動上限チェック
            if (IsActionLimitReached)
            {
                Debug.LogWarning("[InterrogationManager] 本日の行動上限に達しているため、アクションを実行できません。");
                return "（本日の取り調べ可能回数を消化しました）";
            }

            // 1.5 キャラクターごとの重複取り調べチェック (1人につき1日1回)
            if (HasInterrogatedToday(targetChar.CharacterId))
            {
                Debug.LogWarning($"[InterrogationManager] {targetChar.CharacterId} には本日すでに取り調べを行っています。");
                return "（このキャラクターへの本日の取り調べは既に終了しています）";
            }

            // 2. 本日のシナリオデータを取得
            DayScenarioSO todayScenario = GameManager.Instance != null ? GameManager.Instance.GetTodayScenario() : null;
            if (todayScenario == null)
            {
                Debug.LogError("[InterrogationManager] 今日のシナリオデータ(DayScenarioSO)が見つかりません。");
                return "「……」";
            }

            CharacterDailySetting dailySetting = todayScenario.GetCharacterSetting(targetChar.CharacterId);
            if (dailySetting == null)
            {
                Debug.LogError($"[InterrogationManager] {targetChar.CharacterId} のシナリオ設定が見つかりません。");
                return "「……」";
            }

            // 3. 他キャラの好感度辞書を作成（条件判定用）
            Dictionary<string, int> otherAffections = GameManager.Instance != null 
                ? GameManager.Instance.GetAllCharacterAffections() 
                : new Dictionary<string, int>();

            // ★ 4. 対象キャラの該当アクションにおける現在の通算実行回数を取得
            int currentActionCountForThisType = GetCharacterActionCount(targetChar.CharacterId, actionType);

            string responseDialogue = "";

            // 5. アクションごとのロジック処理（判定時には通算回数 currentActionCountForThisType を渡す）
            if (actionType == InterrogationActionType.HearAbout)
            {
                AddPoints(fixedHearAboutPoint);
                TodayEarnedPoints += fixedHearAboutPoint;

                var result = dailySetting.GetResponseWithIndex(actionType, targetChar.CurrentAffection, otherAffections, currentActionCountForThisType, hearingTargetId);
                responseDialogue = result.dialogueText;
            }
            else if (actionType == InterrogationActionType.Release || actionType == InterrogationActionType.ConfirmAI)
            {
                var result = dailySetting.GetResponseWithIndex(actionType, targetChar.CurrentAffection, otherAffections, currentActionCountForThisType);
                responseDialogue = result.dialogueText;
            }
            else
            {
                var (dialogueText, elementIndex) = dailySetting.GetResponseWithIndex(actionType, targetChar.CurrentAffection, otherAffections, currentActionCountForThisType);
                responseDialogue = dialogueText;

                bool isCorrect = (elementIndex == 0 && actionType == dailySetting.correctAction);
                bool isPenaltyAction = (actionType == dailySetting.penaltyAction);

                if (isCorrect)
                {
                    AddPoints(successPointReward);
                    TodayEarnedPoints += successPointReward;
                    
                    if (correctAffectionReward != 0)
                    {
                        targetChar.AddAffection(correctAffectionReward);
                    }
                    Debug.Log($"<color=green>[InterrogationManager] 正解行動（{actionType}）！ 好感度+{correctAffectionReward}、{successPointReward}pt 獲得。</color>");
                }
                else
                {
                    ConsumePoints(failPointPenalty);
                    TodayEarnedPoints -= failPointPenalty;

                    if (isPenaltyAction && failAffectionPenalty != 0)
                    {
                        targetChar.AddAffection(-failAffectionPenalty);
                        Debug.Log($"<color=red>[InterrogationManager] NG行動（{actionType}）を実行！ 好感度-{failAffectionPenalty}。</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>[InterrogationManager] 不正解（{actionType}）。好感度変動なし。{failPointPenalty}pt 減少。</color>");
                    }
                }
            }

            // ★ 6. キャラクター×アクションごとの通算カウントをインクリメント
            if (!characterActionCounts.ContainsKey(targetChar.CharacterId))
            {
                characterActionCounts[targetChar.CharacterId] = new Dictionary<InterrogationActionType, int>();
            }
            if (!characterActionCounts[targetChar.CharacterId].ContainsKey(actionType))
            {
                characterActionCounts[targetChar.CharacterId][actionType] = 0;
            }
            characterActionCounts[targetChar.CharacterId][actionType]++;

            // 7. 行動済みキャラクターリストおよびリザルト用履歴に追加
            interrogatedCharacterIdsToday.Add(targetChar.CharacterId);
            TodayActionHistory[targetChar.CharacterId] = actionType;

            // 8. 本日の全体行動回数をカウントアップ
            currentDailyActions++;
            OnActionCountChanged?.Invoke(currentDailyActions, dailyActionLimit);

            // 9. 行動上限に達したら夜フェーズへ移行
            if (IsActionLimitReached)
            {
                Debug.Log("[InterrogationManager] 1日の行動制限に達しました。夜フェーズへ移行します。");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.TransitionToNightPhase();
                }
            }

            return responseDialogue;
        }

        #region ポイント操作
        public void AddPoints(int amount)
        {
            if (amount <= 0) return;
            currentPoints += amount;
            OnPointsChanged?.Invoke(currentPoints);
        }

        public void ConsumePoints(int amount)
        {
            if (amount <= 0) return;
            currentPoints = Mathf.Max(0, amount); // 0未満にならないよう設定
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

        /// <summary>
        /// セーブ・ロード用のデータ取得
        /// </summary>
        public Dictionary<string, Dictionary<InterrogationActionType, int>> GetCharacterActionCounts()
        {
            return characterActionCounts;
        }

        /// <summary>
        /// セーブ・ロード用のデータ復元
        /// </summary>
        public void RestoreCharacterActionCounts(Dictionary<string, Dictionary<InterrogationActionType, int>> backupData)
        {
            characterActionCounts = backupData != null ? backupData : new Dictionary<string, Dictionary<InterrogationActionType, int>>();
        }

        public List<string> GetInterrogatedCharacterIdsToday()
        {
            return new List<string>(interrogatedCharacterIdsToday);
        }

        public void RestoreInterrogatedCharacterIdsToday(List<string> backupList)
        {
            interrogatedCharacterIdsToday = backupList != null ? new List<string>(backupList) : new List<string>();
        }
        #endregion
    }
}