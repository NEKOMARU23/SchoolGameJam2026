// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Text.RegularExpressions;
// using UnityEngine;
// using UnityEngine.UI;
// using UnityEngine.InputSystem;
// using UnityEngine.SceneManagement;
// using TMPro;
// using TechC.SchoolGameJam2026.Scripts.SO;
// using TechC.SchoolGameJam2026.Scripts.UI;
// using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
// using TechC.SchoolGameJam2026.Scripts.Core.Manager;

// namespace TechC.SchoolGameJam2026.Scripts.Core
// {
//     [Serializable]
//     public struct ProfileUIBinding
//     {
//         public string key;
//         public GameObject unlockButton;
//         public GameObject contentObject;
//     }

//     [Serializable]
//     public struct DialogueLogData
//     {
//         public string speakerName;
//         public string contentText;

//         public DialogueLogData(string speakerName, string contentText)
//         {
//             this.speakerName = speakerName;
//             this.contentText = contentText;
//         }
//     }

//     [Serializable]
//     public struct MonitorRoomBinding
//     {
//         public string characterId;
//         public Button roomButton;
//         public GameObject releasedOverlay;
//     }

//     [Serializable]
//     public struct CharacterSpriteBinding
//     {
//         public string spriteKey;
//         public Sprite sprite;
//     }

//     public class UIManagerBridge : MonoBehaviour
//     {
//         [Header("--- 表示系UI（TextMeshPro） ---")]
//         [SerializeField] private TextMeshProUGUI dayText;
//         [SerializeField] private TextMeshProUGUI actionCountText;
//         [SerializeField] private TextMeshProUGUI pointText;
//         [SerializeField] private TextMeshProUGUI dialogueText;

//         [Header("--- 取調室・プロフィール共通の表示パーツ ---")]
//         [SerializeField] private TextMeshProUGUI targetCharacterNameText;
//         [SerializeField] private Image targetCharacterImage;

//         [Header("--- プロフィール専用表示パーツ ---")]
//         [SerializeField] private TextMeshProUGUI profileNameText;
//         [SerializeField] private Image profileCharacterImage;

//         [Header("--- メッセージウィンドウ ---")]
//         [SerializeField] private GameObject interrogationChatbox;
//         [SerializeField] private GameObject nextPageIcon;

//         [Header("--- 文字送り（タイピング演出）設定 ---")]
//         [SerializeField] private float textSpeed = 0.03f;

//         [Header("--- プロフィール（資料）UI登録リスト ---")]
//         [SerializeField] private List<ProfileUIBinding> profileUIBindings = new List<ProfileUIBinding>();

//         [Header("--- キャラクター差分画像登録 ---")]
//         [SerializeField] private List<CharacterSpriteBinding> characterSpriteBindings = new List<CharacterSpriteBinding>();

//         [Header("--- 取調室のアクションボタン群 ---")]
//         [SerializeField] private Button[] interrogationButtons;

//         [Header("--- 特殊アクションボタン（取調室） ---")]
//         [SerializeField] private Button releaseButton;
//         [SerializeField] private GameObject confirmAiButtonObject;
//         [SerializeField] private int finalDayNumber = 7;

//         [Header("--- 聞き取り（他キャラ選択）パネルUI ---")]
//         [SerializeField] private GameObject hearingPanel;
//         [SerializeField] private Transform characterButtonContainer;
//         [SerializeField] private Button closeHearingPanelButton;

//         [Header("--- シーン遷移設定 ---")]
//         [Tooltip("クリア時に遷移するシーン名")]
//         [SerializeField] private string clearSceneName = "ClearScene";
//         [Tooltip("ゲームオーバー時に遷移するシーン名")]
//         [SerializeField] private string gameOverSceneName = "GameOverScene";

//         [Header("--- モニター画面の部屋ボタン登録 ---")]
//         [SerializeField] private List<MonitorRoomBinding> monitorRoomBindings = new List<MonitorRoomBinding>();

//         [Header("--- 画面パネル切り替え ---")]
//         [SerializeField] private GameObject monitorPanel;
//         [SerializeField] private GameObject interrogationPanel;
//         [SerializeField] private GameObject profilePanel;
//         [SerializeField] private GameObject logPanel;
//         [SerializeField] private GameObject endingPanel;
//         [SerializeField] private GameObject resultPanel;
//         [SerializeField] private GameObject resetConfirmDialog;
//         [SerializeField] private GameObject dayEndConfirmDialog;

//         [Header("--- リザルト画面UI ---")]
//         [SerializeField] private TextMeshProUGUI resultDetailText;
//         [SerializeField] private TextMeshProUGUI resultPointText;

//         [Header("--- ログUI設定 ---")]
//         [SerializeField] private Transform logContentParent;
//         [SerializeField] private GameObject logItemPrefab;

