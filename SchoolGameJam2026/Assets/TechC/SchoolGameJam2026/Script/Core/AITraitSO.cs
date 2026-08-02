using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core
{
    /// <summary>
    /// AIの素性を定義する列挙型
    /// </summary>
    public enum AIType
    {
        NormalAI,
        PersonalityAI
    }

    [CreateAssetMenu(fileName = "NewAITrait", menuName = "Game/AI Trait")]
    public class AITraitSO : ScriptableObject
    {
        /// <summary>
        /// AIの素性データを格納するScriptableObject
        /// ScriptableObjectは略して「SO」という
        /// </summary>
        [Header("AIの素性データ")]
        public string traitName;
        public AIType aiType;          // ノーマルなAI か 人格形成したAI か
        [Range(0, 2)] public int aiLevel; // ボロを出す難易度（0:すぐ出す, 2:なかなか出さない）

        [Header("尋問時のボロ（セリフ）")]
        [TextArea] public string tortureResponse;   // 拷問時のセリフ
        [TextArea] public string talkResponse;      // 対話時のセリフ
        [TextArea] public string negotiateResponse; // 交渉時のセリフ
    }
}