using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    [Serializable]
    public struct ProfileUIBinding
    {
        public string key;
        public GameObject unlockButton;
        public GameObject contentObject;
    }

    /// <summary>
    /// プロフィール1項目分の表示データ
    /// </summary>
    public readonly struct ProfileItemViewData
    {
        public bool IsUnlocked { get; }
        public string Text { get; }

        public ProfileItemViewData(bool isUnlocked, string text)
        {
            IsUnlocked = isUnlocked;
            Text = text;
        }
    }

    /// <summary>
    /// プロフィール画面1キャラ分の表示データ
    /// </summary>
    public sealed class ProfileViewData
    {
        public string CharacterName { get; }
        public Sprite CharacterIcon { get; }

        /// <summary> 項目キー（"BASIC" や過去情報の itemKey）ごとの表示データ </summary>
        public IReadOnlyDictionary<string, ProfileItemViewData> Items { get; }

        public ProfileViewData(string characterName, Sprite characterIcon, IReadOnlyDictionary<string, ProfileItemViewData> items)
        {
            CharacterName = characterName;
            CharacterIcon = characterIcon;
            Items = items;
        }
    }

    /// <summary>
    /// プロフィール画面の表示を担当するビュー（解禁の判定は行わず、渡された結果を表示するだけ）
    /// </summary>
    public class ProfileUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- プロフィール表示パーツ ---")]
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private Image profileCharacterImage;

        [Header("--- プロフィール（資料）UI登録リスト ---")]
        [SerializeField] private List<ProfileUIBinding> profileUIBindings = new List<ProfileUIBinding>();

        public void RefreshProfileDisplay(ProfileViewData data)
        {
            if (data == null) return;

            if (profileNameText != null)
                profileNameText.text = data.CharacterName;

            if (profileCharacterImage != null)
            {
                profileCharacterImage.gameObject.SetActive(true);
                if (data.CharacterIcon != null)
                    profileCharacterImage.sprite = data.CharacterIcon;
            }

            foreach (var binding in profileUIBindings)
            {
                if (string.IsNullOrEmpty(binding.key)) continue;

                // データに無い項目は未解禁として扱う
                data.Items.TryGetValue(binding.key, out ProfileItemViewData item);

                if (binding.unlockButton != null)
                    binding.unlockButton.SetActive(!item.IsUnlocked);

                if (binding.contentObject != null)
                {
                    binding.contentObject.SetActive(item.IsUnlocked);
                    if (item.IsUnlocked)
                    {
                        TextMeshProUGUI contentTmp = binding.contentObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (contentTmp != null && !string.IsNullOrEmpty(item.Text))
                            contentTmp.text = item.Text;
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
        /// 引数には Unity の Inspector 上で "BASIC" や "PAST_1" などの itemKey を渡します。
        /// </summary>
        public void OnClickUnlockProfileItem(string itemKey)
        {
            if (gameUIController != null) gameUIController.TryUnlockProfileItemForViewingCharacter(itemKey);
        }

        #endregion
    }
}
