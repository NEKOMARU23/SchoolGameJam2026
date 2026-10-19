using System;
using System.Collections.Generic;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// どの画面のボタンでも、押したときに効果音を鳴らす（Manager シーンの AudioManager に付ける）
    /// ボタン1つ1つに部品を付けなくても、あとから作ったボタン（選択肢・セーブ枠など）でも鳴る
    /// 押せないボタンや、ボタン以外（会話枠など）を押したときは鳴らさない
    /// 音は Click Clip に入れる（空なら鳴らさない）。音量はオプションの SE 音量に従う
    /// </summary>
    public class ButtonClickSound : MonoBehaviour
    {
        [Tooltip("ボタンを押したときに鳴らす音（空なら鳴らさない）")]
        [SerializeField] private AudioClip clickClip;

        /// <summary> 音を鳴らすボタンが押されたときに通知（音が空でも通知する。テスト用） </summary>
        public event Action<Button> OnButtonPressed;

        private static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Button button = FindButtonAt(mouse.position.ReadValue());
            if (button == null) return;

            // 自分の音を持つボタン（ButtonSoundTrigger）は、そちらで鳴らす
            if (button.GetComponent<ButtonSoundTrigger>() != null) return;

            OnButtonPressed?.Invoke(button);
            if (clickClip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySE(clickClip);
        }

        /// <summary>
        /// 画面のその位置で、一番手前にある押せるボタンを返す（なければ null）
        /// ボタンの上の文字や画像に当たっても、それを持つボタンを見つける
        /// </summary>
        public static Button FindButtonAt(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return null;

            raycastResults.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, raycastResults);
            if (raycastResults.Count == 0) return null;

            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(raycastResults[0].gameObject);
            if (handler == null) return null;

            Button button = handler.GetComponent<Button>();
            return button != null && button.IsInteractable() ? button : null;
        }
    }
}
