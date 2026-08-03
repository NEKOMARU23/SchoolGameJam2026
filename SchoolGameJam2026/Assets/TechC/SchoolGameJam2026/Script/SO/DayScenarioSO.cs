using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.SO
{
    /// <summary>
    /// 取り調べ時の4つの行動種別
    /// </summary>
    public enum InterrogationActionType
    {
        Torture,    // 1. 拷問
        Talk,       // 2. 対話
        Negotiate,  // 3. 交渉
        HearAbout   // 4. 他の人について聞き取り
    }

    /// <summary>
    /// セリフの判定条件タイプ
    /// </summary>
    public enum DialogueConditionType
    {
        TargetAffection,  // 1. 対象キャラ(本人)の内部好感度
        OtherAffection,   // 2. その他のキャラの内部好感度
        Unconditional     // 3. 条件なし（デフォルトセリフ）
    }

    /// <summary>
    /// 条件付きセリフの1データ
    /// </summary>
    [Serializable]
    public class ConditionalDialogue
    {
        [Header("セリフの種別")]
        public DialogueConditionType conditionType = DialogueConditionType.Unconditional;

        [Header("【タイプ2の場合】参照する他キャラのID/名前")]
        public string targetCharacterId;

        [Header("必要好感度（この値以上なら再生可能）")]
        public int requiredAffection = 0;

        [Header("セリフ本文")]
        [TextArea(2, 5)] public string dialogueText;
    }

    /// <summary>
    /// アクションに対するセリフ設定グループ（優先度判定ロジック付き）
    /// </summary>
    [Serializable]
    public class ActionResponseGroup
    {
        [Header("優先度順リスト（上にあるものほど高優先度で評価）")]
        [Tooltip("上から順番に条件を判定し、最初に条件を満たしたセリフを採用します。不要な条件はリストに入れなくてOKです。")]
        public List<ConditionalDialogue> priorityDialogueList = new List<ConditionalDialogue>();

        /// <summary>
        /// 現在の好感度状況から、最も優先度の高いセリフを自動取得する
        /// </summary>
        /// <param name="selfAffection">本人の好感度</param>
        /// <param name="otherAffections">他キャラの好感度辞書 (Key: キャラID, Value: 好感度)</param>
        public string GetValidDialogue(int selfAffection, Dictionary<string, int> otherAffections)
        {
            if (priorityDialogueList == null || priorityDialogueList.Count == 0)
            {
                return "「……」";
            }

            foreach (var dialogue in priorityDialogueList)
            {
                switch (dialogue.conditionType)
                {
                    case DialogueConditionType.TargetAffection:
                        // 1. 本人の好感度判定
                        if (selfAffection >= dialogue.requiredAffection)
                            return dialogue.dialogueText;
                        break;

                    case DialogueConditionType.OtherAffection:
                        // 2. 他人の好感度判定
                        if (otherAffections != null && !string.IsNullOrEmpty(dialogue.targetCharacterId))
                        {
                            if (otherAffections.TryGetValue(dialogue.targetCharacterId, out int otherValue))
                            {
                                if (otherValue >= dialogue.requiredAffection)
                                    return dialogue.dialogueText;
                            }
                        }
                        break;

                    case DialogueConditionType.Unconditional:
                        // 3. 条件なし（無条件）
                        return dialogue.dialogueText;
                }
            }

            return "「……」";
        }
    }

    /// <summary>
    /// 【聞き取り（HearAbout）用】誰についての噂・話を聞くかごとのセリフ設定
    /// </summary>
    [Serializable]
    public class HearingTargetResponse
    {
        [Header("対象となる他キャラのID/名前 (例: B, スズキ 等)")]
        public string targetCharacterId;

        [Header("そのキャラについて話すセリフ（好感度条件付き）")]
        public ActionResponseGroup responseGroup;
    }

    /// <summary>
    /// 1人のキャラの「今日の取り調べセリフ・正解定義」
    /// </summary>
    [Serializable]
    public class CharacterDailySetting
    {
        [Header("キャラクターID / 名前")]
        public string characterId;

        [Header("今日有効な行動（正解）")]
        [Tooltip("Torture, Talk, Negotiate の中から今日の正解を選択")]
        public InterrogationActionType correctAction = InterrogationActionType.Talk;

        [Header("--- アクションごとの返答セリフ ---")]
        public ActionResponseGroup tortureResponse;   // 拷問の時のセリフ
        public ActionResponseGroup talkResponse;      // 対話の時のセリフ
        public ActionResponseGroup negotiateResponse; // 交渉の時のセリフ

        [Header("--- 他の人について聞き取りをする場合のセリフリスト ---")]
        public List<HearingTargetResponse> hearingResponses = new List<HearingTargetResponse>();

        /// <summary>
        /// アクション種別と対象に応じて適切なセリフを取得する
        /// </summary>
        public string GetResponse(InterrogationActionType actionType, int selfAffection, Dictionary<string, int> otherAffections, string hearingTargetId = "")
        {
            switch (actionType)
            {
                case InterrogationActionType.Torture:
                    return tortureResponse.GetValidDialogue(selfAffection, otherAffections);

                case InterrogationActionType.Talk:
                    return talkResponse.GetValidDialogue(selfAffection, otherAffections);

                case InterrogationActionType.Negotiate:
                    return negotiateResponse.GetValidDialogue(selfAffection, otherAffections);

                case InterrogationActionType.HearAbout:
                    // 聞き取りの場合、指定した他キャラ用のセリフを探す
                    var targetHearing = hearingResponses.Find(h => h.targetCharacterId == hearingTargetId);
                    if (targetHearing != null)
                    {
                        return targetHearing.responseGroup.GetValidDialogue(selfAffection, otherAffections);
                    }
                    return "「その者については何も知りません。」";

                default:
                    return "「……」";
            }
        }
    }

    /// <summary>
    /// 1日分のシナリオ・会話設定データ（Day1, Day2... ごとに作成）
    /// </summary>
    [CreateAssetMenu(fileName = "Day1_Scenario", menuName = "Game/Day Scenario Data")]
    public class DayScenarioSO : ScriptableObject
    {
        [Header("何日目のデータか")]
        public int dayNumber = 1;

        [Header("登場する4人の今日のセリフ・正解設定")]
        public List<CharacterDailySetting> characterSettings = new List<CharacterDailySetting>();

        /// <summary>
        /// キャラクターIDから今日のセリフ設定を取得する
        /// </summary>
        public CharacterDailySetting GetCharacterSetting(string characterId)
        {
            return characterSettings.Find(c => c.characterId == characterId);
        }
    }
}