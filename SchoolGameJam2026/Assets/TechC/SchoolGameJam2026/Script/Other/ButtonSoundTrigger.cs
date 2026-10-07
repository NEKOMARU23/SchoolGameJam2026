using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TechC.SchoolGameJam2026.Scripts.Core.Event;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// クリック時に PlaySEEvent を発行して SE を鳴らすトリガー
    /// </summary>
    public class ButtonSoundTrigger : MonoBehaviour, IPointerDownHandler
    {
        [Header("--再生設定--")]
        [Tooltip("AudioManager の seClips 配列の要素番号（0, 1, 2...）")]
        [SerializeField] private int seIndex = 0;

        [Tooltip("Update() で画面全体のクリックを検知するか（UIボタン単体に付ける場合は false）")]
        [SerializeField] private bool triggerOnAnyScreenClick = false;

        private void Update()
        {
            // 画面全体検知モードの場合のみ動作
            if (triggerOnAnyScreenClick)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    PlaySE();
                }
            }
        }

        // UIボタン押下時に実行される（triggerOnAnyScreenClick が false の場合）
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!triggerOnAnyScreenClick)
            {
                PlaySE();
            }
        }

        // SE再生イベントを発火する
        private void PlaySE()
        {
            EventManager.Publish(new PlaySEEvent(seIndex));
        }
    }
}
