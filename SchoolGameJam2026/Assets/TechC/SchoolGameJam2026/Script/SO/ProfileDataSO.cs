using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.SO
{
    /// <summary>
    /// 基礎情報を1つのエレメントとしてまとめたデータ構造（一括解禁用マスターデータ）
    /// </summary>
    [Serializable]
    public class BasicProfileGroup
    {
        [Header("名前")]
        public string characterName;

        [Header("年齢")]
        public string age;

        [Header("職業")]
        public string occupation;

        [Header("誕生日")]
        public string birthday;

        [Header("身長")]
        public string height;

        [Header("体重")]
        public string weight;

        [Header("特技")]
        public string skills;

        [Header("好きなこと")]
        public string likes;

        [Header("嫌いなこと")]
        public string dislikes;

        [Header("基礎情報の解禁に必要なポイント")]
        public int unlockCost = 30;
    }

    /// <summary>
    /// 過去情報の1項目マスターデータ（個別解禁用）
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
    }

    /// <summary>
    /// 各キャラクターの固定プロフィール情報データ（マスターデータアセット）
    /// </summary>
    [CreateAssetMenu(fileName = "NewProfileData", menuName = "Game/Profile Data")]
    public class ProfileDataSO : ScriptableObject
    {
        [Header("キャラクター基本識別情報")]
        public string characterId;       // ID (例: A, B, C, D)
        public Sprite characterIcon;     // 資料画面用アイコン画像

        [Header("--- 基礎情報マスター ---")]
        public BasicProfileGroup basicInfo = new BasicProfileGroup();

        [Header("--- 過去の経歴・個別情報マスター ---")]
        [Tooltip("過去に患った病気、アレルギー、出身校、前職 など")]
        public List<ProfileItem> historyInfoList = new List<ProfileItem>();
    }
}