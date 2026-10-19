using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// タイトル画面のエンディング一覧
    /// まだ見ていないエンディングは「？？？」にして数だけ分かるようにし、見たエンディングはもう一度再生できる
    /// </summary>
    public class EndingListUI : MonoBehaviour
    {
        public const string HiddenName = "？？？";

        [SerializeField] private ScenarioDatabase scenarioDatabase;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private RectTransform entryContainer;
        [Tooltip("複製して使う1行分のボタン（非表示にしておく）")]
        [SerializeField] private Button entryTemplate;
        [SerializeField] private EndingPlayerUI endingPlayer;

        private readonly List<Button> entries = new List<Button>();

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary> エンディングの会話を探すシナリオのデータ（デバッグメニューでも使う） </summary>
        public ScenarioDatabase Database => scenarioDatabase;

        /// <summary> 表示中の各行の文（テスト・確認用） </summary>
        public List<string> ShownNames { get; } = new List<string>();

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        public void Open()
        {
            if (panel == null || scenarioDatabase == null) return;

            List<ScenarioEnding> endings = GetListedEndings();
            int seen = 0;
            ShownNames.Clear();

            for (int i = 0; i < endings.Count; i++)
            {
                ScenarioEnding ending = endings[i];
                bool isSeen = EndingRecordStore.IsSeen(ending.endingId);
                if (isSeen) seen++;

                string name = isSeen ? (string.IsNullOrEmpty(ending.listName) ? ending.endingId : ending.listName) : HiddenName;
                ShownNames.Add(name);

                Button entry = GetOrCreateEntry(i);
                entry.gameObject.SetActive(true);
                entry.interactable = isSeen;
                TextMeshProUGUI label = entry.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = $"{i + 1}. {name}";

                entry.onClick.RemoveAllListeners();
                if (isSeen) entry.onClick.AddListener(() => Replay(ending));
            }
            for (int i = endings.Count; i < entries.Count; i++) entries[i].gameObject.SetActive(false);

            if (countText != null) countText.text = $"見たエンディング {seen} / {endings.Count}";
            panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary> 見たエンディングをもう一度再生し、終わったら一覧に戻る </summary>
        private void Replay(ScenarioEnding ending)
        {
            ScenarioDialogue dialogue = scenarioDatabase.FindDialogue(ending.endingId);
            if (dialogue == null || endingPlayer == null) return;

            Close();
            endingPlayer.Play(dialogue, scenarioDatabase, Open);
        }

        /// <summary>
        /// 一覧に並べるエンディング（優先度順。同じエンディングIDが複数の行にあれば1つにまとめる）
        /// </summary>
        private List<ScenarioEnding> GetListedEndings()
        {
            var sorted = new List<ScenarioEnding>(scenarioDatabase.endings);
            sorted.Sort((a, b) => a.priority.CompareTo(b.priority));

            var result = new List<ScenarioEnding>();
            var ids = new HashSet<string>();
            foreach (ScenarioEnding ending in sorted)
            {
                if (ids.Add(ending.endingId)) result.Add(ending);
            }
            return result;
        }

        private Button GetOrCreateEntry(int index)
        {
            while (entries.Count <= index)
            {
                Button entry = Instantiate(entryTemplate, entryContainer);
                entry.name = $"EndingEntry_{entries.Count + 1}";
                entries.Add(entry);
            }
            return entries[index];
        }
    }
}
