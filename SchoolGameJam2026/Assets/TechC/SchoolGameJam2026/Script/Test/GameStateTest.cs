#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// フラグと、ゲームの状態をまとめて扱う仕組み（スナップショット）のテスト
    /// </summary>
    public class GameStateTest
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
        }

        [Test]
        public void フラグのONとOFFと全部ON全部OFFの判定()
        {
            var flags = new FlagStore();
            Assert.IsFalse(flags.IsOn("item_鍵"));

            Assert.IsTrue(flags.TurnOn("item_鍵"), "OFF から ON に変わったら true");
            Assert.IsFalse(flags.TurnOn("item_鍵"), "すでに ON なら false（同じものを2回手に入れない）");
            flags.TurnOn("info_アリバイ");

            Assert.IsTrue(flags.AreAllOn(FlagStore.ParseList("item_鍵, info_アリバイ")));
            Assert.IsFalse(flags.AreAllOn(FlagStore.ParseList("item_鍵, info_日記")), "1つでも OFF なら不成立");
            Assert.IsTrue(flags.AreAllOff(FlagStore.ParseList("info_日記, flag_和解")));
            Assert.IsFalse(flags.AreAllOff(FlagStore.ParseList("info_日記, item_鍵")), "1つでも ON なら不成立");
            Assert.IsTrue(flags.AreAllOn(FlagStore.ParseList("")), "空欄は条件なし（成立）");

            Assert.IsTrue(flags.TurnOff("item_鍵"));
            Assert.IsFalse(flags.IsOn("item_鍵"));
        }

        [Test]
        public void カンマ区切りの書き方を読み取れる()
        {
            CollectionAssert.AreEqual(new[] { "item_鍵", "info_アリバイ", "flag_和解" },
                FlagStore.ParseList(" item_鍵 ,info_アリバイ、flag_和解 "));
            CollectionAssert.IsEmpty(FlagStore.ParseList("  "));
        }

        [UnityTest]
        public IEnumerator 今日をやり直すとその日に手に入れたフラグだけ戻る()
        {
            FlagStore flags = GameManager.Flags;

            // 1日目に手に入れたフラグは、翌日以降も残る
            flags.TurnOn("info_1日目の情報");
            yield return EndDayAndAdvance();
            Assert.AreEqual(2, GameManager.CurrentDay);

            // 2日目に手に入れたフラグは、2日目をやり直すと消える
            flags.TurnOn("item_2日目の鍵");
            FindComponent<GameUIController>().OnConfirmResetDay();
            yield return null;

            Assert.IsTrue(flags.IsOn("info_1日目の情報"), "前の日までに手に入れたフラグまで消えています");
            Assert.IsFalse(flags.IsOn("item_2日目の鍵"), "その日に手に入れたフラグが戻っていません");
        }

        [UnityTest]
        public IEnumerator 状態を書き出して読み戻すと元どおりになる()
        {
            // 状態をいろいろ変える
            var controller = FindComponent<GameUIController>();
            InterrogationManager.SetPoints(123);
            controller.SelectCharacter("A");
            yield return null;
            controller.ExecuteInterrogationAction((int)InterrogationActionType.Talk);
            yield return SkipDialogue();
            CharacterInstance b = GetCharacter("B");
            b.SetAffection(5);
            b.SetBasicInfoUnlocked(true);
            b.UnlockHistoryItem("PAST_2");
            GameManager.Flags.TurnOn("item_鍵");

            // セーブファイルと同じく JSON にして読み戻す
            string json = JsonUtility.ToJson(GameManager.CaptureState());
            int points = InterrogationManager.CurrentPoints;
            int actions = InterrogationManager.CurrentDailyActions;
            int talkCount = InterrogationManager.GetCharacterActionCount("A", InterrogationActionType.Talk);

            // 状態を壊してから戻す
            InterrogationManager.SetPoints(0);
            InterrogationManager.ResetDailyActions(0);
            b.SetAffection(0);
            b.SetBasicInfoUnlocked(false);
            GameManager.Flags.TurnOff("item_鍵");

            GameManager.RestoreState(JsonUtility.FromJson<GameStateSnapshot>(json));
            yield return null;

            Assert.AreEqual(points, InterrogationManager.CurrentPoints, "ポイントが戻っていません");
            Assert.AreEqual(actions, InterrogationManager.CurrentDailyActions, "行動回数が戻っていません");
            Assert.IsTrue(InterrogationManager.HasInterrogatedToday("A"), "取り調べ済みの記録が戻っていません");
            Assert.AreEqual(talkCount, InterrogationManager.GetCharacterActionCount("A", InterrogationActionType.Talk), "通算回数が戻っていません");
            Assert.AreEqual(5, b.CurrentAffection, "好感度が戻っていません");
            Assert.IsTrue(b.IsBasicInfoUnlocked, "プロフィールの解禁が戻っていません");
            Assert.IsTrue(b.IsHistoryUnlocked("PAST_2"), "過去情報の解禁が戻っていません");
            Assert.IsTrue(GameManager.Flags.IsOn("item_鍵"), "フラグが戻っていません");
        }

        [UnityTest]
        public IEnumerator 翌日の朝の状態ではスマホの会話回数が0回になっている()
        {
            Assert.Greater(GameManager.Scenario.GetSmartphoneTalkCount(1), 0, "前提: 1日目のスマホのセリフがある");

            yield return Click("Button_Help", "MonitorPanel");
            yield return Click("CloseButton", "SmartphoneTalkPanel");
            Assert.AreEqual(1, Core.Manager.SmartphoneTalkManager.Instance.TalkCountToday);

            yield return EndDayAndAdvance();

            Assert.AreEqual(0, Core.Manager.SmartphoneTalkManager.Instance.TalkCountToday, "翌日に会話回数が0に戻っていません");
            Assert.AreEqual(0, GameManager.MorningSnapshot.smartphoneTalkCount, "翌日の朝の状態に前日の会話回数が残っています");
        }

        [UnityTest]
        public IEnumerator デバッグ用に指定した日の朝へ移動できる()
        {
            var controller = FindComponent<GameUIController>();
            controller.SelectCharacter("A");
            yield return null;
            controller.ExecuteInterrogationAction((int)InterrogationActionType.Talk);
            yield return SkipDialogue();
            GameManager.Flags.TurnOn("info_移動前の情報");

            Assert.IsTrue(GameManager.JumpToDay(5));
            yield return null;

            Assert.AreEqual(5, GameManager.CurrentDay);
            Assert.AreEqual(0, InterrogationManager.CurrentDailyActions, "移動先の日の行動回数が 0 になっていません");
            Assert.AreEqual(5, GameManager.MorningSnapshot.day, "移動先の日の朝の状態が記録されていません");
            Assert.IsTrue(GameManager.Flags.IsOn("info_移動前の情報"), "移動前のフラグが消えています");
            Assert.IsFalse(GameManager.JumpToDay(99), "存在しない日には移動できない");
        }

        [UnityTest]
        public IEnumerator 状態をまとめて戻すと画面も描き直される()
        {
            Assert.IsTrue(FindButton("SelectRoomB_Button").interactable, "前提: B の部屋に入れる");

            // 画面に知らせずに状態だけ変えてから、まとめて戻す（デバッグメニュー・ロードと同じ流れ）
            GetCharacter("B").SetReleaseFlag(true);
            GameManager.RestoreState(GameManager.CaptureState());
            yield return null;

            Assert.IsFalse(FindButton("SelectRoomB_Button").interactable, "解放したのにモニター画面が描き直されていません");
        }

        [Test]
        public void デバッグメニューが起動時に用意されている()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<DebugTools.DebugMenu>(), "デバッグメニューが自動で作られていません");
            Assert.IsFalse(DebugTools.DebugMenu.IsOpen, "最初から開いています");
        }

        private static IEnumerator EndDayAndAdvance()
        {
            var controller = FindComponent<GameUIController>();
            controller.OnConfirmEndDay();
            yield return null;
            controller.OnClickCloseResultAndAdvanceDay();
            yield return null;
        }
    }
}
#endif
