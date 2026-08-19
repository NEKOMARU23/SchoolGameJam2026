using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core;


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
        /// IDごとの解放状態を受け取り、ボタンの活性/非活性およびオーバーレイを表示（描画専用）
        /// </summary>
        public void UpdateRoomStatus(Dictionary<string, bool> releaseStatusMap)
        {
            foreach (var binding in monitorRoomBindings)
            {
                // 辞書から対象キャラの解放状態を取得（見つからなければ false）
                bool isReleased = releaseStatusMap != null 
                    && releaseStatusMap.TryGetValue(binding.characterId, out bool released) 
                    && released;

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

        /// <summary>
        /// 部屋ボタン押下時のイベント通知
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