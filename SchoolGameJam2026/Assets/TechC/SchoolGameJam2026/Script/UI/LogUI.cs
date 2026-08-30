using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    public class LogUI : MonoBehaviour
    {
        [Header("--- UI参照 ---")]
        [SerializeField] private Transform contentTransform; // LogContentParent -> Viewport -> Content
        [SerializeField] private Button backButton;           // Button_Back
        [SerializeField] private ScrollRect scrollRect;        // LogContentParent (ScrollRect)

        [Header("--- ログ要素Prefab ---")]
        [SerializeField] private LogItemUI logItemPrefab;     // LogItemUIがアタッチされたPrefab

        private void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(Hide);
            }
        }

        /// <summary>
        /// ログパネルを開く
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f; // 最下部へスクロール
            }
        }

        /// <summary>
        /// ログパネルを閉じる
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 会話ログを1件追加
        /// </summary>
        public void AddLog(string speakerName, string text)
        {
            if (logItemPrefab == null || contentTransform == null) return;

            LogItemUI item = Instantiate(logItemPrefab, contentTransform);
            item.SetLog(speakerName, text);
        }

        /// <summary>
        /// ログを全削除（日付変更時など）
        /// </summary>
        public void ClearLog()
        {
            if (contentTransform == null) return;

            foreach (Transform child in contentTransform)
            {
                Destroy(child.gameObject);
            }
        }
    }
}