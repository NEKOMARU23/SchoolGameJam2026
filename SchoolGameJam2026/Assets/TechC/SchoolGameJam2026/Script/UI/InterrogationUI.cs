using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
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
        [SerializeField] private Transform characterButtonContainer;
        [SerializeField] private Button closeHearingPanelButton;

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

                Button confirmBtn = confirmAiButtonObject.GetComponent<Button>();
                if (confirmBtn != null)
                {
                    confirmBtn.interactable = canInteract;
                }
            }
        }

        #endregion

        #region 聞き取りパネル制御

        public void OnClickOpenHearingPanel()
        {
            if (hearingPanel == null || characterButtonContainer == null) return;
            if (GameManager.Instance == null || GameManager.Instance.AllCharacters == null) return;

            Button[] existingButtons = characterButtonContainer.GetComponentsInChildren<Button>(true);

            foreach (var btn in existingButtons)
            {
                TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                string btnTextName = btnText != null ? btnText.text : "";

                // ボタンに登録されたキャラクターを特定してイベント登録
                CharacterInstance targetChar = GameManager.Instance.AllCharacters.Find(c =>
                    (c.ProfileData != null && c.ProfileData.basicInfo.characterName == btnTextName) ||
                    btn.gameObject.name.EndsWith(c.CharacterId) ||
                    btn.gameObject.name.Contains(c.CharacterId)
                );

                if (targetChar != null)
                {
                    string targetId = targetChar.CharacterId;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        if (gameUIController != null)
                        {
                            gameUIController.OnSelectHearingTarget(targetId);
                        }
                    });
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