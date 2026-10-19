#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 画面の比率を 16:9 に保つ黒い帯（LetterboxManager）のテスト
    /// </summary>
    public class LetterboxTest
    {
        private static void AssertRect(Rect expected, Rect actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f, message + "（x）");
            Assert.AreEqual(expected.y, actual.y, 0.0001f, message + "（y）");
            Assert.AreEqual(expected.width, actual.width, 0.0001f, message + "（幅）");
            Assert.AreEqual(expected.height, actual.height, 0.0001f, message + "（高さ）");
        }

        [Test]
        public void 画面が16対9なら帯は出ない()
        {
            AssertRect(new Rect(0f, 0f, 1f, 1f), Core.Manager.LetterboxManager.CalculateViewport(1920, 1080), "1920×1080");
            AssertRect(new Rect(0f, 0f, 1f, 1f), Core.Manager.LetterboxManager.CalculateViewport(1280, 720), "1280×720");
        }

        [Test]
        public void 縦に長い画面では上下に帯が出る()
        {
            // 1920×1200（16:10）: 16:9 の高さは 1080 なので、上下に 60 ずつ
            AssertRect(new Rect(0f, 0.05f, 1f, 0.9f), Core.Manager.LetterboxManager.CalculateViewport(1920, 1200), "1920×1200");
            // 1024×768（4:3）: 16:9 の高さは 576 なので、上下に 96 ずつ
            AssertRect(new Rect(0f, 0.125f, 1f, 0.75f), Core.Manager.LetterboxManager.CalculateViewport(1024, 768), "1024×768");
        }

        [Test]
        public void 横に長い画面では左右に帯が出る()
        {
            // 2560×1080（21:9）: 16:9 の幅は 1920 なので、左右に 320 ずつ
            AssertRect(new Rect(0.125f, 0f, 0.75f, 1f), Core.Manager.LetterboxManager.CalculateViewport(2560, 1080), "2560×1080");
        }

        [UnityTest]
        public IEnumerator 画面のUIとカメラが16対9の範囲に映る()
        {
            yield return LoadInGameScene();
            yield return null;

            var letterbox = Core.Manager.LetterboxManager.Instance;
            Assert.IsNotNull(letterbox, "LetterboxManager が起動時に作られていません");
            Rect expected = Core.Manager.LetterboxManager.CalculateViewport(Screen.width, Screen.height);

            // 画面の UI（本編とオプション）が、UI 用のカメラに映る形になっている
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode, $"{canvas.name} が画面全体に広がる形のままです");
                Assert.AreSame(letterbox.UICamera, canvas.worldCamera, $"{canvas.name} が UI 用のカメラに映っていません");
                Assert.AreEqual(LayerMask.NameToLayer("UI"), canvas.gameObject.layer, $"{canvas.name} が UI のレイヤーではないので映りません");
            }

            // シーンのカメラに UI 用のカメラが重なり、どちらも 16:9 の範囲を映す
            Camera main = Camera.main;
            Assert.IsNotNull(main, "前提: 本編のカメラがある");
            CollectionAssert.Contains(main.GetUniversalAdditionalCameraData().cameraStack, letterbox.UICamera, "UI 用のカメラが本編のカメラに重なっていません");
            AssertRect(expected, main.rect, "本編のカメラの範囲");
            AssertRect(expected, letterbox.UICamera.rect, "UI 用のカメラの範囲");
        }

        [UnityTest]
        public IEnumerator UIのボタンは映った位置で押せる()
        {
            yield return LoadInGameScene();
            yield return null;

            // ボタンの真ん中の画面上の位置で UI を調べると、そのボタンに当たる（押す位置と映る位置がずれていない）
            var button = FindButton("SelectRoomA_Button");
            var rect = (RectTransform)button.transform;
            Camera camera = button.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));

            Assert.IsTrue(camera.pixelRect.Contains(screen), "ボタンが 16:9 の範囲の外に映っています");
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            eventSystem.RaycastAll(new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = screen }, results);
            Assert.IsNotEmpty(results, "ボタンの位置を押しても、何にも当たりません");
            Assert.IsTrue(results[0].gameObject.transform.IsChildOf(button.transform), $"ボタンの位置を押すと、ほかのもの（{results[0].gameObject.name}）に当たります");
        }
    }
}
#endif
