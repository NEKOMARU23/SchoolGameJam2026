using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;

namespace TechC.SchoolGameJam2026.Scripts.Core
{
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
        
        // ★追加：テキスト枠全体をまとめる親オブジェクト (Interrogation_Chatbox)
        [Header("--- メッセージウィンドウ ---")]
        [Tooltip("取り調べコマンド選択時に表示するメッセージボックス親オブジェクト")]
        [SerializeField] private GameObject interrogationChatbox;

        [Header("--- 取調室のアクションボタン群 ---")]
        [Tooltip("拷問、対話、交渉、聞き取り の4つのボタンを設定（1日1回使用後の無効化制御用）")]
        [SerializeField] private Button[] interrogationButtons;

        [Header("--- 画面パネル切り替え ---")]
        [SerializeField] private GameObject monitorPanel;              // モニター選択画面
        [SerializeField] private GameObject interrogationPanel;        // 取調室画面
        [SerializeField] private GameObject profilePanel;              // 資料（DB）画面
        [SerializeField] private GameObject resetConfirmDialog;        // リセット確認ポップアップ
        [SerializeField] private GameObject dayEndConfirmDialog;       // 一日終了確認ポップアップ

        [Header("--- 現在選択中のデータ状態 ---")]
        [SerializeField] private string currentSelectedCharacterId = "A";
        [SerializeField] private string currentHearingTargetId = "B";  // 聞き取り時に指定する他キャラID

        private void OnEnable()
        {
            // Managerイベントの購読
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
            // 初期画面UIの更新
            if (InterrogationManager.Instance != null)
            {
                UpdatePointUI(InterrogationManager.Instance.CurrentPoints);
                UpdateActionCountUI(InterrogationManager.Instance.CurrentDailyActions, InterrogationManager.Instance.DailyActionLimit);
            }
            if (GameManager.Instance != null)
            {
                UpdateDayUI(GameManager.Instance.CurrentDay);
            }

            // ★ 初期状態ではチャットボックスを隠しておく
            SetChatboxActive(false);
        }

        #region ★ Button の OnClick() に登録するメソッド群

        /// <summary>
        /// 取調対象のキャラクターを選択する
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            currentSelectedCharacterId = characterId;
            Debug.Log($"対象キャラクターを【{characterId}】にセットしました。");

            // 取調室内の表示（名前やアイコン）を更新
            UpdateInterrogationRoomDisplay(characterId);

            // 対象キャラの1日1回制限状態に合わせてボタンの活性/非活性を切り替え
            UpdateInterrogationButtonsState();

