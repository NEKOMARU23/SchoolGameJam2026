using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 「はい / いいえ」の確認ダイアログ（文言と「はい」のときの処理は呼び出し側が渡す）
    /// 取り返しのつかない操作（AI確定・解放・プロフィールの解禁）の前に出す
    /// </summary>
    public class ConfirmDialogUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action onYes;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (yesButton != null) yesButton.onClick.AddListener(OnClickYes);
            if (noButton != null) noButton.onClick.AddListener(Hide);
        }

        /// <summary>
        /// 確認を表示する
        /// </summary>
        /// <param name="message">確認の文（例: ココアを AI と判定しますか？）</param>
        /// <param name="yesAction">「はい」を押したときの処理</param>
        public void Show(string message, Action yesAction)
        {
            if (messageText != null) messageText.text = message;
            onYes = yesAction;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            onYes = null;
            gameObject.SetActive(false);
        }

        private void OnClickYes()
        {
            // 先に閉じてから処理する（処理の中で別の確認を出せるように）
            Action action = onYes;
            Hide();
            action?.Invoke();
        }
    }
}
