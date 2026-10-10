using System;
using System.Collections.Generic;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.Core.State;

namespace TechC.SchoolGameJam2026.Scripts.InGame.MainSystem
{
    /// <summary>
    /// ゲームプレイ中の個々のキャラクターの状態（動的・ランタイムデータ）を管理するクラス
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

        [Tooltip("このキャラクターが解放済みかどうか")]
        [SerializeField] private bool isReleased = false;

        [Tooltip("最終日にプレイヤーが AI と判定（AI確定）したかどうか")]
        [SerializeField] private bool isConfirmedAI = false;

        [Header("プロフィール解禁状態（ランタイム管理）")]
        [Tooltip("基礎情報（名前、年齢等）が解禁済みか")]
        [SerializeField] private bool isBasicInfoUnlocked = false;

        [Tooltip("解禁済みの過去情報項目キー一覧")]
        [SerializeField] private List<string> unlockedHistoryKeys = new List<string>();

        #region プロパティ (外部参照用)

        /// <summary> キャラクターID </summary>
        public string CharacterId => characterId;

        /// <summary> 固定プロフィールデータ </summary>
        public ProfileDataSO ProfileData => profileData;

        /// <summary> 現在の内部好感度 </summary>
        public int CurrentAffection => currentAffection;

        /// <summary> 人格AI（真犯人）フラグ </summary>
        public bool IsAI => isAI;

        /// <summary> 解放フラグ </summary>
        public bool IsReleased => isReleased;

        /// <summary> 最終日にプレイヤーが AI と判定したか </summary>
        public bool IsConfirmedAI => isConfirmedAI;

        /// <summary> 解放か AI確定のどちらかで判定済みか（判定済みの人の部屋には入れない） </summary>
        public bool IsJudged => isReleased || isConfirmedAI;

        /// <summary> 基礎情報が解禁済みか </summary>
        public bool IsBasicInfoUnlocked => isBasicInfoUnlocked;

        /// <summary> 解禁済みの過去情報キー一覧（読み取り専用） </summary>
        public IReadOnlyList<string> UnlockedHistoryKeys => unlockedHistoryKeys;

        #endregion

        /// <summary>
        /// コンストラクタ（コードから生成・初期化する場合）
        /// </summary>
        public CharacterInstance(string characterId, ProfileDataSO profileData, int initialAffection = 0, bool isAI = false, bool isReleased = false)
        {
            this.characterId = characterId;
            this.profileData = profileData;
            this.currentAffection = initialAffection;
            this.isAI = isAI;
            this.isReleased = isReleased;
            this.isBasicInfoUnlocked = false;
            this.unlockedHistoryKeys = new List<string>();
        }

        #region パラメータ変更・状態操作メソッド

        /// <summary>
        /// 好感度を加減算する
        /// </summary>
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

        /// <summary>
        /// 解放フラグを設定する
        /// </summary>
        public void SetReleaseFlag(bool value)
        {
            isReleased = value;
        }

        /// <summary>
        /// AI確定（プレイヤーの判定）フラグを設定する
        /// </summary>
        public void SetConfirmedAIFlag(bool value)
        {
            isConfirmedAI = value;
        }

        #endregion

        #region プロフィール解禁制御メソッド

        /// <summary>
        /// 基礎情報の解禁フラグを設定する
        /// </summary>
        public void SetBasicInfoUnlocked(bool isUnlocked)
        {
            isBasicInfoUnlocked = isUnlocked;
        }

        /// <summary>
        /// 指定された過去情報キーが解禁されているかチェックする
        /// </summary>
        public bool IsHistoryUnlocked(string itemKey)
        {
            return unlockedHistoryKeys.Contains(itemKey);
        }

        /// <summary>
        /// 過去情報項目を解禁する
        /// </summary>
        public void UnlockHistoryItem(string itemKey)
        {
            if (!unlockedHistoryKeys.Contains(itemKey))
            {
                unlockedHistoryKeys.Add(itemKey);
            }
        }

        #endregion

        #region 状態の書き出し・復元（今日をやり直す・セーブ・ロード用）

        /// <summary>
        /// このキャラの今の状態を書き出す
        /// </summary>
        public CharacterStateEntry CaptureState()
        {
            return new CharacterStateEntry
            {
                characterId = characterId,
                affection = currentAffection,
                isReleased = isReleased,
                isAI = isAI,
                isConfirmedAI = isConfirmedAI,
                isBasicInfoUnlocked = isBasicInfoUnlocked,
                unlockedHistoryKeys = new List<string>(unlockedHistoryKeys),
            };
        }

        /// <summary>
        /// 書き出した状態に戻す
        /// </summary>
        public void RestoreState(CharacterStateEntry state)
        {
            if (state == null) return;
            currentAffection = state.affection;
            isReleased = state.isReleased;
            isAI = state.isAI;
            isConfirmedAI = state.isConfirmedAI;
            isBasicInfoUnlocked = state.isBasicInfoUnlocked;
            unlockedHistoryKeys = state.unlockedHistoryKeys != null ? new List<string>(state.unlockedHistoryKeys) : new List<string>();
        }

        #endregion
    }
}