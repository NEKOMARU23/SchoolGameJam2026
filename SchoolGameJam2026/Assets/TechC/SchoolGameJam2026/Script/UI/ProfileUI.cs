using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.SO;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    [Serializable]
    public struct ProfileUIBinding
    {
        public string key;
        public GameObject unlockButton;
        public GameObject contentObject;
    }

    public class ProfileUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- プロフィール表示パーツ ---")]
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private Image profileCharacterImage;

        [Header("--- プロフィール（資料）UI登録リスト ---")]
        [SerializeField] private List<ProfileUIBinding> profileUIBindings = new List<ProfileUIBinding>();

        private string currentViewingCharacterId;

        public void RefreshProfileDisplay(CharacterInstance targetChar)
        {
            if (targetChar == null || targetChar.ProfileData == null) return;

            currentViewingCharacterId = targetChar.CharacterId;

            if (profileNameText != null)
                profileNameText.text = targetChar.ProfileData.basicInfo.characterName;

            if (profileCharacterImage != null)
            {
                profileCharacterImage.gameObject.SetActive(true);
                if (targetChar.ProfileData.characterIcon != null)
                    profileCharacterImage.sprite = targetChar.ProfileData.characterIcon;
            }

            foreach (var binding in profileUIBindings)
            {
                if (string.IsNullOrEmpty(binding.key)) continue;

                bool isUnlocked = false;
                string displayText = "";

                if (binding.key.Equals("BASIC", StringComparison.OrdinalIgnoreCase))
                {
                    isUnlocked = targetChar.IsBasicInfoUnlocked;
                    BasicProfileGroup basic = targetChar.ProfileData.basicInfo;
                    displayText = $"年齢：{basic.age}\n" +
                                  $"職業：{basic.occupation}\n" +
                                  $"誕生日：{basic.birthday}\n" +
                                  $"身長：{basic.height}\n" +
                                  $"体重：{basic.weight}\n" +
                                  $"特技：{basic.skills}\n" +
                                  $"好きなこと：{basic.likes}";
                }
                else
                {
                    isUnlocked = targetChar.IsHistoryUnlocked(binding.key);
                    ProfileItem item = targetChar.ProfileData.historyInfoList.Find(i => i.itemKey == binding.key);
                    if (item != null) displayText = item.valueContent;
                }

                if (binding.unlockButton != null)
                    binding.unlockButton.SetActive(!isUnlocked);

                if (binding.contentObject != null)
                {
                    binding.contentObject.SetActive(isUnlocked);
                    if (isUnlocked)
                    {
                        TextMeshProUGUI contentTmp = binding.contentObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (contentTmp != null && !string.IsNullOrEmpty(displayText))
                            contentTmp.text = displayText;
                    }
                }
            }
        }

        #region UIボタンからのイベント通知窓口

        public void OnClickSelectCharacter(string characterId)
        {
            if (gameUIController != null) gameUIController.ShowProfileOnly(characterId);
        }

        /// <summary>
        /// プロフィール項目の解放ボタン用（OnClick）
        /// 引数には Unity の Inspector 上で "BASIC" や "PAST_CRIME" などの itemKey を渡します。
        /// </summary>
        public void OnClickUnlockProfileItem(string itemKey)
        {
            if (gameUIController != null) gameUIController.TryUnlockProfileItemForCharacter(currentViewingCharacterId, itemKey);
        }

        #endregion
    }
}