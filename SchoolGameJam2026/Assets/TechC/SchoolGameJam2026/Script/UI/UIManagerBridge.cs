using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.UI;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;

namespace TechC.SchoolGameJam2026.Scripts.Core
{
    /// <summary>
    /// 解禁ボタンと、解禁時に表示するテキストのセット構造体
    /// </summary>
    [Serializable]
    public struct ProfileUIBinding
    {
        [Tooltip("識別用キー。基礎情報なら 'BASIC'、過去情報なら ProfileItem の itemKey ('PAST_1' 等) と一致させる")]
        public string key;
        [Tooltip("解禁用のボタンオブジェクト（解禁後に非表示）")]
        public GameObject unlockButton;
        [Tooltip("解禁後に表示するテキスト等のオブジェクト（解禁後に表示）")]
        public GameObject contentObject;
    }

    /// <summary>
    /// ログに記憶するセリフ1件分のデータ構造
    /// </summary>
    [Serializable]
    public struct DialogueLogData
    {
        public string speakerName;
        public string contentText;

        public DialogueLogData(string speakerName, string contentText)
        {
            this.speakerName = speakerName;
            this.contentText = contentText;
        }
    }

    /// <summary>
    /// UIボタンやテキストと、ゲームのロジック（Manager類）を繋ぐクラス
    /// </summary>
    public class UIManagerBridge : MonoBehaviour
    {
        [Header("--- 表示系UI（TextMeshPro） ---")]
        [SerializeField] private TextMeshProUGUI dayText;               // 日数表示 (例: "1日目")
        [SerializeField] private TextMeshProUGUI actionCountText;      // 残り行動回数表示 (例: "残り: 3/4")
        [SerializeField] private TextMeshProUGUI pointText;            // 所持ポイント表示 (例: "120 pt")
        [SerializeField] private TextMeshProUGUI dialogueText;         // キャラクターのセリフ表示枠

        [Header("--- 取調室内の表示パーツ (任意) ---")]
        [SerializeField] private TextMeshProUGUI targetCharacterNameText; // 取調室内の名前テキスト
        [SerializeField] private Image targetCharacterImage;    // 取調室内の立ち絵/アイコン
        
        [Header("--- メッセージウィンドウ ---")]
        [Tooltip("取り調べコマンド選択時に表示するメッセージボックス親オブジェクト")]
        [SerializeField] private GameObject interrogationChatbox;
        [Tooltip("セリフ送りの入力待ち時に表示するアイコン (任意)")]
        [SerializeField] private GameObject nextPageIcon;

        [Header("--- 文字送り（タイピング演出）設定 ---")]
        [Tooltip("1文字あたりの表示にかかる時間(秒)。小さくするほど速くなります")]
        [SerializeField] private float textSpeed = 0.03f;

        [Header("--- プロフィール（資料）UI登録リスト ---")]
        [Tooltip("解禁ボタンと表示用テキストのペアを複数登録するリスト")]
        [SerializeField] private List<ProfileUIBinding> profileUIBindings = new List<ProfileUIBinding>();

        [Header("--- 取調室のアクションボタン群 ---")]
        [Tooltip("拷問、対話、交渉、聞き取り の4つのボタンを設定（1日1回使用後の無効化制御用）")]
        [SerializeField] private Button[] interrogationButtons;

        [Header("--- 画面パネル切り替え ---")]
        [SerializeField] private GameObject monitorPanel;              // モニター選択画面
        [SerializeField] private GameObject interrogationPanel;        // 取調室画面
        [SerializeField] private GameObject profilePanel;              // 資料（DB）画面
        [SerializeField] private GameObject logPanel;                  // ★ ログ画面
        [SerializeField] private GameObject resetConfirmDialog;        // リセット確認ポップアップ
        [SerializeField] private GameObject dayEndConfirmDialog;       // 一日終了確認ポップアップ

        [Header("--- ログUI設定 ---")]
        [Tooltip("ScrollView 内の Content オブジェクト（LogItemPrefab を生成して配置する親）")]
        [SerializeField] private Transform logContentParent;
        [Tooltip("ログ1行分の表示用プレハブ")]
        [SerializeField] private GameObject logItemPrefab;