//         [Header("--- 現在選択中のデータ状態 ---")]
//         [SerializeField] private string currentSelectedCharacterId = "A";
//         [SerializeField] private string currentHearingTargetId = "B";

//         // 勝敗フラグ管理
//         private bool hasEscapedAI = false;        // AIを逃してしまったか
//         private bool isAiIdentified = false;     // AIを正しく特定できたか

//         private Queue<string> dialoguePages = new Queue<string>();
//         private bool isDialoguePlaying = false;
//         private bool isTyping = false;
//         private string currentFullPageText = "";
//         private string currentSpeakerName = "";
//         private Coroutine typingCoroutine;
//         private GameObject previousPanel;

//         private List<DialogueLogData> dialogueLogList = new List<DialogueLogData>();

//         private void OnEnable()
//         {
//             if (InterrogationManager.Instance != null)
//             {
//                 InterrogationManager.Instance.OnPointsChanged += UpdatePointUI;
//                 InterrogationManager.Instance.OnActionCountChanged += UpdateActionCountUI;
//             }

//             if (GameManager.Instance != null)
//             {
//                 GameManager.Instance.OnDayStarted += UpdateDayUI;
//                 GameManager.Instance.OnDayReset += HandleDayResetUI;
//             }
//         }

//         private void OnDisable()
//         {
//             if (InterrogationManager.Instance != null)
//             {
//                 InterrogationManager.Instance.OnPointsChanged -= UpdatePointUI;
//                 InterrogationManager.Instance.OnActionCountChanged -= UpdateActionCountUI;
//             }

//             if (GameManager.Instance != null)
//             {
//                 GameManager.Instance.OnDayStarted -= UpdateDayUI;
//                 GameManager.Instance.OnDayReset -= HandleDayResetUI;
//             }
//         }

//         private void Start()
//         {
//             if (InterrogationManager.Instance != null)
//             {
//                 UpdatePointUI(InterrogationManager.Instance.CurrentPoints);
//                 UpdateActionCountUI(InterrogationManager.Instance.CurrentDailyActions, InterrogationManager.Instance.DailyActionLimit);
//             }
//             if (GameManager.Instance != null)
//             {
//                 UpdateDayUI(GameManager.Instance.CurrentDay);
//             }

//             if (closeHearingPanelButton != null)
//             {
//                 closeHearingPanelButton.onClick.AddListener(CloseHearingPanel);
//             }

//             if (hearingPanel != null) hearingPanel.SetActive(false);

//             SetNextPageIconActive(false);

//             RefreshProfileUIState();
//             RefreshMonitorRoomButtons();

//             isDialoguePlaying = false;
//             isTyping = false;
//             UpdateInterrogationButtonsState();
//         }

//         private void Update()
//         {
//             if (isDialoguePlaying)
//             {
//                 bool isClicked = false;

//                 if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
//                 {
//                     isClicked = true;
//                 }

//                 if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
//                 {
//                     isClicked = true;
//                 }

//                 if (isClicked)
//                 {
//                     OnPlayerClickDialogue();
//                 }
//             }
//         }

//         #region リザルト機能・一日終了フロー

//         public void OnClickConfirmAdvanceNextDay()
//         {
//             if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(false);
//             ShowResultPanel();
//         }

//         private void ShowResultPanel()
//         {
//             if (resultPanel == null)
//             {
//                 if (GameManager.Instance != null) GameManager.Instance.AdvanceToNextDay();
//                 return;
//             }

//             ShowPanel(resultPanel);

//             System.Text.StringBuilder detailSb = new System.Text.StringBuilder();
//             detailSb.AppendLine("<b>【本日の取り調べ評価】</b>\n");

//             int earnedPoints = 0;

//             DayScenarioSO todayScenario = GameManager.Instance != null ? GameManager.Instance.GetTodayScenario() : null;

//             if (InterrogationManager.Instance != null && GameManager.Instance != null && todayScenario != null)
//             {
//                 var history = InterrogationManager.Instance.TodayActionHistory;
//                 earnedPoints = InterrogationManager.Instance.TodayEarnedPoints;

//                 foreach (var character in GameManager.Instance.AllCharacters)
//                 {
//                     string charName = character.ProfileData != null ? character.ProfileData.basicInfo.characterName : character.CharacterId;

//                     if (character.IsReleased)
//                     {
//                         detailSb.AppendLine($"• {charName} : <color=#888888>解放済み（取り調べ対象外）</color>");
//                         continue;
//                     }

//                     if (history.TryGetValue(character.CharacterId, out InterrogationActionType executedAction))
//                     {
//                         var setting = todayScenario.GetCharacterSetting(character.CharacterId);

