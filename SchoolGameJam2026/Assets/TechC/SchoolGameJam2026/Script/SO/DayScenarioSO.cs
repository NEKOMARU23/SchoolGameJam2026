using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.SO
{
    /// <summary>
    /// 取り調べ時の行動種別（解放とAI確定を追加）
    /// </summary>
    public enum InterrogationActionType
    {
        Torture,    // 1. 拷問
        Talk,       // 2. 対話
        Negotiate,  // 3. 交渉
        HearAbout,  // 4. 他の人について聞き取り
        Release,    // 5. 解放
        ConfirmAI   // 6. AI確定（排除/判定）
    }

    /// <summary>
    /// セリフの判定条件タイプ
    /// </summary>
    public enum DialogueConditionType
    {
        TargetAffection, // 1. 対象キャラ(本人)の内部好感度
        OtherAffection,  // 2. その他のキャラの内部好感度
        TotalActions,    // 3. 対象キャラへの「該当アクション」の通算実行回数
        Unconditional    // 4. 条件なし（デフォルトセリフ）
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

        [Header("必要数値（好感度条件、または対象アクションの通算回数）")]
        [Tooltip("好感度条件なら『必要好感度』、通算回数条件なら『このキャラへこのアクションを行った通算回数がこの値以上』で成立します")]
        public int requiredValue = 0;

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
        /// 現在の状況（好感度、このアクションのキャラ別通算回数）から、最も優先度の高いセリフと採用されたElement番号を取得する
        /// </summary>
        public (string dialogueText, int elementIndex) GetValidDialogueWithIndex(int selfAffection, Dictionary<string, int> otherAffections, int totalActionCountForThisType)
        {
            if (priorityDialogueList == null || priorityDialogueList.Count == 0)
            {
                return ("「……」", -1);
            }

            for (int i = 0; i < priorityDialogueList.Count; i++)
            {
                var dialogue = priorityDialogueList[i];
                switch (dialogue.conditionType)
                {
                    case DialogueConditionType.TargetAffection:
                        if (selfAffection >= dialogue.requiredValue)
                            return (dialogue.dialogueText, i);
                        break;

                    case DialogueConditionType.OtherAffection:
                        if (otherAffections != null && !string.IsNullOrEmpty(dialogue.targetCharacterId))
                        {
                            if (otherAffections.TryGetValue(dialogue.targetCharacterId, out int otherValue))
                            {
                                if (otherValue >= dialogue.requiredValue)
                                    return (dialogue.dialogueText, i);
                            }
                        }
                        break;

                    case DialogueConditionType.TotalActions:
                        if (totalActionCountForThisType >= dialogue.requiredValue)
                            return (dialogue.dialogueText, i);
                        break;

                    case DialogueConditionType.Unconditional:
                        return (dialogue.dialogueText, i);
                }
            }

            return ("「……」", -1);
        }

        /// <summary>
        /// （互換用）セリフテキストのみを取得する
        /// </summary>
        public string GetValidDialogue(int selfAffection, Dictionary<string, int> otherAffections, int totalActionCountForThisType)
        {
            return GetValidDialogueWithIndex(selfAffection, otherAffections, totalActionCountForThisType).dialogueText;
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

        [Header("そのキャラについて話すセリフ（条件付き）")]
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

        [Header("今日好感度が下がる行動（不正解/NG行動）")]
        [Tooltip("この行動を選択した時のみ好感度が下がります")]
        public InterrogationActionType penaltyAction = InterrogationActionType.Torture;

        [Header("--- アクションごとの返答セリフ ---")]
        public ActionResponseGroup tortureResponse;   // 拷問の時のセリフ
        public ActionResponseGroup talkResponse;      // 対話の時のセリフ
        public ActionResponseGroup negotiateResponse; // 交渉の時のセリフ

        [Header("--- 解放・AI確定時のセリフ ---")]
        public ActionResponseGroup releaseResponse;   // 解放された時のセリフ
        public ActionResponseGroup confirmAIResponse; // AI確定（処分/排斥）された時のセリフ

        [Header("--- 他の人について聞き取りをする場合のセリフリスト ---")]
        public List<HearingTargetResponse> hearingResponses = new List<HearingTargetResponse>();

        /// <summary>
        /// アクション種別と対象に応じて適切なセリフと採用Element番号を取得する
        /// </summary>
        public (string dialogueText, int elementIndex) GetResponseWithIndex(InterrogationActionType actionType, int selfAffection, Dictionary<string, int> otherAffections, int totalActionCountForThisType, string hearingTargetId = "")
        {
            ActionResponseGroup targetGroup = null;

            switch (actionType)
            {
                case InterrogationActionType.Torture:
                    targetGroup = tortureResponse;
                    break;

                case InterrogationActionType.Talk:
                    targetGroup = talkResponse;
                    break;

                case InterrogationActionType.Negotiate:
                    targetGroup = negotiateResponse;
                    break;

                case InterrogationActionType.Release:
                    return (releaseResponse != null ? releaseResponse.GetValidDialogue(selfAffection, otherAffections, totalActionCountForThisType) : "解放される……ということですか。わかりました。|（対象は部屋から退出しました）", -1);

                case InterrogationActionType.ConfirmAI:
                    return (confirmAIResponse != null ? confirmAIResponse.GetValidDialogue(selfAffection, otherAffections, totalActionCountForThisType) : "【判定完了】対象をAIと判定し、排除シーケンスを完了しました。", -1);

                case InterrogationActionType.HearAbout:
                    var targetHearing = hearingResponses.Find(h => h.targetCharacterId == hearingTargetId);
                    if (targetHearing != null && targetHearing.responseGroup != null)
                    {
                        return targetHearing.responseGroup.GetValidDialogueWithIndex(selfAffection, otherAffections, totalActionCountForThisType);
                    }
                    return ("「その者については何も知りません。」", -1);

                default:
                    return ("「……」", -1);
            }

            if (targetGroup != null)
            {
                return targetGroup.GetValidDialogueWithIndex(selfAffection, otherAffections, totalActionCountForThisType);
            }

            return ("「……」", -1);
        }

        public string GetResponse(InterrogationActionType actionType, int selfAffection, Dictionary<string, int> otherAffections, int totalActionCountForThisType, string hearingTargetId = "")
        {
            return GetResponseWithIndex(actionType, selfAffection, otherAffections, totalActionCountForThisType, hearingTargetId).dialogueText;
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