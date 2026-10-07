using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    [Serializable]
    public struct HearingButtonBinding
    {
        public string characterId;
        public Button button;
    }

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
        /// 選択されているキャラクターの画像と名前を描画更新
        /// </summary>
        public void UpdateCharacterRoomDisplay(CharacterInstance targetChar)
        {
            if (targetChar == null || targetChar.ProfileData == null) return;

            if (targetCharacterNameText != null)
            {
                targetCharacterNameText.text = targetChar.ProfileData.basicInfo.characterName;
            }

            if (targetCharacterImage != null)
            {
                targetCharacterImage.gameObject.SetActive(true);
                targetCharacterImage.sprite = targetChar.ProfileData.characterIcon;
            }
        }

        /// <summary>
        /// 状態に応じて取調室のボタンの有効/無効を更新
        /// </summary>
        public void UpdateButtonsInteractable(bool isReleased, bool isInterrogated, bool isLimitReached, bool isDialoguePlaying, bool isFinalDay)
        {
            bool canInteract = !isReleased && !isInterrogated && !isLimitReached && !isDialoguePlaying;

            if (interrogationButtons != null)
            {
                foreach (var btn in interrogationButtons)
                {
                    if (btn != null) btn.interactable = canInteract;
                }
            }

            if (releaseButton != null)
            {
                releaseButton.interactable = canInteract;
            }

            if (confirmAiButtonObject != null)
            {
                confirmAiButtonObject.SetActive(isFinalDay && !isReleased);

                // AI確定は最終判定のため、行動回数の上限や当日の取り調べ済みに関係なく押せる
                Button confirmBtn = confirmAiButtonObject.GetComponent<Button>();
                if (confirmBtn != null)
                {
                    confirmBtn.interactable = !isReleased && !isDialoguePlaying;
                }
            }
        }

        #endregion

        #region 聞き取りパネル制御

        public void OnClickOpenHearingPanel()
        {
            if (hearingPanel == null) return;

            // 取り調べ中の本人については聞けないため、本人のボタンは隠す
            string selfId = gameUIController != null ? gameUIController.CurrentSelectedCharacterId : null;
            foreach (var binding in hearingButtonBindings)
            {
                if (binding.button != null)
                {
                    binding.button.gameObject.SetActive(binding.characterId != selfId);
                }
            }

            hearingPanel.SetActive(true);
        }

        private void OnClickHearingTarget(string targetId)
        {
            if (gameUIController != null)
            {
                gameUIController.OnSelectHearingTarget(targetId);
            }
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