//                         if (executedAction == InterrogationActionType.HearAbout)
//                         {
//                             detailSb.AppendLine($"• {charName} : 聞き取り実行 → <color=#00FFFF>情報収集完了</color>");
//                         }
//                         else if (setting != null)
//                         {
//                             bool isCorrect = (executedAction == setting.correctAction);
//                             string actionName = GetActionTypeName(executedAction);
//                             string judgeText = isCorrect ? "<color=#00FF00>有効（正解/好感度UP）</color>" : "<color=#FF4444>効果なし（不正解）</color>";

//                             detailSb.AppendLine($"• {charName} : {actionName} 実行 → {judgeText}");
//                         }
//                     }
//                     else
//                     {
//                         detailSb.AppendLine($"• {charName} : <color=#AAAAAA>取り調べ未実施</color>");
//                     }
//                 }
//             }
//             else
//             {
//                 detailSb.AppendLine("（データが正常に取得できませんでした）");
//             }

//             if (resultDetailText != null) resultDetailText.text = detailSb.ToString();

//             string pointSign = earnedPoints >= 0 ? "+" : "";
//             if (resultPointText != null) resultPointText.text = $"本日の獲得解析ポイント: <b>{pointSign}{earnedPoints} pt</b>";
//         }

//         /// <summary>
//         /// リザルト画面を閉じて次の日へ進む（または7日目終了でクリア/ゲームオーバーへ）
//         /// </summary>
//         public void OnClickCloseResultAndAdvanceDay()
//         {
//             int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

//             if (currentDay >= finalDayNumber)
//             {
//                 Debug.Log($"最終日（{finalDayNumber}日目）のリザルトを確認完了。勝敗判定を行います。");

//                 // 勝敗分岐処理
//                 if (isAiIdentified && !hasEscapedAI)
//                 {
//                     Debug.Log("<color=green>【GAME CLEAR】AIを見破り、無事に7日目を終了しました！</color>");
//                     if (!string.IsNullOrEmpty(clearSceneName)) SceneManager.LoadScene(clearSceneName);
//                 }
//                 else
//                 {
//                     Debug.LogWarning($"<color=red>【GAME OVER】条件を満たせませんでした。（AI逃亡: {hasEscapedAI}, AI特定成功: {isAiIdentified}）</color>");
//                     if (!string.IsNullOrEmpty(gameOverSceneName)) SceneManager.LoadScene(gameOverSceneName);
//                 }
//             }
//             else
//             {
//                 if (GameManager.Instance != null)
//                 {
//                     GameManager.Instance.AdvanceToNextDay();
//                 }
//                 ShowPanel(monitorPanel);
//             }
//         }

//         private string GetActionTypeName(InterrogationActionType type)
//         {
//             switch (type)
//             {
//                 case InterrogationActionType.Torture: return "拷問";
//                 case InterrogationActionType.Talk: return "対話";
//                 case InterrogationActionType.Negotiate: return "交渉";
//                 case InterrogationActionType.HearAbout: return "聞き取り";
//                 case InterrogationActionType.Release: return "解放";
//                 case InterrogationActionType.ConfirmAI: return "AI確定";
//                 default: return "不明";
//             }
//         }

//         #endregion

//         #region 特殊ボタン（解放 & AI確定）機能

//         public void OnClickReleaseCharacter()
//         {
//             if (string.IsNullOrEmpty(currentSelectedCharacterId)) return;

//             CharacterInstance targetChar = GameManager.Instance != null && GameManager.Instance.AllCharacters != null
//                 ? GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId)
//                 : null;

//             if (targetChar == null) return;

//             if (targetChar.IsReleased)
//             {
//                 StartDialogueSequence("システム", "（この対象はすでに解放されています）");
//                 return;
//             }

//             targetChar.SetReleaseFlag(true);
//             Debug.Log($"<color=cyan>【解放】キャラクター {currentSelectedCharacterId} を解放しました。</color>");

//             // ★ CharacterInstance が持つ AI 判定フラグをチェック
//             if (CheckIsAI(targetChar))
//             {
//                 hasEscapedAI = true;
//                 Debug.LogWarning($"<color=red>【警告】AIである {currentSelectedCharacterId} が解放されました。（7日目ラストでゲームオーバーになります）</color>");
//             }

//             string rawDialogue = "";
//             if (InterrogationManager.Instance != null)
//             {
//                 rawDialogue = InterrogationManager.Instance.ExecuteAction(targetChar, InterrogationActionType.Release, currentHearingTargetId);
//             }

//             if (string.IsNullOrEmpty(rawDialogue) || rawDialogue == "「……」")
//             {
//                 rawDialogue = "解放される……ということですか。わかりました。|[SYS][IMG:OFF]（対象は部屋から退出しました）";
//             }

