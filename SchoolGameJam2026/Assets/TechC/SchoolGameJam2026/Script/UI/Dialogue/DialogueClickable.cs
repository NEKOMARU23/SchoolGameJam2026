using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TechC.SchoolGameJam2026.Scripts.UI.Dialogue
{
    /// <summary>
    /// クリック回数と外部から受け取った日数に応じて、画像とセリフを表示します。
    /// 日数の進行・保存は行いません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueClickable : MonoBehaviour, IPointerClickHandler
    {
        private const int MinimumDay = 1;
        private const int MinimumClickCount = 1;

        [SerializeField, Min(MinimumDay)] private int currentDay = MinimumDay;
        [SerializeField] private Sprite defaultImage;
        [SerializeField] private List<DayDialogue> days = new() { new DayDialogue() };

        [SerializeField] private GameObject imageWindow;
        [SerializeField] private GameObject textWindow;
        [SerializeField] private Image dialogueImage;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private TMP_Text dayAndClickText;
        [SerializeField] private Button closeButton;
        [SerializeField] private bool hideWindowsOnAwake = true;
        [SerializeField, Min(0f)] private float characterDisplayInterval = 0.04f;

        [SerializeField] private Camera eventCamera;

        private readonly Dictionary<int, int> clickCountsByDay = new();
        private readonly List<string> activePages = new();
        private int activePageIndex = -1;
        private int activeDisplayClickCount;
        private int lastInteractionFrame = -1;
        private Coroutine typingCoroutine;
        private bool isTyping;

        public int CurrentDay => currentDay;
        public bool IsOpen { get; private set; }

        /// <summary>会話ウィンドウが閉じられたとき（最後のページを読み終えた・閉じるボタン）に通知します。</summary>
        public event Action OnClosed;

        private void Reset()
        {
            // SpriteRendererを使うワールドオブジェクトだけ、クリック判定用Colliderを補います。
            // UIのButton/ImageにはEventSystemからクリックが届くためColliderは不要です。
            if (TryGetComponent<SpriteRenderer>(out _) &&
                !TryGetComponent<Collider2D>(out _))
            {
                gameObject.AddComponent<BoxCollider2D>();
            }
        }

        private void Awake()
        {
            ResolveCloseButton();
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseDialogue);
            }

            if (hideWindowsOnAwake)
            {
                CloseDialogue();
            }
        }

        private void OnValidate()
        {
            currentDay = Mathf.Max(currentDay, MinimumDay);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (IsOpen)
                {
                    AdvanceDialoguePage();
                }
                else
                {
                    OpenDialogue();
                }
            }
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector2 pointerPosition = Mouse.current.position.ReadValue();

            // 会話中は、閉じるボタン以外の画面クリックですべてページを進めます。
            if (IsOpen)
            {
                if (!IsPointerOverCloseButton(pointerPosition))
                {
                    AdvanceDialoguePage();
                }

                return;
            }

            // UIのクリックが背後のワールドオブジェクトへ貫通するのを防ぎます。
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera activeCamera = eventCamera != null ? eventCamera : Camera.main;
            if (activeCamera == null)
            {
                return;
            }

            Ray ray = activeCamera.ScreenPointToRay(pointerPosition);

            if (Physics.Raycast(ray, out RaycastHit hit3D) && IsOwnTransform(hit3D.transform))
            {
                OpenDialogue();
                return;
            }

            RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray);
            if (hit2D.collider != null && IsOwnTransform(hit2D.transform))
            {
                OpenDialogue();
            }
        }

        /// <summary>外部の日数管理から、日付が変わったときに呼び出します。</summary>
        public void SetCurrentDay(int day)
        {
            currentDay = Mathf.Max(day, MinimumDay);
        }

        /// <summary>日数を指定して会話を開く場合に使用します。</summary>
        public void OpenDialogueForDay(int day)
        {
            SetCurrentDay(day);
            OpenDialogue();
        }

        public void OpenDialogue()
        {
            // UIイベントとワールド判定が同じフレームに届いた場合の二重実行を防ぎます。
            if (lastInteractionFrame == Time.frameCount)
            {
                return;
            }

            lastInteractionFrame = Time.frameCount;

            DayDialogue dayDialogue = GetDayDialogue(currentDay);
            if (dayDialogue == null || dayDialogue.ClickCount == 0)
            {
                Debug.LogWarning($"{name}: {currentDay}日目のセリフが設定されていません。", this);
                return;
            }

            int rawClickCount = clickCountsByDay.TryGetValue(currentDay, out int previousCount)
                ? previousCount + 1
                : MinimumClickCount;
            clickCountsByDay[currentDay] = rawClickCount;

            // 設定数を超えたクリックでは、その日の最後の内容を繰り返します。
            int displayClickCount = Mathf.Min(rawClickCount, dayDialogue.ClickCount);
            ShowContent(dayDialogue, displayClickCount);
        }

        /// <summary>
        /// 日数と「その日の何回目の会話か」を外部から指定して会話を開きます。
        /// 回数の管理は呼び出し側で行い、このクラスのクリック回数は使いません。
        /// </summary>
        public void OpenDialogueForTalk(int day, int talkNumber)
        {
            lastInteractionFrame = Time.frameCount;
            SetCurrentDay(day);

            DayDialogue dayDialogue = GetDayDialogue(currentDay);
            if (dayDialogue == null || dayDialogue.ClickCount == 0)
            {
                Debug.LogWarning($"{name}: {currentDay}日目のセリフが設定されていません。", this);
                return;
            }

            ShowContent(dayDialogue, Mathf.Clamp(talkNumber, MinimumClickCount, dayDialogue.ClickCount));
        }

        /// <summary>指定した日に用意されている会話の数（その日に聞ける回数）を返します。</summary>
        public int GetTalkCountForDay(int day)
        {
            DayDialogue dayDialogue = GetDayDialogue(Mathf.Max(day, MinimumDay));
            return dayDialogue?.ClickCount ?? 0;
        }

        private void ShowContent(DayDialogue dayDialogue, int displayClickCount)
        {
            DialogueContent content = dayDialogue.GetContent(displayClickCount);

            activePages.Clear();
            activePages.AddRange(content?.CreatePages() ?? new List<string> { string.Empty });
            activePageIndex = 0;
            activeDisplayClickCount = displayClickCount;

            if (dialogueImage != null)
            {
                Sprite image = content?.ImageOverride != null
                    ? content.ImageOverride
                    : ResolveDefaultImage();

                dialogueImage.sprite = image;
                dialogueImage.enabled = image != null;
            }

            SetWindowActive(imageWindow, true);
            SetWindowActive(textWindow, true);
            IsOpen = true;
            ShowCurrentPage();
        }

        /// <summary>会話中に、閉じるボタン以外の画面をクリックしたときに呼び出します。</summary>
        public void AdvanceDialoguePage()
        {
            if (!IsOpen || lastInteractionFrame == Time.frameCount)
            {
                return;
            }

            lastInteractionFrame = Time.frameCount;

            // 文字送りの途中でクリックされた場合は、次ページへ進まず現在の全文を表示します。
            if (isTyping)
            {
                CompleteCurrentPageTyping();
                return;
            }

            if (activePageIndex + 1 >= activePages.Count)
            {
                CloseDialogue();
                return;
            }

            activePageIndex++;
            ShowCurrentPage();
        }

        /// <summary>閉じるボタンのOnClickから呼び出します。</summary>
        public void CloseDialogue()
        {
            bool wasOpen = IsOpen;

            StopTyping();
            SetWindowActive(imageWindow, false);
            SetWindowActive(textWindow, false);
            IsOpen = false;
            activePages.Clear();
            activePageIndex = -1;
            activeDisplayClickCount = 0;

            if (wasOpen)
            {
                OnClosed?.Invoke();
            }
        }

        public void ResetCurrentDayClickCount()
        {
            clickCountsByDay.Remove(currentDay);
        }

        public void ResetAllClickCounts()
        {
            clickCountsByDay.Clear();
        }

        private DayDialogue GetDayDialogue(int day)
        {
            if (days == null || day < MinimumDay || day > days.Count)
            {
                return null;
            }

            return days[day - 1];
        }

        private Sprite ResolveDefaultImage()
        {
            if (defaultImage != null)
            {
                return defaultImage;
            }

            return TryGetComponent(out SpriteRenderer spriteRenderer)
                ? spriteRenderer.sprite
                : null;
        }

        private void ShowCurrentPage()
        {
            if (activePageIndex < 0 || activePageIndex >= activePages.Count)
            {
                return;
            }

            if (dialogueText != null)
            {
                StartTyping(activePages[activePageIndex]);
            }

            if (dayAndClickText != null)
            {
                dayAndClickText.text =
                    $"{currentDay}日目・{activeDisplayClickCount}回目・{activePageIndex + 1}/{activePages.Count}";
            }
        }

        private void StartTyping(string fullText)
        {
            StopTyping();

            if (dialogueText == null)
            {
                return;
            }

            dialogueText.text = fullText ?? string.Empty;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            if (characterDisplayInterval <= 0f || dialogueText.textInfo.characterCount == 0)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
                return;
            }

            isTyping = true;
            typingCoroutine = StartCoroutine(TypeCurrentPage());
        }

        private IEnumerator TypeCurrentPage()
        {
            dialogueText.ForceMeshUpdate();
            int characterCount = dialogueText.textInfo.characterCount;

            for (int visibleCount = 1; visibleCount <= characterCount; visibleCount++)
            {
                dialogueText.maxVisibleCharacters = visibleCount;
                yield return new WaitForSecondsRealtime(characterDisplayInterval);
            }

            dialogueText.maxVisibleCharacters = int.MaxValue;
            typingCoroutine = null;
            isTyping = false;
        }

        private void CompleteCurrentPageTyping()
        {
            StopTyping();
            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
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

        private void ResolveCloseButton()
        {
            if (closeButton != null || textWindow == null)
            {
                return;
            }

            Button[] buttons = textWindow.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.name == "CloseButton")
                {
                    closeButton = button;
                    return;
                }
            }

            if (buttons.Length == 1)
            {
                closeButton = buttons[0];
            }
        }

        private bool IsPointerOverCloseButton(Vector2 screenPosition)
        {
            ResolveCloseButton();
            if (closeButton == null || !closeButton.gameObject.activeInHierarchy ||
                closeButton.transform is not RectTransform closeButtonRect)
            {
                return false;
            }

            Canvas canvas = closeButton.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.RectangleContainsScreenPoint(
                closeButtonRect,
                screenPosition,
                uiCamera);
        }

        private bool IsOwnTransform(Transform hitTransform)
        {
            return hitTransform == transform || hitTransform.IsChildOf(transform);
        }

        private static void SetWindowActive(GameObject window, bool active)
        {
            if (window != null)
            {
                window.SetActive(active);
            }
        }

        [Serializable]
        private sealed class DayDialogue
        {
            [SerializeField] private List<DialogueContent> contents = new() { new DialogueContent() };

            public int ClickCount => contents?.Count ?? 0;

            public DialogueContent GetContent(int clickCount)
            {
                if (contents == null || contents.Count == 0)
                {
                    return null;
                }

                int index = Mathf.Clamp(clickCount, MinimumClickCount, contents.Count) - 1;
                return contents[index];
            }
        }

        [Serializable]
        private sealed class DialogueContent
        {
            [SerializeField, TextArea(2, 6)] private string text;
            [SerializeField] private Sprite imageOverride;

            public Sprite ImageOverride => imageOverride;

            public List<string> CreatePages()
            {
                List<string> pages = new();
                string normalizedText = (text ?? string.Empty)
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');

                foreach (string line in normalizedText.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        pages.Add(line.Trim());
                    }
                }

                if (pages.Count == 0)
                {
                    pages.Add(string.Empty);
                }

                return pages;
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(DialogueClickable))]
    public sealed class DialogueClickableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("m_Script"),
                    new GUIContent("スクリプト"));
            }

            EditorGUILayout.LabelField("日数と会話内容", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("currentDay"),
                new GUIContent("現在の日数", "動作確認用です。実際の日数は外部からSetCurrentDayで渡します。"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("defaultImage"),
                new GUIContent("共通画像", "未設定なら、このオブジェクトのSprite Renderer画像を使います。"));

            DrawDays(serializedObject.FindProperty("days"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("画像・テキストの表示先", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("imageWindow"),
                new GUIContent("画像ウィンドウ"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("textWindow"),
                new GUIContent("テキストウィンドウ"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("dialogueImage"),
                new GUIContent("画像表示欄"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("dialogueText"),
                new GUIContent("セリフ表示欄"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("dayAndClickText"),
                new GUIContent("日数・回数表示欄（任意）"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("closeButton"),
                new GUIContent("閉じるボタン", "未設定の場合は、テキストウィンドウ内のCloseButtonを自動で探します。"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("hideWindowsOnAwake"),
                new GUIContent("開始時にウィンドウを隠す"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("文字送り", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("characterDisplayInterval"),
                new GUIContent(
                    "1文字の表示間隔（秒）",
                    "0にすると文字送りを使わず、ページ全文をすぐ表示します。"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("クリック判定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("eventCamera"),
                new GUIContent("判定に使うカメラ", "未設定ならMain Cameraを使います。"));

            EditorGUILayout.HelpBox(
                "外部の日数管理は、日付変更時に SetCurrentDay(日数) を呼び出してください。",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawDays(SerializedProperty days)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("日数・回数別のセリフ", EditorStyles.boldLabel);

            if (days.arraySize == 0)
            {
                EditorGUILayout.HelpBox("日数を追加してください。", MessageType.Warning);
            }

            for (int dayIndex = 0; dayIndex < days.arraySize; dayIndex++)
            {
                SerializedProperty day = days.GetArrayElementAtIndex(dayIndex);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                day.isExpanded = EditorGUILayout.Foldout(
                    day.isExpanded,
                    $"{dayIndex + 1}日目",
                    true,
                    EditorStyles.foldoutHeader);

                if (GUILayout.Button("この日を削除", GUILayout.Width(90f)))
                {
                    days.DeleteArrayElementAtIndex(dayIndex);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (day.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    DrawContents(day.FindPropertyRelative("contents"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("＋ 日数を追加"))
            {
                int newDayIndex = days.arraySize;
                days.arraySize++;

                SerializedProperty newDay = days.GetArrayElementAtIndex(newDayIndex);
                SerializedProperty contents = newDay.FindPropertyRelative("contents");
                contents.arraySize = 0;
                AddEmptyContent(contents);
                newDay.isExpanded = true;
            }
        }

        private static void DrawContents(SerializedProperty contents)
        {
            EditorGUILayout.LabelField("クリック回数別の内容", EditorStyles.boldLabel);

            if (contents.arraySize == 0)
            {
                EditorGUILayout.HelpBox("回数を追加してください。", MessageType.Warning);
            }

            for (int clickIndex = 0; clickIndex < contents.arraySize; clickIndex++)
            {
                SerializedProperty content = contents.GetArrayElementAtIndex(clickIndex);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                content.isExpanded = EditorGUILayout.Foldout(
                    content.isExpanded,
                    $"{clickIndex + 1}回目",
                    true);

                if (GUILayout.Button("削除", GUILayout.Width(48f)))
                {
                    contents.DeleteArrayElementAtIndex(clickIndex);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (content.isExpanded)
                {
                    EditorGUILayout.PropertyField(
                        content.FindPropertyRelative("text"),
                        new GUIContent(
                            "セリフ（改行ごとに順番表示）",
                            "1行目を最初に表示し、画面クリックごとに次の行を表示します。"));
                    EditorGUILayout.PropertyField(
                        content.FindPropertyRelative("imageOverride"),
                        new GUIContent("画像差し替え（任意）"));
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("＋ 回数を追加"))
            {
                AddEmptyContent(contents);
            }
        }

        private static void AddEmptyContent(SerializedProperty contents)
        {
            int newIndex = contents.arraySize;
            contents.arraySize++;

            SerializedProperty content = contents.GetArrayElementAtIndex(newIndex);
            content.FindPropertyRelative("text").stringValue = string.Empty;
            content.FindPropertyRelative("imageOverride").objectReferenceValue = null;
            content.isExpanded = true;
        }
    }
#endif
}
