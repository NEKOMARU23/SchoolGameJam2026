using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TechC.SchoolGameJam2026.Core.Event;

namespace TechC.SchoolGameJam2026.Script.UI
{
    public struct PlaySEEvent
    {
        ///<summary>
        ///再生したいSEのインデックス番号
        /// </summary>
        public int SeIndex { get; set; }

        public PlaySEEvent(int seIndex)
        {
            SeIndex = seIndex;
        }
    }
    public class ButtonSoundTrigger : MonoBehaviour, IPointerDownHandler
    {
        [Header("--再生設定--")]
        [Tooltip("AudioManager の seClips 配列の要素番号（0, 1, 2...）")]
        [SerializeField] private int seIndex = 0;

        [Tooltip("Update() による画面全体のクリック検知を行うか（UIボタン単体に付ける場合は false）")]
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

        // UIボタン押し込み時に実行される（triggerOnAnyScreenClick が false の場合）
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!triggerOnAnyScreenClick)
            {
                PlaySE();
            }
        }

        // SE再生イベントを発火するメソッド
        private void PlaySE()
        {
            EventManager.Publish(new PlaySEEvent(seIndex));
        }
    }
}