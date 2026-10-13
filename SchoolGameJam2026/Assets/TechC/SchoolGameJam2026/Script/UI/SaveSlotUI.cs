using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary> セーブ枠1つ分の表示内容 </summary>
    public struct SaveSlotView
    {
        public int Slot;
        public string Label;        // 例: 03  3日目  残り行動 2回  2026/10/13 15:20
        public bool CanSelect;      // 押せるか（ロードでは空き枠は押せない）
        public bool CanDelete;      // 削除ボタンを出すか（使用中の枠だけ）
    }

    /// <summary>
    /// セーブ・ロードの枠の一覧（表示するだけ。押したときの処理は SaveLoadController が決める）
    /// </summary>
    public class SaveSlotUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private RectTransform slotContainer;
        [Tooltip("複製して使う1枠分（子に DeleteButton と Label を持つボタン。非表示にしておく）")]
        [SerializeField] private Button slotTemplate;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI noticeText;
        [SerializeField] private ConfirmDialogUI confirmDialog;

        private readonly List<Button> slots = new List<Button>();
        private Action<int> onSelect;
        private Action<int> onDelete;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary> 表示中の各枠の文（テスト・確認用） </summary>
        public List<string> ShownLabels { get; } = new List<string>();

        /// <summary> 最後に出したお知らせ（テスト・確認用） </summary>
        public string Notice => noticeText != null ? noticeText.text : "";

        public event Action OnClosed;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        public void Show(string title, IReadOnlyList<SaveSlotView> views, Action<int> select, Action<int> delete)
        {
            onSelect = select;
            onDelete = delete;
            if (titleText != null) titleText.text = title;
            SetSlots(views);
            ShowNotice("");
            panel.SetActive(true);
        }

        /// <summary> 枠の内容だけを描き直す（セーブ・削除の後） </summary>
        public void SetSlots(IReadOnlyList<SaveSlotView> views)
        {
            ShownLabels.Clear();
            for (int i = 0; i < views.Count; i++)
            {
                SaveSlotView view = views[i];
                ShownLabels.Add(view.Label);

                Button slot = GetOrCreateSlot(i);
                slot.gameObject.SetActive(true);
                slot.interactable = view.CanSelect;
                slot.onClick.RemoveAllListeners();
                slot.onClick.AddListener(() => onSelect?.Invoke(view.Slot));

                Transform labelTransform = slot.transform.Find("Label");
                TextMeshProUGUI label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
                if (label != null) label.text = view.Label;

                Transform deleteTransform = slot.transform.Find("DeleteButton");
                Button delete = deleteTransform != null ? deleteTransform.GetComponent<Button>() : null;
                if (delete != null)
                {
                    delete.gameObject.SetActive(view.CanDelete);
                    delete.onClick.RemoveAllListeners();
                    delete.onClick.AddListener(() => onDelete?.Invoke(view.Slot));
                }
            }
            for (int i = views.Count; i < slots.Count; i++) slots[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (confirmDialog != null && confirmDialog.IsOpen) confirmDialog.Hide();
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
                OnClosed?.Invoke();
            }
        }

        public void ShowNotice(string message)
        {
            if (noticeText != null) noticeText.text = message;
        }

        /// <summary> 確認を出し、「はい」なら処理する </summary>
        public void Confirm(string message, Action onYes)
        {
            if (confirmDialog != null) confirmDialog.Show(message, onYes);
            else onYes?.Invoke();
        }

        private Button GetOrCreateSlot(int index)
        {
            while (slots.Count <= index)
            {
                Button slot = Instantiate(slotTemplate, slotContainer);
                slot.name = $"SaveSlot_{slots.Count + 1:00}";
                slots.Add(slot);
            }
            return slots[index];
        }
    }
}
