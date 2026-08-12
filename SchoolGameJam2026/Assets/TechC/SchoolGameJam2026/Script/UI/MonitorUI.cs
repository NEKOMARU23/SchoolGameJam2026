using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    [Serializable]
    public struct MonitorRoomBinding
    {
        public string characterId;
        public Button roomButton;
        public GameObject releasedOverlay;
    }

    public class MonitorUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- モニター画面の部屋ボタン登録 ---")]
        [SerializeField] private List<MonitorRoomBinding> monitorRoomBindings = new List<MonitorRoomBinding>();

        private void Awake()
        {
            // 各部屋ボタンの OnClick イベントを自動バインド
            foreach (var binding in monitorRoomBindings)
            {
                if (binding.roomButton != null)
                {
                    string charId = binding.characterId;
                    binding.roomButton.onClick.AddListener(() => OnClickRoomButton(charId));
                }
            }
        }

        /// <summary>
        /// キャラクターの解放状態に合わせてモニターボタンの活性/非活性およびオーバーレイを更新
        /// </summary>
        public void RefreshMonitorButtons(List<CharacterInstance> allCharacters)
        {
            if (allCharacters == null || allCharacters.Count == 0)
            {
                foreach (var binding in monitorRoomBindings)
                {
                    if (binding.roomButton != null) binding.roomButton.interactable = true;
                    if (binding.releasedOverlay != null) binding.releasedOverlay.SetActive(false);
                }
                return;
            }

            foreach (var binding in monitorRoomBindings)
            {
                CharacterInstance charInst = allCharacters.Find(c => c.CharacterId == binding.characterId);

                if (charInst != null)
                {
                    bool isReleased = charInst.IsReleased;

                    if (binding.roomButton != null)
                    {
                        binding.roomButton.interactable = !isReleased;
                    }

                    if (binding.releasedOverlay != null)
                    {
                        binding.releasedOverlay.SetActive(isReleased);
                    }
                }
            }
        }

        /// <summary>
        /// 部屋ボタン押下時の処理
        /// </summary>
        private void OnClickRoomButton(string characterId)
        {
            if (gameUIController != null)
            {
                gameUIController.SelectCharacter(characterId);
            }
        }
    }
}