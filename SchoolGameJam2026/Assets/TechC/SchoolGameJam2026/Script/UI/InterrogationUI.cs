using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    [Serializable]
    public struct HearingButtonBinding
    {
        public string characterId;
        public Button button;
    }

    /// <summary>
    /// 取調室の表示を担当するビュー（判定は行わず、渡された結果を表示するだけ）
    /// </summary>
    public class InterrogationUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- キャラクター表示 ---")]
        [SerializeField] private TextMeshProUGUI targetCharacterNameText;
        [SerializeField] private Image targetCharacterImage;

        [Header("--- 取調室のアクションボタン群 ---")]
        [SerializeField] private Button[] interrogationButtons;
        [SerializeField] private Button releaseButton;
        [SerializeField] private GameObject confirmAiButtonObject;

        [Header("--- 「今日はもう取り調べました」の札（取り調べのボタンの代わりに出す） ---")]
        [SerializeField] private GameObject interrogatedNotice;

        [Header("--- 聞き取り（他キャラ選択）パネルUI ---")]
        [SerializeField] private GameObject hearingPanel;
        [SerializeField] private List<HearingButtonBinding> hearingButtonBindings = new List<HearingButtonBinding>();
        [SerializeField] private Button closeHearingPanelButton;

        private void Awake()
        {
            // 各聞き取りボタンに対象キャラIDをバインド
            foreach (var binding in hearingButtonBindings)
            {
                if (binding.button == null) continue;

                string targetId = binding.characterId;
                binding.button.onClick.AddListener(() => OnClickHearingTarget(targetId));
            }
        }

        private void Start()
        {
            if (closeHearingPanelButton != null)
            {
                closeHearingPanelButton.onClick.AddListener(CloseHearingPanel);
            }

            if (hearingPanel != null)
            {
                hearingPanel.SetActive(false);
            }
        }

        #region 表示更新メソッド (GameUIControllerから呼ばれる)

        /// <summary>
        /// 取り調べ中のキャラクターの名前と画像を表示
        /// </summary>
        public void UpdateCharacterRoomDisplay(string characterName, Sprite characterIcon)
        {
            if (targetCharacterNameText != null)
            {
                targetCharacterNameText.text = characterName;
            }

            if (targetCharacterImage != null)
            {
                targetCharacterImage.gameObject.SetActive(true);
                targetCharacterImage.sprite = characterIcon;
            }
        }

        /// <summary>
        /// 取調室のボタンの表示・押下可否を更新
        /// </summary>
        /// <param name="showInterrogation">拷問・対話・交渉・聞き取りのボタンを表示するか</param>
        /// <param name="canInterrogate">拷問・対話・交渉・聞き取りのボタンを押せるか</param>
        /// <param name="showJudgement">解放・AI確定のボタンを表示するか</param>
        /// <param name="canJudge">解放・AI確定のボタンを押せるか</param>
        /// <param name="showInterrogatedNotice">「今日はもう取り調べました」を出すか</param>
        public void UpdateActionButtons(bool showInterrogation, bool canInterrogate, bool showJudgement, bool canJudge, bool showInterrogatedNotice = false)
        {
            if (interrogatedNotice != null) interrogatedNotice.SetActive(showInterrogatedNotice);

            if (interrogationButtons != null)
            {
                foreach (var btn in interrogationButtons)
                {
                    if (btn == null) continue;
                    btn.gameObject.SetActive(showInterrogation);
                    btn.interactable = canInterrogate;
                }
            }

            if (releaseButton != null)
            {
                releaseButton.gameObject.SetActive(showJudgement);
                releaseButton.interactable = canJudge;
            }

            if (confirmAiButtonObject != null)
            {
                confirmAiButtonObject.SetActive(showJudgement);

                Button confirmBtn = confirmAiButtonObject.GetComponent<Button>();
                if (confirmBtn != null)
                {
                    confirmBtn.interactable = canJudge;
                }
            }

            if (!showInterrogation) CloseHearingPanel();
        }

        #endregion

        #region 聞き取りパネル制御

        /// <summary>
        /// 聞き取りパネルを開く（選べるキャラのボタンだけを表示）
        /// </summary>
        public void ShowHearingPanel(ICollection<string> selectableCharacterIds)
        {
            if (hearingPanel == null) return;

            foreach (var binding in hearingButtonBindings)
            {
                if (binding.button != null)
                {
                    binding.button.gameObject.SetActive(selectableCharacterIds != null && selectableCharacterIds.Contains(binding.characterId));
                }
            }

            hearingPanel.SetActive(true);
        }

        public void CloseHearingPanel()
        {
            if (hearingPanel != null)
            {
                hearingPanel.SetActive(false);
            }
        }

        #endregion

        #region UIボタンから呼び出すイベンター

        public void OnClickOpenHearingPanel()
        {
            if (gameUIController != null) gameUIController.OpenHearingPanel();
        }

        private void OnClickHearingTarget(string targetId)
        {
            if (gameUIController != null) gameUIController.OnSelectHearingTarget(targetId);
        }

        public void OnClickInterrogationButton(int actionIndex)
        {
            if (gameUIController != null) gameUIController.ExecuteInterrogationAction(actionIndex);
        }

        public void OnClickReleaseButton()
        {
            if (gameUIController != null) gameUIController.ExecuteReleaseCharacter();
        }

        public void OnClickConfirmAiButton()
        {
            if (gameUIController != null) gameUIController.ExecuteConfirmAI();
        }

        #endregion
    }
}
