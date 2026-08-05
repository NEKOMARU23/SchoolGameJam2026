using System;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.InGame.MainSystem
{
    /// <summary>
    /// ゲームプレイ中の個々のキャラクターの状態（動的データ）を管理するクラス
    /// </summary>
    [Serializable]
    public class CharacterInstance
    {
        [Header("基本設定")]
        [Tooltip("キャラクター識別ID (例: A, B, C, D)")]
        [SerializeField] private string characterId;

        [Tooltip("キャラクターの固定プロフィールデータ (ScriptableObject)")]
        [SerializeField] private ProfileDataSO profileData;

        [Header("動的パラメータ（ゲーム中に変動する値）")]
        [Tooltip("現在の内部好感度")]
        [SerializeField] private int currentAffection = 0;

        [Tooltip("このキャラクターが人格AI（真犯人）かどうか")]
        [SerializeField] private bool isAI = false;

        #region プロパティ (外部参照用)
        /// <summary> キャラクターID </summary>
        public string CharacterId => characterId;

        /// <summary> 固定プロフィールデータ </summary>
        public ProfileDataSO ProfileData => profileData;

        /// <summary> 現在の内部好感度 </summary>
        public int CurrentAffection => currentAffection;

        /// <summary> 人格AI（真犯人）フラグ </summary>
        public bool IsAI => isAI;
        #endregion

        /// <summary>
        /// コンストラクタ（コードから生成する場合）
        /// </summary>
        public CharacterInstance(string characterId, ProfileDataSO profileData, int initialAffection = 0, bool isAI = false)
        {
            this.characterId = characterId;
            this.profileData = profileData;
            this.currentAffection = initialAffection;
            this.isAI = isAI;
        }

        /// <summary>
        /// 好感度を加減算する
        /// </summary>
        /// <param name="amount">加算値（マイナス値で減少）</param>
        public void AddAffection(int amount)
        {
            currentAffection += amount;
        }

        /// <summary>
        /// 好感度を直接セットする（バックアップからの復元用）
        /// </summary>
        public void SetAffection(int value)
        {
            currentAffection = value;
        }

        /// <summary>
        /// AI（真犯人）フラグを設定する
        /// </summary>
        public void SetAIFlag(bool value)
        {
            isAI = value;
        }
    }
}