//             string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;
//             StartDialogueSequence(speakerName, rawDialogue);

//             RefreshMonitorRoomButtons();
//             UpdateInterrogationButtonsState();
//         }

//         public void OnClickConfirmAI()
//         {
//             Debug.Log($"<color=red>【AI確定】{currentSelectedCharacterId} をAIとして選択しました。</color>");

//             CharacterInstance targetChar = GameManager.Instance != null && GameManager.Instance.AllCharacters != null
//                 ? GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId)
//                 : null;

//             if (targetChar == null) return;

//             // ★ CharacterInstance が持つ AI 判定フラグをチェック
//             if (CheckIsAI(targetChar))
//             {
//                 isAiIdentified = true;
//                 Debug.Log("<color=green>【判定成功】正しくAIを選択できました！</color>");
//             }
//             else
//             {
//                 isAiIdentified = false;
//                 Debug.LogWarning("<color=red>【判定失敗】人間を選択してしまいました。</color>");
//             }

//             if (InterrogationManager.Instance != null)
//             {
//                 string rawDialogue = InterrogationManager.Instance.ExecuteAction(targetChar, InterrogationActionType.ConfirmAI, currentHearingTargetId);
//                 string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;

//                 if (!string.IsNullOrEmpty(rawDialogue) && rawDialogue != "「……」")
//                 {
//                     StartDialogueSequence(speakerName, rawDialogue);
//                 }
//             }

//             ShowResultPanel();
//         }

//         /// <summary>
//         /// CharacterInstance の AI フラグ判定（プロパティ: IsAI / フィールド: isAI の両対応）
//         /// </summary>
//         private bool CheckIsAI(CharacterInstance targetChar)
//         {
//             if (targetChar == null) return false;

//             // プロパティ IsAI があるか、フィールド isAI があるかをリフレクションまたは直接参照で判定
//             var type = targetChar.GetType();
//             var prop = type.GetProperty("IsAI") ?? type.GetProperty("isAI");
//             if (prop != null)
//             {
//                 return (bool)prop.GetValue(targetChar);
//             }

//             var field = type.GetField("IsAI") ?? type.GetField("isAI");
//             if (field != null)
//             {
//                 return (bool)field.GetValue(targetChar);
//             }

//             return false;
//         }

//         public void RefreshMonitorRoomButtons()
//         {
//             if (GameManager.Instance == null || GameManager.Instance.AllCharacters == null || GameManager.Instance.AllCharacters.Count == 0)
//             {
//                 foreach (var binding in monitorRoomBindings)
//                 {
//                     if (binding.roomButton != null) binding.roomButton.interactable = true;
//                     if (binding.releasedOverlay != null) binding.releasedOverlay.SetActive(false);
//                 }
//                 return;
//             }

//             foreach (var binding in monitorRoomBindings)
//             {
//                 CharacterInstance charInst = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == binding.characterId);

//                 if (charInst != null)
//                 {
//                     bool isReleased = charInst.IsReleased;

//                     if (binding.roomButton != null)
//                     {
//                         binding.roomButton.interactable = !isReleased;
//                     }

//                     if (binding.releasedOverlay != null)
//                     {
//                         binding.releasedOverlay.SetActive(isReleased);
//                     }
//                 }
//             }
//         }

//         #endregion

//         #region 他キャラ選択パネル（配置済みボタンの動的制御）

//         public void OnClickOpenHearingPanel()
//         {
//             if (hearingPanel == null || characterButtonContainer == null) return;
//             if (GameManager.Instance == null || GameManager.Instance.AllCharacters == null) return;

//             Button[] existingButtons = characterButtonContainer.GetComponentsInChildren<Button>(true);

//             CharacterInstance currentSelectedChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
//             string currentSelectedName = currentSelectedChar != null && currentSelectedChar.ProfileData != null
//                 ? currentSelectedChar.ProfileData.basicInfo.characterName
//                 : currentSelectedCharacterId;

//             foreach (var btn in existingButtons)
//             {
//                 TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
//                 string btnTextName = btnText != null ? btnText.text : "";

//                 bool isSelf = (btnTextName == currentSelectedName || btn.gameObject.name.Contains(currentSelectedCharacterId));

//                 if (isSelf)
//                 {
//                     btn.gameObject.SetActive(false);
//                 }
//                 else
//                 {
//                     btn.gameObject.SetActive(true);

//                     CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c =>
//                         (c.ProfileData != null && c.ProfileData.basicInfo.characterName == btnTextName) ||
//                         btn.gameObject.name.EndsWith(c.CharacterId) ||
//                         btn.gameObject.name.Contains(c.CharacterId)
//                     );

