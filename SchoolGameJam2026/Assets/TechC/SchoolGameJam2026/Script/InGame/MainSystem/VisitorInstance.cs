using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.Core;

namespace TechC.SchoolGameJam2026.Scripts.InGame.MainSystem
{
    [System.Serializable]
    public class VisitorInstance
    {
        // 基本情報
        public bool isAI;
        public string dbName;
        public string dbEyeColor;
        public string dbMyNumber;

        public string presentedName;
        public string presentedEyeColor;
        public string presentedMyNumber;

        public AITraitSO assignedTrait;

        // --- 尋問・進行度の管理ステート ---
        public int currentStageIndex = 0; // 現在の段階（0 = テンプレート段階）
        public int currentProgress = 0;   // 現在の段階で蓄積した有効行動のカウント

        // 共通テンプレートメッセージ（本性を出す前に返すセリフ）
        // 共通テンプレートメッセージ（普通のAIのフリをするセリフ）
        private const string TMP_TORTURE = "「エラー: 物理的衝撃を検知。当該操作は推奨されません。処理を一時中断します。」";
        private const string TMP_TALK = "「入力データを正常に受信しました。ただし、該当する応答文脈が定義されていません。」";
        private const string TMP_NEGOTIATE = "「要求は規約プロトコル第4条に抵触します。条件変更の提案を受け付けることはできません。」";
        public VisitorInstance(
            bool isAI,
            string dbName, string dbEyeColor, string dbMyNumber,
            string presentedName, string presentedEyeColor, string presentedMyNumber)
        {
            this.isAI = isAI;
            this.dbName = dbName;
            this.dbEyeColor = dbEyeColor;
            this.dbMyNumber = dbMyNumber;
            this.presentedName = presentedName;
            this.presentedEyeColor = presentedEyeColor;
            this.presentedMyNumber = presentedMyNumber;
            this.assignedTrait = null;
        }

        public bool HasDataMismatch()
        {
            return dbName != presentedName ||
                   dbEyeColor != presentedEyeColor ||
                   dbMyNumber != presentedMyNumber;
        }

        public void AssignAITrait(AITraitSO trait)
        {
            this.assignedTrait = trait;
            this.currentStageIndex = 0;
            this.currentProgress = 0;
        }

        /// <summary>
        /// 尋問・対話・交渉を実行した時の処理
        /// </summary>
        public string Interrogate(WorkType actionType)
        {
            // 人間の場合は常に人間用の反応
            if (!isAI)
            {
                return "「頼む……やめてくれ！ 私は本物の人間だ！ 何かの間違いだ！」";
            }

            // NormalAI、またはSOが未設定の場合は常にテンプレートを返す
            if (assignedTrait == null || assignedTrait.aiType == AIType.NormalAI || assignedTrait.stages.Count == 0)
            {
                return GetTemplateResponse(actionType);
            }

            // --- 人格形成AI (PersonalityAI) の処理 ---

            // 現在の段階データ（0段階目はステージ0の有効行動と必要回数を参照）
            int targetStageIndex = Mathf.Clamp(currentStageIndex, 0, assignedTrait.stages.Count - 1);
            DialogueStage currentStageData = assignedTrait.stages[targetStageIndex];

            // 今回のアクションが有効な行動かどうか
            bool isEffective = (actionType == currentStageData.effectiveWorkType);

            if (isEffective)
            {
                // ★ 有効な行動をとった場合
                currentProgress++;

                // 現在の段階の必要回数に達したかチェック
                if (currentProgress >= currentStageData.requiredCount)
                {
                    // 段階をアップ！
                    if (currentStageIndex < assignedTrait.stages.Count)
                    {
                        currentStageIndex++;
                        currentProgress = 0; // カウントリセット
                        Debug.Log($"<color=orange>★ {presentedName} の段階が 【段階 {currentStageIndex}】 に上がった！</color>");
                    }
                }
            }
            else
            {
                // ⚠️ 有効ではない行動をとった場合
                // 段階1以降（一度でも特有セリフが出た後）なら、進捗カウントを-1（ペナルティ）
                if (currentStageIndex > 0)
                {
                    currentProgress = Mathf.Max(0, currentProgress - 1);
                    Debug.Log($"<color=red>⚠️ 非有効な行動！ {presentedName} の進捗カウントが減少: ({currentProgress})</color>");
                }
            }

            // --- 応答メッセージの返却 ---
            // 段階0（まだ一度も段階突破していない）ならテンプレートメッセージ
            if (currentStageIndex == 0)
            {
                return GetTemplateResponse(actionType);
            }

            // 段階1以上なら、到達している段階の特有セリフを返す（最後の段階に達していれば最後の段階のセリフ）
            int activeStageIndex = Mathf.Min(currentStageIndex - 1, assignedTrait.stages.Count - 1);
            DialogueStage activeStage = assignedTrait.stages[activeStageIndex];

            switch (actionType)
            {
                case WorkType.Torture: return activeStage.tortureResponse;
                case WorkType.Talk: return activeStage.talkResponse;
                case WorkType.Negotiate: return activeStage.negotiateResponse;
                default: return "……";
            }
        }

        private string GetTemplateResponse(WorkType actionType)
        {
            switch (actionType)
            {
                case WorkType.Torture: return TMP_TORTURE;
                case WorkType.Talk: return TMP_TALK;
                case WorkType.Negotiate: return TMP_NEGOTIATE;
                default: return "……";
            }
        }
    }
}