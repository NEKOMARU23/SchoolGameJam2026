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
        /// <param name="canAct">拷問・対話・交渉・聞き取り・解放のボタンを押せるか</param>
        /// <param name="showConfirmAI">AI確定ボタンを表示するか</param>
        /// <param name="canConfirmAI">AI確定ボタンを押せるか</param>
        public void UpdateActionButtons(bool canAct, bool showConfirmAI, bool canConfirmAI)
        {
            if (interrogationButtons != null)
            {
                foreach (var btn in interrogationButtons)
                {
                    if (btn != null) btn.interactable = canAct;
                }
            }

            if (releaseButton != null)
            {
                releaseButton.interactable = canAct;
            }

            if (confirmAiButtonObject != null)
            {
                confirmAiButtonObject.SetActive(showConfirmAI);

                Button confirmBtn = confirmAiButtonObject.GetComponent<Button>();
                if (confirmBtn != null)
                {
                    confirmBtn.interactable = canConfirmAI;
                }
            }
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
