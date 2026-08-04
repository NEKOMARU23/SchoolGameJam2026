using UnityEngine;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;

namespace TechC.SchoolGameJam2026.Scripts.Core
{
    /// <summary>
    /// 1年生が配置したUIボタンやテキストと、ゲームのロジック（Manager類）を繋ぐクラス
    /// </summary>
    public class UIManagerBridge : MonoBehaviour
    {
        [Header("--- 表示系UI（TextMeshPro） ---")]
        [SerializeField] private TextMeshProUGUI dayText;               // 日数表示 (例: "1日目")
        [SerializeField] private TextMeshProUGUI actionCountText;      // 残り行動回数表示 (例: "残り: 3/4")
        [SerializeField] private TextMeshProUGUI pointText;            // 所持ポイント表示 (例: "120 pt")
        [SerializeField] private TextMeshProUGUI dialogueText;         // キャラクターのセリフ表示枠

        [Header("--- 画面パネル切り替え ---")]
        [SerializeField] private GameObject monitorPanel;              // モニター選択画面
        [SerializeField] private GameObject interrogationPanel;        // 取調室画面
        [SerializeField] private GameObject profilePanel;              // 資料（DB）画面
        [SerializeField] private GameObject resetConfirmDialog;        // リセット確認ポップアップ

        [Header("--- 現在選択中のデータ状態 ---")]
        [SerializeField] private string currentSelectedCharacterId = "A";
        [SerializeField] private string currentHearingTargetId = "B"; // 聞き取り時に指定する他キャラID

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
        }

        #region ★ 1年生が Button の OnClick() に登録するメソッド群

        /// <summary>
        /// 取調対象のキャラクターを選択する（モニター画面等から呼ぶ）
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            currentSelectedCharacterId = characterId;
            Debug.Log($"対象キャラクターを【{characterId}】にセットしました。");
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
        }

        /// <summary>
        /// 資料画面の「解禁ボタン」を押した時
        /// </summary>
        public void OnClickUnlockProfileItem(ProfileItem item)
        {
            if (item.isUnlocked) return;

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
        /// リスタートボタンを押してダイアログを開く
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

        #endregion

        #region UI更新用の内部処理

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
        }

        private void UpdateDayUI(int day)
        {
            if (dayText != null) dayText.text = $"{day} 日目";
        }

        private void HandleDayResetUI()
        {
            if (dialogueText != null) dialogueText.text = "（本日の朝の状態に巻き戻しました）";
            Debug.Log("UIを朝の状態へリセットしました。");
        }

        /// <summary>
        /// 【テスト用】現在選択中のキャラの基礎情報をポイント消費して解禁する
        /// </summary>
        public void OnClickTestUnlockBasicInfo()
        {
            CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c => c.CharacterId == currentSelectedCharacterId);
            if (targetChar == null || targetChar.ProfileData == null) return;

            var basicGroup = targetChar.ProfileData.basicInfo;

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

        #endregion
    }
}