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

        [Header("行動制限設定")]
        [Tooltip("1日の最大行動回数")]
        [SerializeField] private int dailyActionLimit = 4;

        [Tooltip("本日実行済みの行動回数")]
        [SerializeField] private int currentDailyActions = 0;

        // ★追加：本日すでに取り調べを行ったキャラクターのIDリスト
        private List<string> interrogatedCharacterIdsToday = new List<string>();

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
        /// ★指定したキャラクターに今日すでに行動（取り調べ）を行ったか確認する
        /// </summary>
        public bool HasInterrogatedToday(string characterId)
        {
            return interrogatedCharacterIdsToday.Contains(characterId);
        }

        /// <summary>
        /// 取り調べアクションを実行する
        /// </summary>
        /// <param name="targetChar">対象のキャラクター</param>
        /// <param name="actionType">実行するアクション種別</param>
        /// <param name="hearingTargetId">【HearAboutの場合のみ】噂を聞く対象キャラのID</param>
        /// <returns>キャラクターから返ってくるセリフテキスト</returns>
        public string ExecuteAction(CharacterInstance targetChar, InterrogationActionType actionType, string hearingTargetId = "")
        {
            // 1. 1日の全体行動上限チェック
            if (IsActionLimitReached)
            {
                Debug.LogWarning("[InterrogationManager] 本日の行動上限に達しているため、アクションを実行できません。");
                return "（本日の取り調べ可能回数を消化しました）";
            }

            // ★ 1.5 キャラクターごとの重複取り調べチェック (1人につき1日1回)
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

            string responseDialogue = "";

            // 4. アクションごとのロジック処理
            if (actionType == InterrogationActionType.HearAbout)
            {
                // 【他の人の話を聞く】場合：正解判定なし、固定ポイント獲得
                AddPoints(fixedHearAboutPoint);
                responseDialogue = dailySetting.GetResponse(actionType, targetChar.CurrentAffection, otherAffections, hearingTargetId);
            }
            else
            {
                // 【拷問 / 対話 / 交渉】の場合：正解判定
                bool isCorrect = (actionType == dailySetting.correctAction);

                if (isCorrect)
                {
                    AddPoints(successPointReward);
                    targetChar.AddAffection(1); // 正解時は好感度も少し上昇
                    Debug.Log($"[InterrogationManager] 正解！ {successPointReward}pt 獲得");
                }
                else
                {
                    ConsumePoints(failPointPenalty);
                    targetChar.AddAffection(-1); // 不正解時は好感度低下
                    Debug.Log($"[InterrogationManager] 不正解…… {failPointPenalty}pt 減少");
                }

                responseDialogue = dailySetting.GetResponse(actionType, targetChar.CurrentAffection, otherAffections);
            }

            // ★ 5. 行動済みキャラクターリストに追加
            interrogatedCharacterIdsToday.Add(targetChar.CharacterId);

            // 6. 全体行動回数をカウントアップ
            currentDailyActions++;
            OnActionCountChanged?.Invoke(currentDailyActions, dailyActionLimit);

            // 7. 行動上限に達したら夜フェーズ（次の展開）へ移行
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
        /// <summary>
        /// 1日の行動記録（回数およびキャラクターごとの行動済みフラグ）をリセットする
        /// </summary>
        public void ResetDailyActions(int actionCount = 0)
        {
            currentDailyActions = actionCount;
            interrogatedCharacterIdsToday.Clear(); // ★本日取り調べたキャラリストをリセット
            OnActionCountChanged?.Invoke(currentDailyActions, dailyActionLimit);
        }

        /// <summary>
        /// ★「今日をやり直す（リセット）」用のバックアップ・復元処理で使うリスト取得
        /// </summary>
        public List<string> GetInterrogatedCharacterIdsToday()
        {
            return new List<string>(interrogatedCharacterIdsToday);
        }

        /// <summary>
        /// ★「今日をやり直す（リセット）」時にバックアップから状態を復元する
        /// </summary>
        public void RestoreInterrogatedCharacterIdsToday(List<string> backupList)
        {
            interrogatedCharacterIdsToday = backupList != null ? new List<string>(backupList) : new List<string>();
        }
        #endregion
    }
}