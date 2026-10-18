#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// キーボード操作（Enter・Space / Ctrl / A / L / Esc）のテスト
    /// テスト用の仮のキーボードをつないで、キーを押したことにする
    /// </summary>
    public class KeyboardTest
    {
        private Keyboard keyboard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnterとSpaceで取調室の会話が進む()
        {
            var dialogueUI = FindComponent<DialogueUI>();
            yield return Click("SelectRoomA_Button");
            yield return Click("Button_Talk");
            Assert.IsTrue(dialogueUI.IsDialoguePlaying, "前提: 会話が始まる");
            int actions = InterrogationManager.CurrentDailyActions;

            for (int i = 0; i < 60 && dialogueUI.IsDialoguePlaying; i++)
            {
                yield return Press(i % 2 == 0 ? Key.Enter : Key.Space);
            }

            Assert.IsFalse(dialogueUI.IsDialoguePlaying, "Enter・Space を押しても会話が最後まで進みません");
            Assert.AreEqual(actions, InterrogationManager.CurrentDailyActions, "Enter・Space で行動のボタンがもう一度押されました");
        }

        [UnityTest]
        public IEnumerator Enterでスマホの会話が進んで閉じる()
        {
            yield return Click("Button_Help", "MonitorPanel");
            var talk = FindComponent<UI.Dialogue.DialogueClickable>();
            Assert.IsTrue(talk.IsOpen, "前提: スマホの会話が開く");

            for (int i = 0; i < 60 && talk.IsOpen; i++)
            {
                yield return Press(Key.Enter);
            }
            Assert.IsFalse(talk.IsOpen, "Enter を押してもスマホの会話が閉じません");
        }

        [UnityTest]
        public IEnumerator Ctrlを押している間だけスキップになる()
        {
            var controller = FindComponent<GameUIController>();
            Assert.IsFalse(controller.IsSkipOn, "前提: スキップはオフ");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return null;
            yield return null;
            Assert.IsTrue(controller.IsSkipOn, "Ctrl を押してもスキップになりません");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Assert.IsFalse(controller.IsSkipOn, "Ctrl を離してもスキップが止まりません");
        }

        [UnityTest]
        public IEnumerator Aでオートが切り替わる()
        {
            var controller = FindComponent<GameUIController>();
            bool before = controller.IsAutoOn;

            yield return Press(Key.A);
            Assert.AreEqual(!before, controller.IsAutoOn, "A を押してもオートが切り替わりません");

            yield return Press(Key.A);
            Assert.AreEqual(before, controller.IsAutoOn, "もう一度 A を押しても元に戻りません");
        }

        [UnityTest]
        public IEnumerator Lでログが開いてもう一度押すと閉じる()
        {
            var log = FindComponent<LogUI>();
            Assert.IsFalse(log.gameObject.activeInHierarchy, "前提: ログは閉じている");

            yield return Press(Key.L);
            Assert.IsTrue(log.gameObject.activeInHierarchy, "L を押してもログが開きません");

            yield return Press(Key.L);
            Assert.IsFalse(log.gameObject.activeInHierarchy, "もう一度 L を押してもログが閉じません");
        }

        [UnityTest]
        public IEnumerator Escでログが閉じてオプションは開かない()
        {
            Assume.That(OptionUI.Instance != null, "前提: オプション画面（Manager シーン）がある");
            var log = FindComponent<LogUI>();

            yield return Press(Key.L);
            Assert.IsTrue(log.gameObject.activeInHierarchy, "前提: ログが開く");

            yield return Press(Key.Escape);
            Assert.IsFalse(log.gameObject.activeInHierarchy, "Esc を押してもログが閉じません");
            Assert.IsFalse(OptionUI.IsOpen, "ログを閉じる Esc で、オプション画面まで開きました");
        }

        [UnityTest]
        public IEnumerator オプション画面が開いている間はキーが効かない()
        {
            Assume.That(OptionUI.Instance != null, "前提: オプション画面（Manager シーン）がある");
            var controller = FindComponent<GameUIController>();
            bool before = controller.IsAutoOn;

            OptionUI.Instance.Open();
            yield return null;
            yield return Press(Key.A);
            Assert.AreEqual(before, controller.IsAutoOn, "オプション画面が開いているのに A でオートが切り替わりました");
            OptionUI.Instance.Close();
        }

        [UnityTest]
        public IEnumerator クリックしたボタンの選択は外れる()
        {
            Button(out var button);
            EventSystem.current.SetSelectedGameObject(button);
            yield return null;
            Assert.IsNull(EventSystem.current.currentSelectedGameObject, "ボタンが選ばれたままです（Enter・Space でもう一度押されてしまう）");
        }

        private static void Button(out GameObject button)
        {
            Assert.IsNotNull(EventSystem.current, "前提: EventSystem がある");
            button = FindButton("Day_Button", "MonitorPanel").gameObject;
        }
    }
}
#endif
