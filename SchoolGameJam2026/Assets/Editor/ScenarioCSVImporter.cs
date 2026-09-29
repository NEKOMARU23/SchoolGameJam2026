using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.Editor
{
    public class ScenarioCSVImporter : EditorWindow
    {
        private TextAsset csvFile;
        private string exportFolderPath = "Assets/Resources/Scenarios"; // 生成するSOの保存先

        [MenuItem("Tools/シナリオCSVインポーター")]
        public static void ShowWindow()
        {
            GetWindow<ScenarioCSVImporter>("シナリオCSVインポーター");
        }

        private void OnGUI()
        {
            GUILayout.Label("CSVファイルから DayScenarioSO を一括生成/更新", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            csvFile = (TextAsset)EditorGUILayout.ObjectField("シナリオCSVファイル", csvFile, typeof(TextAsset), false);
            exportFolderPath = EditorGUILayout.TextField("保存先フォルダ", exportFolderPath);

            EditorGUILayout.Space();

            if (GUILayout.Button("CSVをインポートしてSOを生成", GUILayout.Height(40)))
            {
                if (csvFile == null)
                {
                    EditorUtility.DisplayDialog("エラー", "CSVファイルをセットしてください。", "OK");
                    return;
                }

                ImportCSV();
            }
        }

        private void ImportCSV()
        {
            // 保存先フォルダの存在確認・作成
            if (!Directory.Exists(exportFolderPath))
            {
                Directory.CreateDirectory(exportFolderPath);
            }

            // CSV行データ定義
            var lines = csvFile.text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1)
            {
                Debug.LogError("CSVファイルが空か、ヘッダーしかありません。");
                return;
            }

            // 日数ごとにデータをまとめる辞書 <DayNumber, List<RowData>>
            var dayDataDict = new Dictionary<int, List<CSVRow>>();

            // 1行目はヘッダーなので2行目(i=1)から読み込み
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCSVLine(lines[i]);
                if (cols.Count < 11) continue;

                try
                {
                    var row = new CSVRow
                    {
                        DayNumber = int.Parse(cols[0]),
                        CharacterId = cols[1].Trim(),
                        CorrectAction = Enum.Parse<InterrogationActionType>(cols[2].Trim(), true),
                        PenaltyAction = Enum.Parse<InterrogationActionType>(cols[3].Trim(), true),
                        ActionType = Enum.Parse<InterrogationActionType>(cols[4].Trim(), true),
                        HearingTargetId = cols[5].Trim(),
                        Priority = int.Parse(cols[6]),
                        ConditionType = Enum.Parse<DialogueConditionType>(cols[7].Trim(), true),
                        TargetCharacterId = cols[8].Trim(),
                        RequiredValue = string.IsNullOrEmpty(cols[9]) ? 0 : int.Parse(cols[9]),
                        DialogueText = cols[10].Replace("\\n", "\n") // \n を改行コードに変換
                    };

                    if (!dayDataDict.ContainsKey(row.DayNumber))
                    {
                        dayDataDict[row.DayNumber] = new List<CSVRow>();
                    }
                    dayDataDict[row.DayNumber].Add(row);
                }
                catch (Exception e)
                {
                    Debug.LogError($"CSV {i + 1} 行目の読み込みでエラーが発生しました: {e.Message}");
                }
            }

            // 日数ごとに DayScenarioSO を生成・更新
            foreach (var kvp in dayDataDict)
            {
                int dayNum = kvp.Key;
                var rows = kvp.Value;

                string assetPath = $"{exportFolderPath}/Day{dayNum}_Scenario.asset";
                DayScenarioSO scenarioSO = AssetDatabase.LoadAssetAtPath<DayScenarioSO>(assetPath);

                if (scenarioSO == null)
                {
                    scenarioSO = ScriptableObject.CreateInstance<DayScenarioSO>();
                    AssetDatabase.CreateAsset(scenarioSO, assetPath);
                }

                scenarioSO.dayNumber = dayNum;
                scenarioSO.characterSettings = BuildCharacterSettings(rows);

                EditorUtility.SetDirty(scenarioSO);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完了", "シナリオデータのインポートが完了しました！", "OK");
        }

        private List<CharacterDailySetting> BuildCharacterSettings(List<CSVRow> rows)
        {
            var charDict = new Dictionary<string, CharacterDailySetting>();

            foreach (var row in rows)
            {
                if (!charDict.TryGetValue(row.CharacterId, out var charSetting))
                {
                    charSetting = new CharacterDailySetting
                    {
                        characterId = row.CharacterId,
                        correctAction = row.CorrectAction,
                        penaltyAction = row.PenaltyAction,
                        tortureResponse = new ActionResponseGroup(),
                        talkResponse = new ActionResponseGroup(),
                        negotiateResponse = new ActionResponseGroup(),
                        releaseResponse = new ActionResponseGroup(),
                        confirmAIResponse = new ActionResponseGroup(),
                        hearingResponses = new List<HearingTargetResponse>()
                    };
                    charDict[row.CharacterId] = charSetting;
                }

                // セリフデータ作成
                var conditionalDialogue = new ConditionalDialogue
                {
                    conditionType = row.ConditionType,
                    targetCharacterId = row.TargetCharacterId,
                    requiredValue = row.RequiredValue,
                    dialogueText = row.DialogueText
                };

                // アクション種別に応じてグループに振分け
                switch (row.ActionType)
                {
                    case InterrogationActionType.Torture:
                        AddPriorityDialogue(charSetting.tortureResponse, conditionalDialogue, row.Priority);
                        break;
                    case InterrogationActionType.Talk:
                        AddPriorityDialogue(charSetting.talkResponse, conditionalDialogue, row.Priority);
                        break;
                    case InterrogationActionType.Negotiate:
                        AddPriorityDialogue(charSetting.negotiateResponse, conditionalDialogue, row.Priority);
                        break;
                    case InterrogationActionType.Release:
                        AddPriorityDialogue(charSetting.releaseResponse, conditionalDialogue, row.Priority);
                        break;
                    case InterrogationActionType.ConfirmAI:
                        AddPriorityDialogue(charSetting.confirmAIResponse, conditionalDialogue, row.Priority);
                        break;
                    case InterrogationActionType.HearAbout:
                        var targetHearing = charSetting.hearingResponses.Find(h => h.targetCharacterId == row.HearingTargetId);
                        if (targetHearing == null)
                        {
                            targetHearing = new HearingTargetResponse
                            {
                                targetCharacterId = row.HearingTargetId,
                                responseGroup = new ActionResponseGroup()
                            };
                            charSetting.hearingResponses.Add(targetHearing);
                        }
                        AddPriorityDialogue(targetHearing.responseGroup, conditionalDialogue, row.Priority);
                        break;
                }
            }

            return new List<CharacterDailySetting>(charDict.Values);
        }

        private void AddPriorityDialogue(ActionResponseGroup group, ConditionalDialogue dialogue, int priority)
        {
            while (group.priorityDialogueList.Count < priority)
            {
                group.priorityDialogueList.Add(null);
            }
            group.priorityDialogueList[priority - 1] = dialogue;
        }

        // CSVのカンマ区切り解析 (ダブルクォーテーション対応)
        private List<string> ParseCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            StringBuilder current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result;
        }

        private class CSVRow
        {
            public int DayNumber;
            public string CharacterId;
            public InterrogationActionType CorrectAction;
            public InterrogationActionType PenaltyAction;
            public InterrogationActionType ActionType;
            public string HearingTargetId;
            public int Priority;
            public DialogueConditionType ConditionType;
            public string TargetCharacterId;
            public int RequiredValue;
            public string DialogueText;
        }
    }
}