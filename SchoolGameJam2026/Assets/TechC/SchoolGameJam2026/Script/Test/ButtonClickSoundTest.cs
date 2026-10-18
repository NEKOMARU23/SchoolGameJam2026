#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// ボタンを押したときに効果音を鳴らす仕組み（ButtonClickSound）のテスト
    /// テスト用の仮のマウスをつないで、ボタンの上で押したことにする
    /// </summary>
    public class ButtonClickSoundTest
    {
        private Mouse mouse;
        private ButtonClickSound clickSound;
        private readonly List<Button> pressed = new List<Button>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            clickSound = FindComponent<ButtonClickSound>();
            pressed.Clear();
            clickSound.OnButtonPressed += OnPressed;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (clickSound != null) clickSound.OnButtonPressed -= OnPressed;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            mouse = null;
        }

        private void OnPressed(Button button) => pressed.Add(button);

        /// <summary> ボタンの真ん中の、画面上の位置 </summary>
        private static Vector2 ScreenCenterOf(Button button)
        {
            var rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }

        /// <summary> その位置でマウスの左ボタンを押し、離さずに画面の外へ動かしてから離す（ボタンの処理は動かさない） </summary>
        private IEnumerator PressAt(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-100f, -100f) }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-100f, -100f) });
            yield return null;
        }

        [Test]
        public void 部品がManagerシーンのAudioManagerに付いている()
        {
            Assert.IsNotNull(clickSound.GetComponent<Core.Manager.AudioManager>(), "ButtonClickSound が AudioManager と同じオブジェクトに付いていません");
        }

        [Test]
        public void ボタンの位置からボタンが見つかる()
        {
            Button button = FindButton("SelectRoomA_Button");
            Assert.AreSame(button, ButtonClickSound.FindButtonAt(ScreenCenterOf(button)), "ボタンの真ん中を押しても、そのボタンが見つかりません");
        }

        [UnityTest]
        public IEnumerator ボタンを押すと音を鳴らす()
        {
            Button button = FindButton("SelectRoomA_Button");
            yield return PressAt(ScreenCenterOf(button));

            Assert.AreEqual(1, pressed.Count, "ボタンを押しても音を鳴らす処理が動きません");
            Assert.AreSame(button, pressed[0]);
        }

        [UnityTest]
        public IEnumerator 押せないボタンでは鳴らさない()
        {
            Button button = FindButton("SelectRoomA_Button");
            button.interactable = false;
            yield return PressAt(ScreenCenterOf(button));

            Assert.AreEqual(0, pressed.Count, "押せないボタンで音を鳴らしました");
        }

        [UnityTest]
        public IEnumerator ボタンがない所では鳴らさない()
        {
            yield return PressAt(new Vector2(-100f, -100f));
            Assert.AreEqual(0, pressed.Count, "ボタンがない所で音を鳴らしました");
        }
    }
}
#endif
