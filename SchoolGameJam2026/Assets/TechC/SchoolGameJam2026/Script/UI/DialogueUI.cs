using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 会話テキストの表示（ページ送り・文字送り・演出タグの適用）を担当するビュー
    /// 書式の解析は <see cref="DialogueTextFormat"/> が行う
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("--- ダイアログ表示パーツ ---")]
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Image characterIllustration;

        [Header("--- 文字送り設定 ---")]
        [SerializeField] private float textSpeed = 0.05f;

        [Header("--- オート・スキップ ---")]
        [Tooltip("オートで次のページへ進むまでの待ち時間（オプションの設定で上書きされる）")]
        [SerializeField] private float autoWaitSeconds = 1.2f;
        [Tooltip("スキップ中に1ページを送る間隔（秒）")]
        [SerializeField] private float skipInterval = 0.06f;

        /// <summary> 全ページの表示が完了したときに通知 </summary>
        public event Action OnDialogueFinished;

        /// <summary> スキップできない（未読の）会話に来て、スキップが止まったときに通知 </summary>
        public event Action OnSkipStopped;

        public bool IsAuto { get; private set; }
        public bool IsSkipping { get; private set; }

        private bool currentSkippable = true;   // 今の会話をスキップしてよいか（既読、またはスキップの種類が「すべて」）
        private float modeTimer;

        /// <summary> 文字送り中、または未表示のページが残っているか </summary>
        public bool IsDialoguePlaying { get; private set; } = false;

        private readonly Queue<DialoguePage> pages = new Queue<DialoguePage>();
        private string currentSpeakerName = "";

        // 素材を名前から探す処理（GameUIController から渡される）
        private Func<string, Sprite> findCharacterSprite;
        private Action<string> playSe;

        /// <summary>
        /// 立ち絵の画像と SE を名前から探す処理を設定する
        /// </summary>
        public void SetAssetResolvers(Func<string, Sprite> characterSpriteFinder, Action<string> sePlayer)
        {
            findCharacterSprite = characterSpriteFinder;
            playSe = sePlayer;
        }
        private Coroutine typingCoroutine;
        private bool isTyping = false;

        /// <summary>
        /// 会話テキストの再生を開始
        /// </summary>
        public void StartDialogueSequence(string speakerName, string rawText)
        {
            StartDialogue(speakerName, DialogueTextFormat.Parse(rawText));
        }

        /// <summary>
        /// ページの一覧（Excel の会話など）の再生を開始
        /// </summary>
        /// <param name="speakerName">話者の指定がないページで表示する名前（会話の相手）</param>
        /// <param name="skippable">スキップしてよい会話か（既読、またはスキップの種類が「すべて」。判断は呼び出し側）</param>
        public void StartDialogue(string speakerName, IReadOnlyList<DialoguePage> dialoguePages, bool skippable = true)
        {
            StopTyping();
            pages.Clear();
            currentSpeakerName = speakerName;
            currentSkippable = skippable;
            modeTimer = 0f;

            if (dialoguePages != null)
            {
                foreach (DialoguePage page in dialoguePages) pages.Enqueue(page);
            }

            if (pages.Count == 0)
            {
                FinishDialogue();
                return;
            }

            // 非アクティブ時はコルーチンもクリックも使えないため、全ページをまとめて即時表示する
            if (!isActiveAndEnabled)
            {
                var sb = new StringBuilder();
                while (pages.Count > 0)
                {
                    string body = ApplyPage(pages.Dequeue(), playSound: false);
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(body);
                }
                if (dialogueText != null)
                {
                    dialogueText.text = sb.ToString();
                    dialogueText.maxVisibleCharacters = int.MaxValue;
                }
                FinishDialogue();
                return;
            }

            IsDialoguePlaying = true;
            ShowNextPage();
        }

        /// <summary>
        /// 1文字を表示する間隔（秒）を変える（オプションの「文字の速さ」）
        /// </summary>
        public void SetTextSpeed(float secondsPerCharacter)
        {
            textSpeed = Mathf.Max(0f, secondsPerCharacter);
        }

        /// <summary> オートで次のページへ進むまでの待ち時間（オプションの「オートの待ち時間」） </summary>
        public void SetAutoWait(float seconds)
        {
            autoWaitSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary> オート（ページを表示し終えたら、少し待って自動で次へ）のオン・オフ </summary>
        public void SetAuto(bool on)
        {
            IsAuto = on;
            modeTimer = 0f;
        }

        /// <summary> スキップ（会話をどんどん送る）のオン・オフ </summary>
        public void SetSkip(bool on)
        {
            IsSkipping = on;
            modeTimer = 0f;
        }

        private void Update()
        {
            if (!IsDialoguePlaying) return;

            if (IsSkipping)
            {
                if (!currentSkippable)
                {
                    // まだ読んでいない会話に来たら、スキップをやめて読ませる
                    IsSkipping = false;
                    OnSkipStopped?.Invoke();
                    return;
                }

                modeTimer += Time.deltaTime;
                if (modeTimer >= skipInterval)
                {
                    modeTimer = 0f;
                    OnClickDialogueArea();
                }
                return;
            }

            // オート: 文字を表示し終えたら、待ち時間のあとに次のページへ
            if (IsAuto && !isTyping)
            {
                modeTimer += Time.deltaTime;
                if (modeTimer >= autoWaitSeconds)
                {
                    modeTimer = 0f;
                    ShowNextPage();
                }
            }
        }

        /// <summary>
        /// 会話状態のリセット
        /// </summary>
        public void ResetDialogueState()
        {
            StopTyping();
            pages.Clear();
            IsDialoguePlaying = false;

            if (speakerNameText != null) speakerNameText.text = "";
            if (dialogueText != null) dialogueText.text = "";
        }

        /// <summary>
        /// クリック時のスキップ / 次ページへ送る（TextSkipButton 用）
        /// </summary>
        public void OnClickDialogueArea()
        {
            if (!IsDialoguePlaying) return;

            if (isTyping)
            {
                // タイピング中の場合は現在ページを一括表示して演出スキップ
                StopTyping();
                if (dialogueText != null) dialogueText.maxVisibleCharacters = int.MaxValue;
                OnPageCompleted();
            }
            else
            {
                ShowNextPage();
            }
        }

        private void OnDisable()
        {
            // 非アクティブになるとコルーチンは止まるため、表示途中のページは全文表示にして整合を取る
            // （残りのページは再表示後のクリックで送る）
            if (isTyping)
            {
                typingCoroutine = null;
                if (dialogueText != null) dialogueText.maxVisibleCharacters = int.MaxValue;
                OnPageCompleted();
            }
        }

        private void ShowNextPage()
        {
            if (pages.Count == 0)
            {
                FinishDialogue();
                return;
            }

            string body = ApplyPage(pages.Dequeue());
            StopTyping();

            // 非アクティブ時はコルーチンを開始できないため即時表示する
            if (!isActiveAndEnabled)
            {
                if (dialogueText != null)
                {
                    dialogueText.text = body;
                    dialogueText.maxVisibleCharacters = int.MaxValue;
                }
                OnPageCompleted();
                return;
            }

            typingCoroutine = StartCoroutine(TypeTextCoroutine(body));
        }

        /// <summary>
        /// ページの演出指定（話者名・立ち絵）を画面に反映し、本文を返す
        /// </summary>
        private string ApplyPage(DialoguePage page, bool playSound = true)
        {
            if (speakerNameText != null)
            {
                speakerNameText.text = page.IsSystem ? "" : (string.IsNullOrEmpty(page.Speaker) ? currentSpeakerName : page.Speaker);
            }

            if (characterIllustration != null && page.ImageKey != null)
            {
                if (page.HidesImage)
                {
                    characterIllustration.gameObject.SetActive(false);
                }
                else if (page.ShowsImage)
                {
                    characterIllustration.gameObject.SetActive(true);
                }
                else
                {
                    // 名前で指定された立ち絵に差し替える（素材がまだない場合は今の立ち絵のまま）
                    Sprite sprite = findCharacterSprite?.Invoke(page.ImageKey);
                    if (sprite != null) characterIllustration.sprite = sprite;
                    characterIllustration.gameObject.SetActive(true);
                }
            }

            if (playSound && !string.IsNullOrEmpty(page.Se)) playSe?.Invoke(page.Se);

            return page.Body;
        }

        /// <summary>
        /// タイピング演出コルーチン（maxVisibleCharacters でリッチテキストタグを崩さずに1文字ずつ表示）
        /// </summary>
        private IEnumerator TypeTextCoroutine(string pageText)
        {
            isTyping = true;

            if (dialogueText != null)
            {
                dialogueText.text = pageText;
                dialogueText.maxVisibleCharacters = 0;
                dialogueText.ForceMeshUpdate();

                int totalCharacters = dialogueText.textInfo.characterCount;
                var wait = new WaitForSeconds(textSpeed);

                for (int i = 1; i <= totalCharacters; i++)
                {
                    dialogueText.maxVisibleCharacters = i;
                    yield return wait;
                }
            }

            typingCoroutine = null;
            OnPageCompleted();
        }

        private void OnPageCompleted()
        {
            isTyping = false;
            modeTimer = 0f;   // オートの待ち時間は、ページを表示し終えてから数える

            // 最終ページを表示し終えたら会話終了
            if (pages.Count == 0)
            {
                FinishDialogue();
            }
        }

        private void FinishDialogue()
        {
            isTyping = false;
            IsDialoguePlaying = false;
            OnDialogueFinished?.Invoke();
        }

        private void StopTyping()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            isTyping = false;
        }
    }
}