        [Header("--- 現在選択中のデータ状態 ---")]
        [SerializeField] private string currentSelectedCharacterId = "A";
        [SerializeField] private string currentHearingTargetId = "B";  // 聞き取り時に指定する他キャラID

        // --------------------------------------------------
        // ★ ページ送り・文字送り・パネル履歴・ログ管理用の変数
        // --------------------------------------------------
        private Queue<string> dialoguePages = new Queue<string>(); // 分割したセリフを保持するキュー
        private bool isDialoguePlaying = false;                   // 現在一連のセリフ再生中かどうか
        private bool isTyping = false;                            // 現在1文字ずつ表示処理中（タイピング中）かどうか
        private string currentFullPageText = "";                  // 現在のページで表示すべき全文
        private Coroutine typingCoroutine;                        // 文字送りのコルーチン参照
        private GameObject previousPanel;                         // 直前に開いていたパネルを記憶する参照
        
        // ★ 会話ログ履歴データリスト
        private List<DialogueLogData> dialogueLogList = new List<DialogueLogData>();

        private void OnEnable()
        {
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.OnPointsChanged += UpdatePointUI;
                InterrogationManager.Instance.OnActionCountChanged += UpdateActionCountUI;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayStarted += UpdateDayUI;
                GameManager.Instance.OnDayReset += HandleDayResetUI;
            }
        }

        private void OnDisable()
        {
            if (InterrogationManager.Instance != null)
            {
                InterrogationManager.Instance.OnPointsChanged -= UpdatePointUI;
                InterrogationManager.Instance.OnActionCountChanged -= UpdateActionCountUI;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayStarted -= UpdateDayUI;
                GameManager.Instance.OnDayReset -= HandleDayResetUI;
            }
        }

        private void Start()
        {
            if (InterrogationManager.Instance != null)
            {
                UpdatePointUI(InterrogationManager.Instance.CurrentPoints);
                UpdateActionCountUI(InterrogationManager.Instance.CurrentDailyActions, InterrogationManager.Instance.DailyActionLimit);
            }
            if (GameManager.Instance != null)
            {
                UpdateDayUI(GameManager.Instance.CurrentDay);
            }

            SetChatboxActive(false);
            SetNextPageIconActive(false);
            
            RefreshProfileUIState();
        }

        private void Update()
        {
            // ★ 新Input Systemに対応した入力判定
            if (isDialoguePlaying)
            {
                bool isClicked = false;

                if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
                {
                    isClicked = true;
                }

                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                {
                    isClicked = true;
                }

                if (isClicked)
                {
                    OnPlayerClickDialogue();
                }
            }
        }

        #region ★ Button の OnClick() に登録するメソッド群

        /// <summary>
        /// 取調対象のキャラクターを選択する
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            currentSelectedCharacterId = characterId;
            Debug.Log($"対象キャラクターを【{characterId}】にセットしました。");

