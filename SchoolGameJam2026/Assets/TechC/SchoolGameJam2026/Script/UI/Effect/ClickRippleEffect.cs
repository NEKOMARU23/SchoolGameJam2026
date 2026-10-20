using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// クリックした所に、丸い波紋（輪が広がって消える）を出すエフェクト
    /// Canvas（いちばん手前に出すもの）に付ける。輪は画像を使わず RingGraphic で描く
    /// クリックを邪魔しないように、輪はクリックを受け取らない
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ClickRippleEffect : MonoBehaviour
    {
        [Header("色・時間")]
        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("1つの波紋が広がって消えるまでの秒数")]
        [SerializeField, Min(0.05f)] private float duration = 0.45f;

        [Header("大きさ（UI の単位。直径）")]
        [SerializeField, Min(0f)] private float startSize = 12f;
        [SerializeField, Min(0f)] private float endSize = 110f;

        [Header("輪の太さ（広がるにつれて細くなる）")]
        [SerializeField, Min(0f)] private float startThickness = 7f;
        [SerializeField, Min(0f)] private float endThickness = 1.5f;

        [Header("押した瞬間に中心に出る、うすい丸")]
        [SerializeField] private bool useFlash = true;
        [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.3f;
        [Tooltip("丸が消えるまでの割合（波紋の時間に対して）")]
        [SerializeField, Range(0.05f, 1f)] private float flashLength = 0.4f;

        [Header("2つ目の輪（少し遅れて、小さめに広がる）")]
        [SerializeField] private bool useSecondRing = false;
        [SerializeField, Min(0f)] private float secondRingDelay = 0.07f;
        [SerializeField, Range(0.1f, 1f)] private float secondRingScale = 0.6f;

        private class Ripple
        {
            public RingGraphic Ring;
            public RingGraphic Flash;   // 中心のうすい丸（なければ null）
            public float Elapsed;
            public float Delay;
            public float Scale;
            public bool Paused;
        }

        private readonly List<Ripple> active = new List<Ripple>();
        private readonly Stack<RingGraphic> pool = new Stack<RingGraphic>();
        private RectTransform area;
        private Canvas canvas;

        /// <summary> 今出ている輪の数（テスト用） </summary>
        public int ActiveCount => active.Count;

        private void Awake()
        {
            area = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) Spawn(mouse.position.ReadValue());

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Ripple ripple = active[i];
                if (!ripple.Paused) ripple.Elapsed += Time.unscaledDeltaTime;
                if (!Apply(ripple))
                {
                    Recycle(ripple);
                    active.RemoveAt(i);
                }
            }
        }

        /// <summary> 画面のその位置に波紋を出す </summary>
        public void Spawn(Vector2 screenPosition)
        {
            Add(screenPosition, 0f, 1f, 0f, paused: false, withFlash: useFlash);
            if (useSecondRing) Add(screenPosition, secondRingDelay, secondRingScale, 0f, paused: false, withFlash: false);
        }

        /// <summary>
        /// 【見た目の確認用】進み具合（0〜1）を止めた波紋を出す。止めた波紋は ClearAll で消す
        /// </summary>
        public void SpawnPaused(Vector2 screenPosition, float progress)
        {
            float time = Mathf.Clamp01(progress) * duration;
            Add(screenPosition, 0f, 1f, time, paused: true, withFlash: useFlash);
            if (useSecondRing) Add(screenPosition, secondRingDelay, secondRingScale, time, paused: true, withFlash: false);
        }

        /// <summary> 出ている波紋をすべて消す </summary>
        public void ClearAll()
        {
            foreach (Ripple ripple in active) Recycle(ripple);
            active.Clear();
        }

        private void Add(Vector2 screenPosition, float delay, float scale, float elapsed, bool paused, bool withFlash)
        {
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, cam, out Vector2 local)) return;

            RingGraphic flash = withFlash ? TakeRing(local) : null;   // 丸は輪の後ろに出す
            RingGraphic ring = TakeRing(local);

            var ripple = new Ripple { Ring = ring, Flash = flash, Delay = delay, Scale = scale, Elapsed = elapsed, Paused = paused };
            active.Add(ripple);
            Apply(ripple);
        }

        /// <summary> 経過時間に合わせて輪の大きさ・太さ・濃さを変える。終わったら false </summary>
        private bool Apply(Ripple ripple)
        {
            float t = (ripple.Elapsed - ripple.Delay) / duration;
            if (t >= 1f) return false;

            if (t < 0f)
            {
                // 2つ目の輪の出始めを待っている間は見せない
                ripple.Ring.enabled = false;
                return true;
            }

            ripple.Ring.enabled = true;
            float grow = 1f - (1f - t) * (1f - t);   // はじめ速く、だんだんゆっくり、最後まで広がる
            float size = Mathf.Lerp(startSize, endSize, grow) * ripple.Scale;
            ripple.Ring.rectTransform.sizeDelta = new Vector2(size, size);
            ripple.Ring.Thickness = Mathf.Lerp(startThickness, endThickness, grow) * Mathf.Lerp(1f, ripple.Scale, 0.5f);

            Color c = color;
            c.a *= t < 0.3f ? 1f : Mathf.Pow(1f - (t - 0.3f) / 0.7f, 1.5f);   // しばらくはっきり見せ、後半でなめらかに消える
            ripple.Ring.color = c;

            if (ripple.Flash != null)
            {
                // 中心のうすい丸: 輪の内側をうすく塗り、すぐに消える
                float f = Mathf.Clamp01(t / flashLength);
                float flashSize = size * 0.9f;
                ripple.Flash.rectTransform.sizeDelta = new Vector2(flashSize, flashSize);
                ripple.Flash.Thickness = flashSize * 0.5f;
                Color fc = color;
                fc.a *= flashAlpha * (1f - f) * (1f - f);
                ripple.Flash.color = fc;
                ripple.Flash.enabled = f < 1f;
            }
            return true;
        }

        private RingGraphic CreateRing()
        {
            var go = new GameObject("Ripple", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            RingGraphic ring = go.AddComponent<RingGraphic>();
            ring.raycastTarget = false;
            return ring;
        }

        private RingGraphic TakeRing(Vector2 local)
        {
            RingGraphic ring = pool.Count > 0 ? pool.Pop() : CreateRing();
            ring.rectTransform.anchoredPosition = local;
            ring.gameObject.SetActive(true);
            ring.rectTransform.SetAsLastSibling();
            return ring;
        }

        private void Recycle(Ripple ripple)
        {
            Recycle(ripple.Ring);
            Recycle(ripple.Flash);
        }

        private void Recycle(RingGraphic ring)
        {
            if (ring == null) return;
            ring.gameObject.SetActive(false);
            pool.Push(ring);
        }
    }
}
