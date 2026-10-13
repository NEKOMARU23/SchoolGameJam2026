using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core.State;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// クリア画面・ゲームオーバー画面で、今遊んだゲームのセーブを消すかどうかを聞く
    /// （セーブしていない・ロードしていないゲームなら何も出さない）
    /// </summary>
    public class SaveDeletePromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private int slot;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (yesButton != null) yesButton.onClick.AddListener(DeleteAndClose);
            if (noButton != null) noButton.onClick.AddListener(Close);
        }

        private void Start()
        {
            slot = SaveStore.CurrentSlot;
            bool ask = SaveStore.Exists(slot);
            if (ask && messageText != null) messageText.text = $"今回のゲームのセーブ（{slot}番）を消しますか？";
            if (panel != null) panel.SetActive(ask);
        }

        private void DeleteAndClose()
        {
            SaveStore.Delete(slot);
            Close();
        }

        private void Close()
        {
            SaveStore.CurrentSlot = 0;
            if (panel != null) panel.SetActive(false);
        }
    }
}
