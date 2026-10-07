using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- ダイアログ表示パーツ ---")]
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Image characterIllustration;

        [Header("--- 文字送り設定 ---")]
        [SerializeField] private float textSpeed = 0.05f;

        // 内部状態
        public bool IsDialoguePlaying { get; private set; } = false;
        private Coroutine typingCoroutine;
        private string currentFullText = "";

        /// <summary>
        /// 会話テキストの再生を開始（表示専用）
        /// </summary>
        public void StartDialogueSequence(string speakerName, string rawText)
        {
            if (speakerNameText != null)
            {
                speakerNameText.text = speakerName;
            }

            // 特殊タグ除去などのパースが必要な場合はここで文字列だけを整形
            currentFullText = rawText;

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }

            typingCoroutine = StartCoroutine(TypeTextCoroutine(currentFullText));
        }

        /// <summary>
        /// タイピング演出コルーチン
        /// </summary>
        private IEnumerator TypeTextCoroutine(string fullText)
        {
            IsDialoguePlaying = true;
            if (dialogueText != null) dialogueText.text = "";

            foreach (char c in fullText.ToCharArray())
            {
                if (dialogueText != null)
                {
                    dialogueText.text += c;
                }
                yield return new WaitForSeconds(textSpeed);
            }

            IsDialoguePlaying = false;
        }

        /// <summary>
        /// 会話状態のリセット
        /// </summary>
        public void ResetDialogueState()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }

            IsDialoguePlaying = false;

            if (speakerNameText != null) speakerNameText.text = "";
            if (dialogueText != null) dialogueText.text = "";
        }

        /// <summary>
        /// クリック時のスキップ / 次へ送る（UIボタンやクリックイベント用）
        /// </summary>
        public void OnClickDialogueArea()
        {
            if (IsDialoguePlaying)
            {
                // タイピング中の場合は全文字を一括表示して演出スキップ
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                if (dialogueText != null) dialogueText.text = currentFullText;
                IsDialoguePlaying = false;
            }
            else
            {
                // 会話終了後のクリック通知などがあればControllerへ送る
            }
        }
    }
}