using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.Core;

namespace TechC.SchoolGameJam2026.Scripts.InGame.MainSystem
{
    /// <summary>
    /// 関所で生成される訪問者のインスタンスを表すクラス
    /// </summary>
    [System.Serializable]
    public class VisitorInstance
    {
        // --- 【関所フェーズで使う情報】 ---
        public string id;
        public string name;
        public bool isAI;               // 単純なBool判定（関所ではこれしか見ない！）
        public bool hasDocumentError;   // 書類不備（関所で人間/AIを疑う手がかり）

        // --- 【収容後に割り当てられるSO】 ---
        public AITraitSO assignedTrait; // 収容時までは null

        // --- 【尋問時の状態】 ---
        public int workCount = 0;
        public bool workedToday = false;

        // コンストラクタ（関所の訪問者生成）
        public VisitorInstance(string id, string name, bool isAI, bool hasDocumentError)
        {
            this.id = id;
            this.name = name;
            this.isAI = isAI;
            this.hasDocumentError = hasDocumentError;
            this.assignedTrait = null; // 関所の時点では空っぽ
        }

        // 収容所に入った時にSOをガチャ（ランダム付与）するメソッド
        public void AssignAITrait(AITraitSO trait)
        {
            this.assignedTrait = trait;
        }

        // 尋問（作業）を受けた時の処理
        public string Interrogate(string workType)
        {
            workCount++;
            workedToday = true;

            // 【もし本物の人間（isAI == false）が収容されていた場合】
            if (!isAI)
            {
                return "「頼む……やめてくれ！私は人間だ！何か誤解がある！」";
            }

            // 【AIの場合】付与されたSOの難易度（aiLevel）と作業回数を比べる
            if (workCount < assignedTrait.aiLevel)
            {
                // まだボロを出さない（人間を装う）
                return "「何か問題でも？ 私は正常な市民です。」";
            }

            // aiLevelを超えたら、付与されたSOから決定的なボロを返す！
            switch (workType)
            {
                case "torture": return assignedTrait.tortureResponse;
                case "talk": return assignedTrait.talkResponse;
                case "negotiate": return assignedTrait.negotiateResponse;
                default: return "……";
            }
        }
    }
}