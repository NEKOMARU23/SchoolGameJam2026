using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core
{
    // 行動の種類
    public enum WorkType
    {
        Torture,    // 尋問
        Talk,       // 対話
        Negotiate   // 交渉
    }

    // AIのタイプ
    public enum AIType
    {
        NormalAI,       // 通常AI
        PersonalityAI   // 人格形成AI（ターゲット）
    }

    /// <summary>
    /// 各段階（ステージ）の定義クラス
    /// </summary>
    [System.Serializable]
    public class DialogueStage
    {
        [Header("この段階を突破するために必要な「有効行動」の実行回数")]
        public int requiredCount = 2;

        [Header("この段階で有効な行動")]
        public WorkType effectiveWorkType;

        [Header("この段階でのキャラ特有セリフ")]
        [TextArea] public string tortureResponse;   // 尋問時のセリフ
        [TextArea] public string talkResponse;      // 対話時のセリフ
        [TextArea] public string negotiateResponse; // 交渉時のセリフ
    }

    [CreateAssetMenu(fileName = "NewAITrait", menuName = "Game/AI Trait")]

    /// <summary>
    /// AIの素性（Trait）を定義するScriptableObject
    /// </summary>    
    public class AITraitSO : ScriptableObject
    {
        public string traitName; // AIの素性名（例: 感情芽生え型）
        public AIType aiType;    // NormalAI か PersonalityAI か

        [Header("【人格形成AI用】変化する段階リスト（無限に追加可能）")]
        public List<DialogueStage> stages = new List<DialogueStage>();
    }
}