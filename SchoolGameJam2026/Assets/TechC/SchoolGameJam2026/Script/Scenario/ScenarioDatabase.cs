using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Scenario
{
    [Serializable]
    public class NamedSprite
    {
        public string name;
        public Sprite sprite;
    }

    [Serializable]
    public class NamedAudioClip
    {
        public string name;
        public AudioClip clip;
    }

    [Serializable]
    public class NamedObject
    {
        public string name;
        public GameObject prefab;
    }

    /// <summary>
    /// Excel（ScenarioData.xlsx）を取り込んで作るシナリオのデータ
    /// 手で編集せず、Tools ＞ シナリオExcelを取り込む で作り直す
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDatabase", menuName = "Game/Scenario Database")]
    public class ScenarioDatabase : ScriptableObject
    {
        [Header("Excel から取り込んだデータ")]
        public List<ScenarioDialogue> dialogues = new List<ScenarioDialogue>();
        public List<ScenarioConditionRow> conditions = new List<ScenarioConditionRow>();
        public List<ScenarioDailySetting> dailySettings = new List<ScenarioDailySetting>();
        public List<ScenarioFlagDefinition> flags = new List<ScenarioFlagDefinition>();
        public List<ScenarioAIJudgement> aiJudgements = new List<ScenarioAIJudgement>();
        public List<ScenarioEnding> endings = new List<ScenarioEnding>();

        [Header("Data フォルダの素材（名前 = ファイル名）")]
        public List<NamedSprite> characterSprites = new List<NamedSprite>();
        public List<NamedSprite> backgrounds = new List<NamedSprite>();
        public List<NamedSprite> eventCGs = new List<NamedSprite>();
        public List<NamedObject> effects = new List<NamedObject>();
        public List<NamedAudioClip> seClips = new List<NamedAudioClip>();
        public List<NamedAudioClip> bgmClips = new List<NamedAudioClip>();

        [NonSerialized] private Dictionary<string, ScenarioDialogue> dialogueMap;

        /// <summary> 取り込み直したときなどに、検索用の情報を作り直す </summary>
        public void ClearCache()
        {
            dialogueMap = null;
        }

        public ScenarioDialogue FindDialogue(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (dialogueMap == null)
            {
                dialogueMap = new Dictionary<string, ScenarioDialogue>(StringComparer.Ordinal);
                foreach (ScenarioDialogue d in dialogues)
                {
                    if (d != null && !string.IsNullOrEmpty(d.id)) dialogueMap[d.id] = d;
                }
            }
            return dialogueMap.TryGetValue(id, out ScenarioDialogue found) ? found : null;
        }

        /// <summary>
        /// 条件シートから、その場面の候補の行を優先度順に取り出す
        /// </summary>
        public List<ScenarioConditionRow> GetConditionRows(int day, string characterId, ScenarioAction action, string hearingTargetId = "", int talkNumber = 0)
        {
            var rows = new List<ScenarioConditionRow>();
            foreach (ScenarioConditionRow row in conditions)
            {
                if (row.day != day || row.action != action) continue;

                if (action == ScenarioAction.Smartphone)
                {
                    if (row.talkNumber != talkNumber) continue;
                }
                else
                {
                    if (row.characterId != characterId) continue;
                    if (action == ScenarioAction.HearAbout && row.hearingTargetId != (hearingTargetId ?? "")) continue;
                }
                rows.Add(row);
            }
            rows.Sort((a, b) => a.priority.CompareTo(b.priority));
            return rows;
        }

        public ScenarioDailySetting GetDailySetting(int day, string characterId)
        {
            return dailySettings.Find(s => s.day == day && s.characterId == characterId);
        }

        /// <summary> その日のデータ（条件・日別設定）があるか </summary>
        public bool HasDay(int day)
        {
            return conditions.Exists(c => c.day == day) || dailySettings.Exists(s => s.day == day);
        }

        /// <summary> その日にスマホで聞ける回数（書かれた「回目」の最大） </summary>
        public int GetSmartphoneTalkCount(int day)
        {
            int max = 0;
            foreach (ScenarioConditionRow row in conditions)
            {
                if (row.day == day && row.action == ScenarioAction.Smartphone) max = Mathf.Max(max, row.talkNumber);
            }
            return max;
        }

        public ScenarioFlagDefinition FindFlag(string name)
        {
            return flags.Find(f => f.name == name);
        }

        public Sprite FindCharacterSprite(string name) => Find(characterSprites, name)?.sprite;
        public Sprite FindBackground(string name) => Find(backgrounds, name)?.sprite;
        public Sprite FindEventCG(string name) => Find(eventCGs, name)?.sprite;
        public GameObject FindEffect(string name) => effects.Find(e => e.name == name)?.prefab;
        public AudioClip FindSE(string name) => seClips.Find(c => c.name == name)?.clip;
        public AudioClip FindBGM(string name) => bgmClips.Find(c => c.name == name)?.clip;

        private static NamedSprite Find(List<NamedSprite> list, string name)
        {
            return string.IsNullOrEmpty(name) ? null : list.Find(s => s.name == name);
        }
    }
}