            UpdateInterrogationRoomDisplay(characterId);
            UpdateInterrogationButtonsState();
            RefreshProfileUIState();
            SetChatboxActive(false);
        }

        /// <summary>
        /// モニター画面の部屋選択ボタン用
        /// </summary>
        public void OpenInterrogationForCharacter(string characterId)
        {
            SelectCharacter(characterId);
            OnClickOpenInterrogationPanel();
        }

        /// <summary>
        /// 「聞き取り」の対象キャラクターを選択する
        /// </summary>
        public void SelectHearingTarget(string targetId)
        {
            currentHearingTargetId = targetId;
            Debug.Log($"聞き取り対象を【{targetId}】にセットしました。");
        }

        /// <summary>
        /// 取調室の行動ボタン（【0:拷問】【1:対話】【2:交渉】【3:聞き取り】）を押した時
        /// </summary>
        public void OnClickInterrogationButton(int actionIndex)
        {
            if (isDialoguePlaying) return;

            if (InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId))
            {
                StartDialogueSequence("システム", "（このキャラクターへの本日の取り調べは既に終了しています）");
                UpdateInterrogationButtonsState();
                return;
            }

            InterrogationActionType actionType = (InterrogationActionType)actionIndex;

            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
            if (targetChar == null)
            {
                Debug.LogError($"ID: {currentSelectedCharacterId} のキャラクターが見つかりません。");
                return;
            }

            string rawDialogue = InterrogationManager.Instance.ExecuteAction(targetChar, actionType, currentHearingTargetId);

            // キャラクター名を取得してログ・セリフ再生を開始
            string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;
            StartDialogueSequence(speakerName, rawDialogue);

            UpdateInterrogationButtonsState();
        }

        /// <summary>
        /// UI全体（チャットボックス等）をクリックした時に進行させるUIイベント用
        /// </summary>
        public void OnClickAdvanceDialogue()
        {
            if (isDialoguePlaying)
            {
                OnPlayerClickDialogue();
            }
        }

        /// <summary>
        /// 資料画面の「基礎情報 解禁ボタン」を押した時
        /// </summary>
        public void OnClickUnlockBasicProfile()
        {
            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            BasicProfileGroup basicGroup = targetChar.ProfileData.basicInfo;

            if (basicGroup.isUnlocked)
            {
                Debug.Log("すでに基礎情報は解禁されています！");
                RefreshProfileUIState();
                return;
            }

            if (InterrogationManager.Instance.TrySpendPoints(basicGroup.unlockCost))
            {
                basicGroup.isUnlocked = true;
                Debug.Log($"<color=green>【解禁成功】{targetChar.CharacterId} の基礎情報を解禁しました！ (消費: {basicGroup.unlockCost}pt)</color>");
                RefreshProfileUIState();
            }
            else
            {
                Debug.Log("<color=red>ポイントが足りません！</color>");
            }
        }

        /// <summary>
        /// 資料画面の「過去情報 解禁ボタン」を押した時（直接オブジェクト指定）
        /// </summary>
        public void OnClickUnlockProfileItem(ProfileItem item)
        {
            if (item == null || item.isUnlocked) return;

            if (InterrogationManager.Instance.TrySpendPoints(item.unlockCost))
            {
                item.isUnlocked = true;
                Debug.Log($"[{item.itemKey}] を解禁しました！");
                RefreshProfileUIState();
            }
            else
            {
                Debug.Log("ポイントが足りません！");
            }
        }

        /// <summary>
        /// 資料画面の「過去情報 解禁ボタン」を押した時（キー指定）
        /// </summary>
        public void OnClickUnlockProfileItemByKey(string itemKey)
        {
            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            ProfileItem item = targetChar.ProfileData.historyInfoList.Find(i => i.itemKey == itemKey);
            if (item == null)
            {
                Debug.LogError($"キー【{itemKey}】に対応する情報が見つかりません。");
                return;
            }

            OnClickUnlockProfileItem(item);
        }

        // --------------------------------------------------
        // ★ 画面パネル切り替え機能
        // --------------------------------------------------

        public void ShowPanel(GameObject panelToShow)
        {
            if (monitorPanel != null && monitorPanel.activeSelf && monitorPanel != panelToShow)
            {
                previousPanel = monitorPanel;
            }
            else if (interrogationPanel != null && interrogationPanel.activeSelf && interrogationPanel != panelToShow)
            {
                previousPanel = interrogationPanel;
            }
            else if (profilePanel != null && profilePanel.activeSelf && profilePanel != panelToShow)
            {
                previousPanel = profilePanel;
            }
            else if (logPanel != null && logPanel.activeSelf && logPanel != panelToShow)
            {
                previousPanel = logPanel;
            }

            if (monitorPanel != null) monitorPanel.SetActive(monitorPanel == panelToShow);
            if (interrogationPanel != null) interrogationPanel.SetActive(interrogationPanel == panelToShow);
            if (profilePanel != null) profilePanel.SetActive(profilePanel == panelToShow);
            if (logPanel != null) logPanel.SetActive(logPanel == panelToShow);

            if (panelToShow == profilePanel)
            {
                RefreshProfileUIState();
            }
            else if (panelToShow == logPanel)
            {
                // ★ ログ画面を開いたタイミングでログ一覧表示を生成
                BuildLogListUI();
            }
        }

        public void OnClickOpenMonitorPanel() => ShowPanel(monitorPanel);

        public void OnClickOpenInterrogationPanel()
        {
            ShowPanel(interrogationPanel);
            UpdateInterrogationButtonsState();
        }

        public void OnClickOpenProfilePanel() => ShowPanel(profilePanel);

        public void OnClickOpenLogPanel() => ShowPanel(logPanel);

        public void OnClickReturnToPreviousPanel()
        {
            if (previousPanel != null)
            {
                ShowPanel(previousPanel);
            }
            else
            {
                ShowPanel(monitorPanel);
            }
        }

        // --------------------------------------------------
        // ★ ダイアログ関連
        // --------------------------------------------------

        public void OnClickOpenResetDialog()
        {
            if (resetConfirmDialog != null) resetConfirmDialog.SetActive(true);
        }

        public void OnClickConfirmResetDay()
        {
            GameManager.Instance.ResetCurrentDay();
            if (resetConfirmDialog != null) resetConfirmDialog.SetActive(false);
        }

        public void OnClickCancelResetDay()
        {
            if (resetConfirmDialog != null) resetConfirmDialog.SetActive(false);
        }

        public void OnClickOpenEndDayDialog()
        {
            if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(true);
        }

        public void OnClickConfirmAdvanceNextDay()
        {
            if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(false);
            GameManager.Instance.AdvanceToNextDay();
        }

        public void OnClickCancelEndDay()
        {
            if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(false);
        }

        #endregion

        #region ★ プロフィールUIの表示制御

        private void RefreshProfileUIState()
        {
            if (GameManager.Instance == null || GameManager.Instance.AllCharacters == null) return;

            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            foreach (var binding in profileUIBindings)
            {
                if (string.IsNullOrEmpty(binding.key)) continue;

                bool isUnlocked = false;

                if (binding.key.Equals("BASIC", StringComparison.OrdinalIgnoreCase))
                {
                    isUnlocked = targetChar.ProfileData.basicInfo.isUnlocked;
                }
                else
                {
                    ProfileItem item = targetChar.ProfileData.historyInfoList.Find(i => i.itemKey == binding.key);
                    if (item != null)
                    {
                        isUnlocked = item.isUnlocked;
                    }
                }

                if (binding.unlockButton != null)
                {
                    binding.unlockButton.SetActive(!isUnlocked);
                }

                if (binding.contentObject != null)
                {
                    binding.contentObject.SetActive(isUnlocked);
                }
            }
        }

        #endregion

        #region ★ 文字送り・ページ送り・ログ保存のコアロジック

        private void OnPlayerClickDialogue()
        {
            if (isTyping)
            {
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                
                if (dialogueText != null) dialogueText.text = currentFullPageText;
                isTyping = false;

                SetNextPageIconActive(dialoguePages.Count > 0);
            }
            else
            {
                AdvanceToNextDialoguePage();
            }
        }

        /// <summary>
        /// セリフテキストを区切り文字 '|' で分割し、全体の再生を開始する
        /// </summary>
        private void StartDialogueSequence(string speakerName, string fullText)
        {
            dialoguePages.Clear();

            if (typingCoroutine != null) StopCoroutine(typingCoroutine);

            if (string.IsNullOrEmpty(fullText))
            {
                SetChatboxActive(false);
                return;
            }

            string[] pages = fullText.Split('|');
            foreach (var p in pages)
            {
                string trimmed = p.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    dialoguePages.Enqueue(trimmed);
                    
                    // ★ ログリストにセリフ履歴を追加保存
                    dialogueLogList.Add(new DialogueLogData(speakerName, trimmed));
                }
            }

            if (dialoguePages.Count == 0) return;

            SetChatboxActive(true);
            isDialoguePlaying = true;

            AdvanceToNextDialoguePage();
        }

        private void AdvanceToNextDialoguePage()
        {
            if (dialoguePages.Count > 0)
            {
                currentFullPageText = dialoguePages.Dequeue();
                SetNextPageIconActive(false);

                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                typingCoroutine = StartCoroutine(TypeTextCoroutine(currentFullPageText));
            }
            else
            {
                EndDialogueSequence();
            }
        }

        private IEnumerator TypeTextCoroutine(string textToType)
        {
            isTyping = true;
            if (dialogueText != null) dialogueText.text = "";

            bool isInsideTag = false;
            string currentDisplayedText = "";

            for (int i = 0; i < textToType.Length; i++)
            {
                char c = textToType[i];

                if (c == '<') isInsideTag = true;
                if (c == '>') isInsideTag = false;

                currentDisplayedText += c;

                if (dialogueText != null)
                {
                    dialogueText.text = currentDisplayedText;
                }

                if (!isInsideTag && textSpeed > 0f)
                {
                    yield return new WaitForSeconds(textSpeed);
                }
            }

            isTyping = false;

            SetNextPageIconActive(dialoguePages.Count > 0);
        }

        private void EndDialogueSequence()
        {
            isDialoguePlaying = false;
            isTyping = false;
            SetNextPageIconActive(false);
        }

        #endregion

        #region ★ ログUI表示処理

        /// <summary>
        /// ログ画面に蓄積された会話履歴を生成する
        /// </summary>
        private void BuildLogListUI()
        {
            if (logContentParent == null || logItemPrefab == null) return;

            // 既存の表示要素をクリア
            foreach (Transform child in logContentParent)
            {
                Destroy(child.gameObject);
            }

            // 保存されたログリストを順番に生成
            foreach (var log in dialogueLogList)
            {
                GameObject itemObj = Instantiate(logItemPrefab, logContentParent);
                LogItemUI itemUI = itemObj.GetComponent<LogItemUI>();
                if (itemUI != null)
                {
                    itemUI.SetLog(log.speakerName, log.contentText);
                }
            }
        }

        /// <summary>
        /// ログデータをクリア（必要に応じて呼び出し）
        /// </summary>
        public void ClearDialogueLog()
        {
            dialogueLogList.Clear();
        }

        #endregion

        #region UI更新用の内部処理

        private void SetChatboxActive(bool isActive)
        {
            if (interrogationChatbox != null)
            {
                interrogationChatbox.SetActive(isActive);
            }
        }

        private void SetNextPageIconActive(bool isActive)
        {
            if (nextPageIcon != null)
            {
                nextPageIcon.SetActive(isActive);
            }
        }

        private void UpdateInterrogationButtonsState()
        {
            if (interrogationButtons == null || interrogationButtons.Length == 0) return;

            bool isInterrogated = InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId);
            bool isLimitReached = InterrogationManager.Instance != null && InterrogationManager.Instance.IsActionLimitReached;

            bool canInteract = !isInterrogated && !isLimitReached && !isDialoguePlaying;

            foreach (var btn in interrogationButtons)
            {
                if (btn != null)
                {
                    btn.interactable = canInteract;
                }
            }
        }

        private void UpdateInterrogationRoomDisplay(string characterId)
        {
            if (GameManager.Instance == null || GameManager.Instance.AllCharacters == null) return;

            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == characterId);
            if (targetChar != null && targetChar.ProfileData != null)
            {
                if (targetCharacterNameText != null)
                    targetCharacterNameText.text = targetChar.ProfileData.basicInfo.characterName;

                if (targetCharacterImage != null)
                    targetCharacterImage.sprite = targetChar.ProfileData.characterIcon;
            }
        }

        private void UpdatePointUI(int points)
        {
            if (pointText != null) pointText.text = $"{points} pt";
        }

        private void UpdateActionCountUI(int currentActions, int maxLimit)
        {
            if (actionCountText != null)
            {
                int remaining = Mathf.Max(0, maxLimit - currentActions);
                actionCountText.text = $"残り行動: {remaining}/{maxLimit}";
            }

            UpdateInterrogationButtonsState();
        }

        private void UpdateDayUI(int day)
        {
            if (dayText != null) dayText.text = $"{day} 日目";
            SetChatboxActive(false);
            UpdateInterrogationButtonsState();
            RefreshProfileUIState();
        }

        private void HandleDayResetUI()
        {
            StartDialogueSequence("システム", "（本日の朝の状態に巻き戻しました）");
            UpdateInterrogationButtonsState();
            RefreshProfileUIState();
            Debug.Log("UIを朝の状態へリセットしました。");
        }

        #endregion
    }
}