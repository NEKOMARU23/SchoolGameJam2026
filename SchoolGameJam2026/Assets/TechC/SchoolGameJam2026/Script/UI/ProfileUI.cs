using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TechC.SchoolGameJam2026.Scripts.Core;
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
        [SerializeField] private ScreenNavigationUI navigationUI;

        [Header("--- プロフィール表示パーツ ---")]
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private Image profileCharacterImage;

        [Header("--- プロフィール（資料）UI登録リスト ---")]
        [SerializeField] private List<ProfileUIBinding> profileUIBindings = new List<ProfileUIBinding>();

        // プロフィール画面独自で表示中のキャラIDを保持（拷問対象と連動させない）
        private string currentViewingCharacterId;

        /// <summary>
        /// 選択されているキャラクターのプロフィール画面描画を更新（描画専用）
        /// </summary>
        public void RefreshProfileDisplay(CharacterInstance targetChar)
        {
            if (targetChar == null || targetChar.ProfileData == null)
            {
                Debug.LogWarning("[ProfileUI] RefreshProfileDisplay: targetChar または ProfileData が null のため更新をスキップしました。");
                return;
            }

            currentViewingCharacterId = targetChar.CharacterId;

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
                    if (item != null)
                    {
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

        /// <summary>
        /// キャラクター選択切り替えボタン用 (OnClick)
        /// プロフィール表示のみを切り替え、拷問パネルの選択キャラは変更しません。
        /// </summary>
        public void OnClickSelectCharacter(string characterId)
        {
            if (gameUIController != null)
            {
                gameUIController.ShowProfileOnly(characterId);
            }
        }

        /// <summary>
        /// ナビゲーションバー等の「戻る」ボタン用 (OnClick)
        /// </summary>
        public void OnClickBack()
        {
            if (navigationUI != null)
            {
                navigationUI.BackToPreviousPanel();
            }
            else if (gameUIController != null)
            {
                gameUIController.OnClickReturnToInterrogationOrMonitor();
            }
        }

        public void OnClickUnlockBasicProfile()
        {
            if (gameUIController != null)
            {
                gameUIController.TryUnlockBasicProfileForCharacter(currentViewingCharacterId);
            }
        }

        public void OnClickUnlockProfileItemByKey(string itemKey)
        {
            if (gameUIController != null)
            {
                gameUIController.TryUnlockProfileItemForCharacter(currentViewingCharacterId, itemKey);
            }
        }

        #endregion
    }
}