//                     if (targetChar != null)
//                     {
//                         string targetId = targetChar.CharacterId;
//                         btn.onClick.RemoveAllListeners();
//                         btn.onClick.AddListener(() => OnSelectHearingCharacter(targetId));
//                     }
//                 }
//             }

//             hearingPanel.SetActive(true);
//         }

//         public void CloseHearingPanel()
//         {
//             if (hearingPanel != null) hearingPanel.SetActive(false);
//         }

//         private void OnSelectHearingCharacter(string targetCharacterId)
//         {
//             CloseHearingPanel();

//             SelectHearingTarget(targetCharacterId);
//             OnClickInterrogationButton((int)InterrogationActionType.HearAbout);
//         }

//         #endregion

//         #region Button の OnClick() に登録するメソッド群

//         public void SelectCharacter(string characterId)
//         {
//             currentSelectedCharacterId = characterId;
//             Debug.Log($"対象キャラクターを【{characterId}】にセットしました。");

//             isDialoguePlaying = false;
//             isTyping = false;

//             UpdateInterrogationRoomDisplay(characterId);
//             UpdateInterrogationButtonsState();
//             RefreshProfileUIState();
//         }

//         public void OpenInterrogationForCharacter(string characterId)
//         {
//             CharacterInstance targetChar = GameManager.Instance != null && GameManager.Instance.AllCharacters != null
//                 ? GameManager.Instance.AllCharacters.Find(c => c.CharacterId == characterId)
//                 : null;

//             if (targetChar != null && targetChar.IsReleased)
//             {
//                 Debug.Log($"キャラクター【{characterId}】はすでに解放されているため、取調室に入れません。");
//                 return;
//             }

//             SelectCharacter(characterId);
//             OnClickOpenInterrogationPanel();
//         }

//         public void SelectHearingTarget(string targetId)
//         {
//             currentHearingTargetId = targetId;
//             Debug.Log($"聞き取り対象を【{targetId}】にセットしました。");
//         }

//         public void OnClickInterrogationButton(int actionIndex)
//         {
//             if (isDialoguePlaying) return;

//             CharacterInstance targetChar = GameManager.Instance != null && GameManager.Instance.AllCharacters != null
//                 ? GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId)
//                 : null;

//             if (targetChar == null) return;

//             if (targetChar.IsReleased)
//             {
//                 StartDialogueSequence("システム", "（このキャラクターはすでに解放されているため、取り調べできません）");
//                 return;
//             }

//             if (InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId))
//             {
//                 StartDialogueSequence("システム", "（このキャラクターへの本日の取り調べは既に終了しています）");
//                 UpdateInterrogationButtonsState();
//                 return;
//             }

//             InterrogationActionType actionType = (InterrogationActionType)actionIndex;
//             string rawDialogue = InterrogationManager.Instance.ExecuteAction(targetChar, actionType, currentHearingTargetId);

//             string speakerName = targetChar.ProfileData != null ? targetChar.ProfileData.basicInfo.characterName : targetChar.CharacterId;
//             StartDialogueSequence(speakerName, rawDialogue);

//             UpdateInterrogationButtonsState();
//         }

//         public void OnClickAdvanceDialogue()
//         {
//             if (isDialoguePlaying)
//             {
//                 OnPlayerClickDialogue();
//             }
//         }

//         public void OnClickUnlockBasicProfile()
//         {
//             CharacterInstance targetChar = GameManager.Instance != null && GameManager.Instance.AllCharacters != null
//                 ? GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId)
//                 : null;

//             if (targetChar == null || targetChar.ProfileData == null) return;

//             BasicProfileGroup basicGroup = targetChar.ProfileData.basicInfo;

//             if (basicGroup.isUnlocked)
//             {
//                 RefreshProfileUIState();
//                 return;
//             }

//             if (InterrogationManager.Instance != null && InterrogationManager.Instance.TrySpendPoints(basicGroup.unlockCost))
//             {
//                 basicGroup.isUnlocked = true;
//                 RefreshProfileUIState();
//             }
//         }

//         public void OnClickUnlockProfileItem(ProfileItem item)
//         {
//             if (item == null || item.isUnlocked) return;

//             if (InterrogationManager.Instance != null && InterrogationManager.Instance.TrySpendPoints(item.unlockCost))
//             {
//                 item.isUnlocked = true;
//                 RefreshProfileUIState();
//             }
//         }

//         public void OnClickUnlockProfileItemByKey(string itemKey)
//         {
//             CharacterInstance targetChar = GameManager.Instance != null && GameManager.Instance.AllCharacters != null
//                 ? GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId)
//                 : null;

//             if (targetChar == null || targetChar.ProfileData == null) return;

//             ProfileItem item = targetChar.ProfileData.historyInfoList.Find(i => i.itemKey == itemKey);
//             if (item != null)
//             {
//                 OnClickUnlockProfileItem(item);
//             }
//         }

