#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.UI;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// クリックした所に丸い波紋を出すエフェクト（ClickRippleEffect）のテスト
    /// テスト用の Canvas とエフェクトを作り、仮のマウスで押したことにする
    /// </summary>
    public class ClickRippleEffectTest
    {
        private Mouse mouse;
        private GameObject canvasObject;
        private ClickRippleEffect effect;

        [SetUp]
        public void SetUp()
        {
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();

            canvasObject = new GameObject("TestRippleCanvas", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var go = new GameObject("ClickRipple", typeof(RectTransform));
            go.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            effect = go.AddComponent<ClickRippleEffect>();
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null) Object.Destroy(canvasObject);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
        }

        private IEnumerator Press(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }

        [UnityTest]
        public IEnumerator クリックすると押した所に波紋が出てしばらくすると消える()
        {
            Vector2 position = new Vector2(Screen.width * 0.3f, Screen.height * 0.6f);
            yield return Press(position);

            Assert.Greater(effect.ActiveCount, 0, "クリックしても波紋が出ません");
            RingGraphic ring = effect.GetComponentInChildren<RingGraphic>();
            Assert.IsNotNull(ring);
            Assert.IsFalse(ring.raycastTarget, "波紋がクリックをさえぎってしまいます");

            // 輪の真ん中が、押した所にある（黒帯の仕組みで Canvas は UI 用のカメラに映る形になっているので、そのカメラで位置を求める）
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, ring.rectTransform.position);
            Assert.AreEqual(position.x, screen.x, 1f, "波紋が押した所に出ていません（横）");
            Assert.AreEqual(position.y, screen.y, 1f, "波紋が押した所に出ていません（縦）");

            // 広がりながら消える
            float startSize = ring.rectTransform.sizeDelta.x;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Greater(ring.rectTransform.sizeDelta.x, startSize, "波紋が広がっていません");

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0, effect.ActiveCount, "時間がたっても波紋が消えません");
        }

        [UnityTest]
        public IEnumerator 続けてクリックするとそれぞれに波紋が出る()
        {
            yield return Press(new Vector2(Screen.width * 0.2f, Screen.height * 0.5f));
            int first = effect.ActiveCount;
            yield return Press(new Vector2(Screen.width * 0.7f, Screen.height * 0.5f));
            Assert.Greater(effect.ActiveCount, first, "2回目のクリックで波紋が増えていません");
        }
    }
}
#endif
