using UnityEngine;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// ログアイテムUIの制御クラス
    /// </summary>
    public class LogItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueContentText;

        public void SetLog(string speakerName, string content)
        {
            if (speakerNameText != null) speakerNameText.text = speakerName;
            if (dialogueContentText != null) dialogueContentText.text = content;
        }
    }
}