//         public void ShowPanel(GameObject panelToShow)
//         {
//             if (monitorPanel != null && monitorPanel.activeSelf && monitorPanel != panelToShow) previousPanel = monitorPanel;
//             else if (interrogationPanel != null && interrogationPanel.activeSelf && interrogationPanel != panelToShow) previousPanel = interrogationPanel;
//             else if (profilePanel != null && profilePanel.activeSelf && profilePanel != panelToShow) previousPanel = profilePanel;
//             else if (logPanel != null && logPanel.activeSelf && logPanel != panelToShow) previousPanel = logPanel;

//             if (monitorPanel != null) monitorPanel.SetActive(monitorPanel == panelToShow);
//             if (interrogationPanel != null) interrogationPanel.SetActive(interrogationPanel == panelToShow);
//             if (profilePanel != null) profilePanel.SetActive(profilePanel == panelToShow);
//             if (logPanel != null) logPanel.SetActive(logPanel == panelToShow);
//             if (endingPanel != null) endingPanel.SetActive(endingPanel == panelToShow);
//             if (resultPanel != null) resultPanel.SetActive(resultPanel == panelToShow);

//             if (panelToShow == profilePanel) RefreshProfileUIState();
//             else if (panelToShow == logPanel) BuildLogListUI();
//             else if (panelToShow == monitorPanel) RefreshMonitorRoomButtons();
//         }

//         public void OnClickOpenMonitorPanel() => ShowPanel(monitorPanel);

//         public void OnClickOpenInterrogationPanel()
//         {
//             ShowPanel(interrogationPanel);
//             UpdateInterrogationButtonsState();
//         }

//         public void OnClickOpenProfilePanel() => ShowPanel(profilePanel);
//         public void OnClickOpenLogPanel() => ShowPanel(logPanel);

//         public void OnClickReturnToPreviousPanel()
//         {
//             if (previousPanel != null) ShowPanel(previousPanel);
//             else ShowPanel(monitorPanel);
//         }

//         public void OnClickOpenResetDialog()
//         {
//             if (resetConfirmDialog != null) resetConfirmDialog.SetActive(true);
//         }

//         public void OnClickConfirmResetDay()
//         {
//             if (GameManager.Instance != null) GameManager.Instance.ResetCurrentDay();
//             if (resetConfirmDialog != null) resetConfirmDialog.SetActive(false);
//         }

//         public void OnClickCancelResetDay()
//         {
//             if (resetConfirmDialog != null) resetConfirmDialog.SetActive(false);
//         }

//         public void OnClickOpenEndDayDialog()
//         {
//             if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(true);
//         }

//         public void OnClickCancelEndDay()
//         {
//             if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(false);
//         }

//         #endregion

//         #region UIの自動制御・更新処理

//         private void UpdateInterrogationButtonsState()
//         {
//             CharacterInstance targetChar = GameManager.Instance != null && GameManager.Instance.AllCharacters != null
//                 ? GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId)
//                 : null;

//             bool isReleased = targetChar != null && targetChar.IsReleased;

//             bool isInterrogated = InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId);
//             bool isLimitReached = InterrogationManager.Instance != null && InterrogationManager.Instance.IsActionLimitReached;

//             bool canInteract = !isReleased && !isInterrogated && !isLimitReached && !isDialoguePlaying;

//             if (interrogationButtons != null)
//             {
//                 foreach (var btn in interrogationButtons)
//                 {
//                     if (btn != null) btn.interactable = canInteract;
//                 }
//             }

//             if (releaseButton != null)
//             {
//                 releaseButton.interactable = canInteract;
//             }

//             if (confirmAiButtonObject != null)
//             {
//                 int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
//                 bool isFinalDay = (currentDay >= finalDayNumber);

//                 confirmAiButtonObject.SetActive(isFinalDay && !isReleased);

//                 Button confirmBtn = confirmAiButtonObject.GetComponent<Button>();
//                 if (confirmBtn != null)
//                 {
//                     confirmBtn.interactable = canInteract;
//                 }
//             }
//         }

//         private void RefreshProfileUIState()
//         {
//             if (GameManager.Instance == null || GameManager.Instance.AllCharacters == null) return;

//             CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
//             if (targetChar == null || targetChar.ProfileData == null) return;

//             if (targetCharacterNameText != null)
//             {
//                 targetCharacterNameText.text = targetChar.ProfileData.basicInfo.characterName;
//             }

//             if (targetCharacterImage != null)
//             {
//                 targetCharacterImage.gameObject.SetActive(true);
//                 if (targetChar.ProfileData.characterIcon != null)
//                 {
//                     targetCharacterImage.sprite = targetChar.ProfileData.characterIcon;
//                 }
//             }

