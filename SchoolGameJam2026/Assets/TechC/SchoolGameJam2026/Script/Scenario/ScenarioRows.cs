using System;
using System.Collections.Generic;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.Scenario
{
    /// <summary> 本文シートの「種類」 </summary>
    public enum PageKind
    {
        Talk,       // 会話（話者名あり）
        Narration   // 地の文（話者名なし）
    }

    /// <summary> 条件シートの「行動」（取り調べの行動 + スマホ + 朝） </summary>
    public enum ScenarioAction
    {
        Torture,
        Talk,
        Negotiate,
        HearAbout,
        Release,
        ConfirmAI,
        Smartphone,
        Morning     // 朝: その日の始めに自動で流れるスマホの会話
    }

    /// <summary> エンディングシートの「判定結果」 </summary>
    public enum EndingResult
    {
        AllCorrect,         // 全員正解
        ConfirmedHuman,     // 人間をAI確定した
        ReleasedAI,         // AIを解放した
        Any                 // 指定なし
    }

    /// <summary> エンディングシートの「種類」 </summary>
    public enum EndingKind
    {
        Clear,
        GameOver
    }

    /// <summary> フラグ一覧シートの「種類」 </summary>
    public enum FlagKind
    {
        KeyItem,
        Info,
        Progress
    }

    /// <summary> 本文シートの1行（会話の1ページ） </summary>
    [Serializable]
    public class ScenarioPage
    {
        public PageKind kind = PageKind.Talk;
        public string speaker = "";     // 空なら会話の相手
        public string text = "";
        public string se = "";
        public string character = "";   // 立ち絵（「非表示」で消す）
        public string background = "";
        public string eventCG = "";
        public string effect = "";
        public string bgm = "";
    }

    /// <summary> 本文シートのセリフID 1つ分（複数ページ） </summary>
    [Serializable]
    public class ScenarioDialogue
    {
        public string id = "";
        public List<ScenarioPage> pages = new List<ScenarioPage>();
    }

    /// <summary> 他キャラの好感度の条件（例: B>=2） </summary>
    [Serializable]
    public class AffectionCondition
    {
        public string characterId = "";
        public int minValue;
    }

    /// <summary> 条件シートの1行 </summary>
    [Serializable]
    public class ScenarioConditionRow
    {
        public int day;
        public string characterId = "";         // スマホ・朝は空
        public ScenarioAction action;
        public string hearingTargetId = "";     // 聞き取りのときだけ
        public int talkNumber;                  // スマホのときだけ（回目）
        public int priority;

        public bool hasSelfAffection;
        public int selfAffection;
        public List<AffectionCondition> otherAffections = new List<AffectionCondition>();
        public bool hasActionCount;
        public int actionCount;
        public List<string> requiredFlags = new List<string>();
        public List<string> ngFlags = new List<string>();
        public List<string> gainFlags = new List<string>();
        public List<string> aiTargets = new List<string>();     // この会話を聞いたら AI になるキャラ

        public string dialogueId = "";
        public string sourceLocation = "";      // エラー表示用（例: 条件 12行目）
    }

    /// <summary> 選択肢シートの1行（会話の後に出す選択肢1つ） </summary>
    [Serializable]
    public class ScenarioChoice
    {
        public string dialogueId = "";          // この会話の後に出す
        public int order;                       // 並べる順番（1, 2, 3 …）
        public string text = "";                // ボタンに表示する文

        public bool hasSelfAffection;
        public int selfAffection;
        public List<AffectionCondition> otherAffections = new List<AffectionCondition>();
        public List<string> requiredFlags = new List<string>();
        public List<string> ngFlags = new List<string>();

        public int affectionChange;             // 選んだときの、会話の相手の好感度の増減
        public List<string> gainFlags = new List<string>();
        public List<string> aiTargets = new List<string>();
        public string nextDialogueId = "";      // 選んだ後に出す会話（空なら会話はそこで終わる）
        public string sourceLocation = "";
    }

    /// <summary> 日別設定シートの1行 </summary>
    [Serializable]
    public class ScenarioDailySetting
    {
        public int day;
        public string characterId = "";
        public InterrogationActionType correctAction;
        public bool hasNgAction;
        public InterrogationActionType ngAction;
    }

    /// <summary> フラグ一覧シートの1行 </summary>
    [Serializable]
    public class ScenarioFlagDefinition
    {
        public string name = "";
        public FlagKind kind;
        public string description = "";
        public string profileText = "";
        public string displayTarget = "";       // A〜D / その他 / アイテム / なし
    }

    /// <summary> AI判定シートの1行 </summary>
    [Serializable]
    public class ScenarioAIJudgement
    {
        public string characterId = "";
        public int day;
        public List<string> avoidFlags = new List<string>();
    }

    /// <summary> エンディングシートの1行 </summary>
    [Serializable]
    public class ScenarioEnding
    {
        public int priority;
        public EndingResult result = EndingResult.Any;
        public List<string> requiredFlags = new List<string>();
        public List<string> ngFlags = new List<string>();
        public EndingKind kind = EndingKind.Clear;
        public string endingId = "";
        public string listName = "";
    }
}