            // ★ キャラクター（部屋）を切り替えたときは一旦チャットボックスを非表示にする
            SetChatboxActive(false);
        }

        /// <summary>
        /// モニター画面の部屋選択ボタン用
        /// キャラクターを指定して、直接その部屋の取調室画面を開く
        /// </summary>
        /// <param name="characterId">部屋に対応するキャラID ("A", "B", "C", "D" など)</param>
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
        /// <param name="actionIndex">0=Torture, 1=Talk, 2=Negotiate, 3=HearAbout</param>
        public void OnClickInterrogationButton(int actionIndex)
        {
            // 1日1回チェック：既に今日取り調べ済みの場合は処理しない
            if (InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId))
            {
                if (dialogueText != null) dialogueText.text = "（このキャラクターへの本日の取り調べは既に終了しています）";
                SetChatboxActive(true); // ★ 警告表示のためテキストボックスを表示
                UpdateInterrogationButtonsState();
                return;
            }

            InterrogationActionType actionType = (InterrogationActionType)actionIndex;

            // 対象キャラクターインスタンスの検索
            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
            if (targetChar == null)
            {
                Debug.LogError($"ID: {currentSelectedCharacterId} のキャラクターが見つかりません。");
                return;
            }

            // アクション実行とセリフテキストの更新
            string resultDialogue = InterrogationManager.Instance.ExecuteAction(targetChar, actionType, currentHearingTargetId);

            if (dialogueText != null)
            {
                dialogueText.text = resultDialogue;
            }

            // ★ コマンド実行後にテキストのグループ（Interrogation_Chatbox）を表示する
            SetChatboxActive(true);

            // アクション実行後、ボタンを非活性化（1日1回制限の適用）
            UpdateInterrogationButtonsState();
        }

        /// <summary>
        /// 資料画面の「基礎情報 解禁ボタン」を押した時（グループ一括解禁）
        /// </summary>
        public void OnClickUnlockBasicProfile()
        {
            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            BasicProfileGroup basicGroup = targetChar.ProfileData.basicInfo;

            if (basicGroup.isUnlocked)
            {
                Debug.Log("すでに基礎情報は解禁されています！");
                return;
            }

            if (InterrogationManager.Instance.TrySpendPoints(basicGroup.unlockCost))
            {
                basicGroup.isUnlocked = true;
                Debug.Log($"<color=green>【解禁成功】{targetChar.CharacterId} の基礎情報を解禁しました！ (消費: {basicGroup.unlockCost}pt)</color>");
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
            }
            else
            {
                Debug.Log("ポイントが足りません！");
            }
        }

        /// <summary>
        /// 資料画面の「過去情報 解禁ボタン」を押した時（UnityのOnClick用：キー文字列で指定）
        /// </summary>
        /// <param name="itemKey">解禁したいプロフィールのitemKey</param>
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
        // ★ 画面パネル切り替え機能（1年生のボタン用）
        // --------------------------------------------------

        /// <summary>
        /// 指定したパネルだけを表示し、他の主要画面パネルを非表示にする
        /// </summary>
        public void ShowPanel(GameObject panelToShow)
        {
            if (monitorPanel != null) monitorPanel.SetActive(monitorPanel == panelToShow);
            if (interrogationPanel != null) interrogationPanel.SetActive(interrogationPanel == panelToShow);
            if (profilePanel != null) profilePanel.SetActive(profilePanel == panelToShow);
        }

        /// <summary> モニター画面を開く </summary>
        public void OnClickOpenMonitorPanel() => ShowPanel(monitorPanel);

        /// <summary> 取調室画面を開く </summary>
        public void OnClickOpenInterrogationPanel()
        {
            ShowPanel(interrogationPanel);
            UpdateInterrogationButtonsState(); // 開いた時にボタン状態を更新
        }

        /// <summary> 資料画面を開く </summary>
        public void OnClickOpenProfilePanel() => ShowPanel(profilePanel);

        // --------------------------------------------------
        // ★ 当日の朝にリセットするダイアログ関連
        // --------------------------------------------------

        /// <summary>
        /// リスタートボタンを押してリセット確認ダイアログを開く
        /// </summary>
        public void OnClickOpenResetDialog()
        {
            if (resetConfirmDialog != null) resetConfirmDialog.SetActive(true);
        }

        /// <summary>
        /// リセット確認ダイアログで「はい」を押した時
        /// </summary>
        public void OnClickConfirmResetDay()
        {
            GameManager.Instance.ResetCurrentDay();
            if (resetConfirmDialog != null) resetConfirmDialog.SetActive(false);
        }

        /// <summary>
        /// リセット確認ダイアログで「いいえ」を押した時
        /// </summary>
        public void OnClickCancelResetDay()
        {
            if (resetConfirmDialog != null) resetConfirmDialog.SetActive(false);
        }

        // --------------------------------------------------
        // ★ 一日を終了して翌日へ進むダイアログ関連
        // --------------------------------------------------

        /// <summary>
        /// 「一日を終了する」ボタンを押して終了確認ダイアログを開く
        /// </summary>
        public void OnClickOpenEndDayDialog()
        {
            if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(true);
            Debug.Log("一日終了の確認ダイアログを開きました。");
        }

        /// <summary>
        /// 一日終了確認ダイアログで「はい」を押した時（次の日へ進む）
        /// </summary>
        public void OnClickConfirmAdvanceNextDay()
        {
            if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(false);

            // GameManager側で次の日をセットしてロード
            GameManager.Instance.AdvanceToNextDay();
        }

        /// <summary>
        /// 一日終了確認ダイアログで「いいえ」を押した時
        /// </summary>
        public void OnClickCancelEndDay()
        {
            if (dayEndConfirmDialog != null) dayEndConfirmDialog.SetActive(false);
            Debug.Log("一日の終了をキャンセルしました。");
        }

        #endregion

        #region UI更新用の内部処理

        /// <summary>
        /// ★ チャットボックス全体の表示/非表示を制御する内部メソッド
        /// </summary>
        private void SetChatboxActive(bool isActive)
        {
            if (interrogationChatbox != null)
            {
                interrogationChatbox.SetActive(isActive);
            }
        }

        /// <summary>
        /// 現在選択されているキャラが本日行動済みか、または全体の行動限界に達しているかに応じてボタンの有効/無効を切り替える
        /// </summary>
        private void UpdateInterrogationButtonsState()
        {
            if (interrogationButtons == null || interrogationButtons.Length == 0) return;

            bool isInterrogated = InterrogationManager.Instance != null && InterrogationManager.Instance.HasInterrogatedToday(currentSelectedCharacterId);
            bool isLimitReached = InterrogationManager.Instance != null && InterrogationManager.Instance.IsActionLimitReached;

            // 「本日まだ行動していない」かつ「全体行動回数の上限に達していない」場合のみボタンを押せるようにする
            bool canInteract = !isInterrogated && !isLimitReached;

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

            // 行動回数が変わった時もボタン状態を再チェック
            UpdateInterrogationButtonsState();
        }

        private void UpdateDayUI(int day)
        {
            if (dayText != null) dayText.text = $"{day} 日目";
            SetChatboxActive(false); // ★ 新しい日になったらチャットボックスを隠す
            UpdateInterrogationButtonsState();
        }

        private void HandleDayResetUI()
        {
            if (dialogueText != null) dialogueText.text = "（本日の朝の状態に巻き戻しました）";
            SetChatboxActive(false); // ★ リセット時もチャットボックスを非表示に戻す
            UpdateInterrogationButtonsState();
            Debug.Log("UIを朝の状態へリセットしました。");
        }

        #endregion
    }
}