//             if (profileNameText != null)
//             {
//                 profileNameText.text = targetChar.ProfileData.basicInfo.characterName;
//             }

//             if (profileCharacterImage != null)
//             {
//                 profileCharacterImage.gameObject.SetActive(true);
//                 if (targetChar.ProfileData.characterIcon != null)
//                 {
//                     profileCharacterImage.sprite = targetChar.ProfileData.characterIcon;
//                 }
//             }

//             foreach (var binding in profileUIBindings)
//             {
//                 if (string.IsNullOrEmpty(binding.key)) continue;

//                 bool isUnlocked = false;
//                 string displayText = "";

//                 if (binding.key.Equals("BASIC", StringComparison.OrdinalIgnoreCase))
//                 {
//                     isUnlocked = targetChar.ProfileData.basicInfo.isUnlocked;

//                     var basic = targetChar.ProfileData.basicInfo;
//                     displayText = $"年齢：{basic.age}\n" +
//                                   $"職業：{basic.occupation}\n" +
//                                   $"誕生日：{basic.birthday}\n" +
//                                   $"身長：{basic.height}\n" +
//                                   $"体重：{basic.weight}\n" +
//                                   $"特技：{basic.skills}\n" +
//                                   $"好きなこと：{basic.likes}";
//                 }
//                 else
//                 {
//                     ProfileItem item = targetChar.ProfileData.historyInfoList.Find(i => i.itemKey == binding.key);
//                     if (item != null)
//                     {
//                         isUnlocked = item.isUnlocked;
//                         displayText = item.valueContent;
//                     }
//                 }

//                 if (binding.unlockButton != null) 
//                     binding.unlockButton.SetActive(!isUnlocked);

//                 if (binding.contentObject != null)
//                 {
//                     binding.contentObject.SetActive(isUnlocked);

//                     if (isUnlocked)
//                     {
//                         var contentTmp = binding.contentObject.GetComponentInChildren<TextMeshProUGUI>();
//                         if (contentTmp != null && !string.IsNullOrEmpty(displayText))
//                         {
//                             contentTmp.text = displayText;
//                         }
//                     }
//                 }
//             }
//         }

//         private void UpdateDayUI(int day)
//         {
//             if (dayText != null) dayText.text = $"{day} 日目";

//             isDialoguePlaying = false;
//             isTyping = false;

//             UpdateInterrogationButtonsState();
//             RefreshProfileUIState();
//             RefreshMonitorRoomButtons();
//         }

//         private void HandleDayResetUI()
//         {
//             StartDialogueSequence("システム", "（本日の朝の状態に巻き戻しました）");
//             UpdateInterrogationButtonsState();
//             RefreshProfileUIState();
//             RefreshMonitorRoomButtons();
//         }

//         #endregion

//         #region 文字送り & ログ（特殊タグ [SYS], [IMG:xxx] 対応）

//         private void OnPlayerClickDialogue()
//         {
//             if (isTyping)
//             {
//                 if (typingCoroutine != null) StopCoroutine(typingCoroutine);
//                 if (dialogueText != null) dialogueText.text = currentFullPageText;
//                 isTyping = false;
//                 SetNextPageIconActive(dialoguePages.Count > 0);
//             }
//             else
//             {
//                 AdvanceToNextDialoguePage();
//             }
//         }

//         private void StartDialogueSequence(string speakerName, string fullText)
//         {
//             dialoguePages.Clear();
//             if (typingCoroutine != null) StopCoroutine(typingCoroutine);

//             if (string.IsNullOrEmpty(fullText))
//             {
//                 SetChatboxActive(false);
//                 return;
//             }

//             currentSpeakerName = speakerName;

//             string[] pages = fullText.Split('|');
//             foreach (var p in pages)
//             {
//                 string trimmed = p.Trim();
//                 if (!string.IsNullOrEmpty(trimmed))
//                 {
//                     dialoguePages.Enqueue(trimmed);

//                     string cleanLogText = Regex.Replace(trimmed, @"\[(SYS|IMG:.*?)\]", "");
//                     string logSpeaker = trimmed.StartsWith("[SYS]") ? "状況" : speakerName;
//                     dialogueLogList.Add(new DialogueLogData(logSpeaker, cleanLogText));
//                 }
//             }

//             if (dialoguePages.Count == 0) return;

//             SetChatboxActive(true);
//             isDialoguePlaying = true;
//             AdvanceToNextDialoguePage();
//         }

//         private void AdvanceToNextDialoguePage()
//         {
//             if (dialoguePages.Count > 0)
//             {
//                 string rawPageText = dialoguePages.Dequeue();
//                 SetNextPageIconActive(false);

