using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.Core;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.SO; // ProfileItem が定義されている ScriptableObject の名前空間を追加

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

        /// <summary>
        /// 選択されているキャラクターのプロフィール画面描画を更新（描画専用）
        /// </summary>
        public void RefreshProfileDisplay(CharacterInstance targetChar)
        {
            if (targetChar == null || targetChar.ProfileData == null) return;

            // 名前・アイコンの表示更新
            if (profileNameText != null)
            {
                profileNameText.text = targetChar.ProfileData.basicInfo.characterName;
            }

            if (profileCharacterImage != null)
            {
                profileCharacterImage.gameObject.SetActive(true);
                if (targetChar.ProfileData.characterIcon != null)
                {
                    profileCharacterImage.sprite = targetChar.ProfileData.characterIcon;
                }
            }

            // 各項目（基本情報・開放情報）のUI切り替え
            foreach (var binding in profileUIBindings)
            {
                if (string.IsNullOrEmpty(binding.key)) continue;

                bool isUnlocked = false;
                string displayText = "";

                if (binding.key.Equals("BASIC", StringComparison.OrdinalIgnoreCase))
                {
                    isUnlocked = targetChar.ProfileData.basicInfo.isUnlocked;

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
                    // 明示的な型指定（using TechC.SchoolGameJam2026.Scripts.SO により ProfileItem が参照可能）
                    ProfileItem item = targetChar.ProfileData.historyInfoList.Find(i => i.itemKey == binding.key);
                    if (item != null)
                    {
                        isUnlocked = item.isUnlocked;
                        displayText = item.valueContent;
                    }
                }

                if (binding.unlockButton != null)
                {
                    binding.unlockButton.SetActive(!isUnlocked);
                }

                if (binding.contentObject != null)
                {
                    binding.contentObject.SetActive(isUnlocked);

                    if (isUnlocked)
                    {
                        TextMeshProUGUI contentTmp = binding.contentObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (contentTmp != null && !string.IsNullOrEmpty(displayText))
                        {
                            contentTmp.text = displayText;
                        }
                    }
                }
            }
        }

        #region UIボタンからのイベント通知窓口

        public void OnClickUnlockBasicProfile()
        {
            if (gameUIController != null)
            {
                gameUIController.TryUnlockBasicProfile();
            }
        }

        public void OnClickUnlockProfileItemByKey(string itemKey)
        {
            if (gameUIController != null)
            {
                gameUIController.TryUnlockProfileItem(itemKey);
            }
        }

        #endregion
    }
}