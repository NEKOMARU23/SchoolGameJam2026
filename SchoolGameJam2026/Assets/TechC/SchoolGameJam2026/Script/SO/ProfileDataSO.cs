using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.SO
{
    /// <summary>
    /// 基礎情報を1つのエレメントとしてまとめたクラス（一括解禁用）
    /// </summary>
    [Serializable]
    public class BasicProfileGroup
    {
        [Header("名前")]
        public string characterName;

        [Header("身長")]
        public string height;

        [Header("体重")]
        public string weight;

        [Header("血液型")]
        public string bloodType;

        [Header("好きなこと")]
        public string likes;

        [Header("嫌いなこと")]
        public string dislikes;

        [Header("基礎情報の解禁に必要なポイント")]
        public int unlockCost = 30;

        [Header("解禁済みかどうか")]
        public bool isUnlocked = false;
    }

    /// <summary>
    /// 過去情報の1項目データ（個別解禁用）
    /// </summary>
    [Serializable]
    public class ProfileItem
    {
        [Header("項目名 (例: 前職、アレルギー、過去の病歴)")]
        public string itemKey;

        [Header("項目の内容 (例: エビ・カニ、〇〇高校中退)")]
        [TextArea(2, 4)] public string valueContent;

        [Header("解禁に必要なポイント")]
        public int unlockCost = 30;

        [Header("解禁済みかどうか")]
        public bool isUnlocked = false;
    }

    /// <summary>
    /// 各キャラクターの固定プロフィール情報データ
    /// </summary>
    [CreateAssetMenu(fileName = "NewProfileData", menuName = "Game/Profile Data")]
    public class ProfileDataSO : ScriptableObject
    {
        [Header("キャラクター基本識別情報")]
        public string characterId;       // ID (例: A, B, C, D)
        public Sprite characterIcon;     // 資料画面用アイコン画像

        [Header("--- 基礎情報（1つのエレメントで管理） ---")]
        public BasicProfileGroup basicInfo = new BasicProfileGroup();

        [Header("--- 過去の経歴・個別情報 ---")]
        [Tooltip("過去に患った病気、アレルギー、出身校、前職 など")]
        public List<ProfileItem> historyInfoList = new List<ProfileItem>();

        // --------------------------------------------------
        // ★ リセット（当日の朝に戻す）用の状態保存・復元処理
        // --------------------------------------------------

        /// <summary>
        /// 現在の全プロフィールの解禁フラグをリストとして取得する（バックアップ用）
        /// </summary>
        public List<bool> GetUnlockStates()
        {
            List<bool> states = new List<bool>();

            // 1. 基礎情報の解禁状態を保持（インデックス 0）
            states.Add(basicInfo.isUnlocked);

            // 2. 過去情報の解禁状態を保持
            foreach (var item in historyInfoList)
            {
                states.Add(item.isUnlocked);
            }

            return states;
        }

        /// <summary>
        /// 保存しておいた解禁フラグリストから状態を復元する（リセット用）
        /// </summary>
        public void RestoreUnlockStates(List<bool> states)
        {
            if (states == null || states.Count == 0) return;

            int index = 0;

            // 1. 基礎情報の復元
            basicInfo.isUnlocked = states[index++];

            // 2. 過去情報の復元
            foreach (var item in historyInfoList)
            {
                if (index < states.Count)
                {
                    item.isUnlocked = states[index++];
                }
            }
        }
    }
}