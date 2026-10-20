using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 画像を使わずに、輪（リング）の形を描く UI の部品
    /// RectTransform の大きさが輪の外側の直径になる。縁は少しぼかして、なめらかに見せる
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RingGraphic : MaskableGraphic
    {
        [Tooltip("輪の太さ（UI の単位）")]
        [SerializeField, Min(0f)] private float thickness = 4f;

        [Tooltip("縁をぼかす幅（UI の単位）")]
        [SerializeField, Min(0f)] private float feather = 1.5f;

        [Tooltip("円を何角形で描くか（多いほどなめらか）")]
        [SerializeField, Range(8, 256)] private int segments = 96;

        public float Thickness
        {
            get => thickness;
            set { if (!Mathf.Approximately(thickness, value)) { thickness = Mathf.Max(0f, value); SetVerticesDirty(); } }
        }

        public float Feather
        {
            get => feather;
            set { if (!Mathf.Approximately(feather, value)) { feather = Mathf.Max(0f, value); SetVerticesDirty(); } }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = Mathf.Max(0f, outer - thickness);
            if (outer <= 0f) return;

            // 内側から外側へ 4 つの半径: ぼかし始め（透明）・輪の内側・輪の外側・ぼかし終わり（透明）
            float[] radii = { Mathf.Max(0f, inner - feather), inner, outer, outer + feather };
            float[] alphas = { 0f, 1f, 1f, 0f };

            Color32 baseColor = color;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (int r = 0; r < radii.Length; r++)
                {
                    Color32 c = baseColor;
                    c.a = (byte)(baseColor.a * alphas[r]);
                    vh.AddVert(center + dir * radii[r], c, Vector2.zero);
                }
            }

            // 隣り合う 2 本の線のあいだを、3 つの帯（ぼかし・輪・ぼかし）の四角でつなぐ
            int stride = radii.Length;
            for (int i = 0; i < segments; i++)
            {
                int a = i * stride;
                int b = (i + 1) * stride;
                for (int r = 0; r < stride - 1; r++)
                {
                    vh.AddTriangle(a + r, a + r + 1, b + r + 1);
                    vh.AddTriangle(a + r, b + r + 1, b + r);
                }
            }
        }
    }
}