//                 bool isSystemMessage = false;
//                 if (rawPageText.StartsWith("[SYS]"))
//                 {
//                     isSystemMessage = true;
//                     rawPageText = rawPageText.Substring(5);
//                 }

//                 if (targetCharacterNameText != null)
//                 {
//                     targetCharacterNameText.text = isSystemMessage ? "" : currentSpeakerName;
//                 }

//                 if (rawPageText.StartsWith("[IMG:"))
//                 {
//                     int closeIndex = rawPageText.IndexOf(']');
//                     if (closeIndex > 0)
//                     {
//                         string spriteKey = rawPageText.Substring(5, closeIndex - 5);
//                         rawPageText = rawPageText.Substring(closeIndex + 1);

//                         if (spriteKey.Equals("OFF", StringComparison.OrdinalIgnoreCase) || spriteKey.Equals("HIDE", StringComparison.OrdinalIgnoreCase))
//                         {
//                             if (targetCharacterImage != null)
//                             {
//                                 targetCharacterImage.gameObject.SetActive(false);
//                             }
//                         }
//                         else
//                         {
//                             if (targetCharacterImage != null)
//                             {
//                                 targetCharacterImage.gameObject.SetActive(true);
//                                 var binding = characterSpriteBindings.Find(b => b.spriteKey.Equals(spriteKey, StringComparison.OrdinalIgnoreCase));
//                                 if (binding.sprite != null)
//                                 {
//                                     targetCharacterImage.sprite = binding.sprite;
//                                 }
//                             }
//                         }
//                     }
//                 }

//                 currentFullPageText = rawPageText;

//                 if (typingCoroutine != null) StopCoroutine(typingCoroutine);
//                 typingCoroutine = StartCoroutine(TypeTextCoroutine(currentFullPageText));
//             }
//             else
//             {
//                 EndDialogueSequence();
//             }
//         }

//         private IEnumerator TypeTextCoroutine(string textToType)
//         {
//             isTyping = true;
//             if (dialogueText != null) dialogueText.text = "";

//             bool isInsideTag = false;
//             string currentDisplayedText = "";

//             for (int i = 0; i < textToType.Length; i++)
//             {
//                 char c = textToType[i];
//                 if (c == '<') isInsideTag = true;
//                 if (c == '>') isInsideTag = false;

//                 currentDisplayedText += c;
//                 if (dialogueText != null) dialogueText.text = currentDisplayedText;

//                 if (!isInsideTag && textSpeed > 0f) yield return new WaitForSeconds(textSpeed);
//             }

//             isTyping = false;
//             SetNextPageIconActive(dialoguePages.Count > 0);
//         }

//         private void EndDialogueSequence()
//         {
//             isDialoguePlaying = false;
//             isTyping = false;
//             SetNextPageIconActive(false);
//             UpdateInterrogationButtonsState();
//         }

//         private void BuildLogListUI()
//         {
//             if (logContentParent == null || logItemPrefab == null) return;

//             foreach (Transform child in logContentParent) Destroy(child.gameObject);

//             foreach (var log in dialogueLogList)
//             {
//                 GameObject itemObj = Instantiate(logItemPrefab, logContentParent);
//                 LogItemUI itemUI = itemObj.GetComponent<LogItemUI>();
//                 if (itemUI != null) itemUI.SetLog(log.speakerName, log.contentText);
//             }
//         }

//         private void SetChatboxActive(bool isActive)
//         {
//             if (interrogationChatbox != null) interrogationChatbox.SetActive(isActive);
//         }

//         private void SetNextPageIconActive(bool isActive)
//         {
//             if (nextPageIcon != null) nextPageIcon.SetActive(isActive);
//         }

//         private void UpdateInterrogationRoomDisplay(string characterId)
//         {
//             if (GameManager.Instance == null || GameManager.Instance.AllCharacters == null) return;

//             CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == characterId);
//             if (targetChar != null && targetChar.ProfileData != null)
//             {
//                 if (targetCharacterNameText != null) targetCharacterNameText.text = targetChar.ProfileData.basicInfo.characterName;
//                 if (targetCharacterImage != null)
//                 {
//                     targetCharacterImage.gameObject.SetActive(true);
//                     targetCharacterImage.sprite = targetChar.ProfileData.characterIcon;
//                 }
//             }
//         }

//         private void UpdatePointUI(int points)
//         {
//             if (pointText != null) pointText.text = $"{points} pt";
//         }

//         private void UpdateActionCountUI(int currentActions, int maxLimit)
//         {
//             if (actionCountText != null)
//             {
//                 int remaining = Mathf.Max(0, maxLimit - currentActions);
//                 actionCountText.text = $"残り行動: {remaining}/{maxLimit}";
//             }
//             UpdateInterrogationButtonsState();
//         }

//         #endregion
//     }
// }