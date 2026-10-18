using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// マウスを乗せると、少し持ち上げてふちを光らせ、名前の札を出す（机の上のスマホ・ノート用。描画専用）
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HoverHighlightUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("--- 持ち上げる高さ ---")]
        [SerializeField] private float liftAmount = 12f;

        [Header("--- ふちの光（後ろに置いた半透明の四角。乗せている間だけ表示し、一緒に持ち上げる） ---")]
        [SerializeField] private RectTransform glow;

        [Header("--- 名前の札（乗せている間だけ表示する） ---")]
        [SerializeField] private GameObject label;

        [Header("--- 押せないときは反応しない ---")]
        [SerializeField] private Selectable selectable;

        private RectTransform rectTransform;
        private Graphic raycastGraphic;
        private Vector2 basePosition;
        private Vector2 glowBasePosition;
        private Vector4 baseRaycastPadding;
        private bool isHovered;

        public bool IsHovered => isHovered;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            raycastGraphic = GetComponent<Graphic>();
            SetVisual(false);
        }

        private void OnDisable()
        {
            // 画面を切り替えたときに、持ち上がったまま残らないようにする
            Restore();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isHovered) return;
            if (selectable != null && !selectable.IsInteractable()) return;

            isHovered = true;
            basePosition = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = basePosition + Vector2.up * liftAmount;
            if (glow != null)
            {
                glowBasePosition = glow.anchoredPosition;
                glow.anchoredPosition = glowBasePosition + Vector2.up * liftAmount;
            }

            // 持ち上げた分だけ当たり判定を下に広げ、元の位置にあるマウスから外れないようにする
            if (raycastGraphic != null)
            {
                baseRaycastPadding = raycastGraphic.raycastPadding;
                float scale = Mathf.Approximately(rectTransform.localScale.y, 0f) ? 1f : rectTransform.localScale.y;
                raycastGraphic.raycastPadding = baseRaycastPadding - new Vector4(0f, liftAmount / scale, 0f, 0f);
            }
            SetVisual(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Restore();
        }

        private void Restore()
        {
            if (!isHovered) return;

            isHovered = false;
            rectTransform.anchoredPosition = basePosition;
            if (glow != null) glow.anchoredPosition = glowBasePosition;
            if (raycastGraphic != null) raycastGraphic.raycastPadding = baseRaycastPadding;
            SetVisual(false);
        }

        private void SetVisual(bool on)
        {
            if (glow != null) glow.gameObject.SetActive(on);
            if (label != null) label.SetActive(on);
        }
    }
}
