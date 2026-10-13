using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 会話の後に出す選択肢の表示（表示するだけ。どれを出すかと選んだ後の処理は呼び出し側が決める）
    /// 画面全体を覆うので、選ぶまで後ろの画面は操作できない（必ずどれかを選ぶ）
    /// </summary>
    public class ChoiceUI : MonoBehaviour
    {
        [Tooltip("選択肢のボタンを並べる親")]
        [SerializeField] private RectTransform buttonContainer;
        [Tooltip("複製して使う選択肢ボタン（非表示にしておく）")]
        [SerializeField] private Button buttonTemplate;

        private readonly List<Button> buttons = new List<Button>();
        private Action<int> onSelected;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary> 表示中の選択肢の文（テスト・確認用） </summary>
        public IReadOnlyList<string> ShownTexts { get; private set; } = Array.Empty<string>();

        private void Awake()
        {
            if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);
        }

        /// <summary>
        /// 選択肢を表示する
        /// </summary>
        /// <param name="texts">上から並べる選択肢の文</param>
        /// <param name="selected">選ばれたときに、選ばれた番号（0 始まり）で呼ばれる</param>
        public void Show(IReadOnlyList<string> texts, Action<int> selected)
        {
            onSelected = selected;
            ShownTexts = new List<string>(texts);

            for (int i = 0; i < texts.Count; i++)
            {
                Button button = GetOrCreateButton(i);
                button.gameObject.SetActive(true);
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = texts[i];
            }
            for (int i = texts.Count; i < buttons.Count; i++) buttons[i].gameObject.SetActive(false);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            onSelected = null;
            ShownTexts = Array.Empty<string>();
            gameObject.SetActive(false);
        }

        /// <summary> 番号を指定して選ぶ（ボタンから呼ばれる） </summary>
        public void Select(int index)
        {
            if (!IsOpen || index < 0 || index >= ShownTexts.Count) return;

            // 先に閉じてから知らせる（選んだ後の会話で、また選択肢を出せるように）
            Action<int> callback = onSelected;
            Hide();
            callback?.Invoke(index);
        }

        private Button GetOrCreateButton(int index)
        {
            while (buttons.Count <= index)
            {
                Button button = Instantiate(buttonTemplate, buttonContainer);
                button.name = $"ChoiceButton_{buttons.Count + 1}";
                int captured = buttons.Count;
                button.onClick.AddListener(() => Select(captured));
                buttons.Add(button);
            }
            return buttons[index];
        }
    }
}
