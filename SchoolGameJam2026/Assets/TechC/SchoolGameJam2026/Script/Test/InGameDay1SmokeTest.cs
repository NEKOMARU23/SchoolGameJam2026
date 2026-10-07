#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// Day1 で画面上のボタンを一通り押し、配線ミスやエラーが出ないかを確認する
    /// （実際のボタンの onClick を、表示中・押下可能な状態でのみ押す）
    /// </summary>
    public class InGameDay1SmokeTest
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
        }

        [UnityTest]
        public IEnumerator 初期状態はDay1でモニター画面が表示されている()
        {
            Assert.AreEqual(1, GameManager.CurrentDay);
            Assert.AreEqual(0, InterrogationManager.CurrentDailyActions);
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "モニター画面が表示されていません");
            Assert.IsFalse(IsPanelActive("InterrogationPanel"), "取調室が最初から表示されています");
            yield break;
        }

        [UnityTest]
        public IEnumerator 四人全員に別々のアクションを実行できる()
        {
            var controller = FindComponent<GameUIController>();

            // A: 対話
            yield return Click("SelectRoomA_Button");
            Assert.IsTrue(IsPanelActive("InterrogationPanel"), "部屋ボタンで取調室が開きません");
            Assert.AreEqual("A", controller.CurrentSelectedCharacterId);

            yield return Click("Button_Talk");
            Assert.AreEqual(1, InterrogationManager.CurrentDailyActions, "対話で行動回数が増えていません");
            Assert.IsTrue(InterrogationManager.HasInterrogatedToday("A"));
            yield return SkipDialogueByButton();

            // 同じキャラへの2回目は押せない
            Assert.IsFalse(FindButton("Button_Talk").interactable, "取り調べ済みのキャラにもう一度アクションできてしまいます");

            // B: 拷問
            yield return Click("Button_ToMonitor");
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "戻るボタンでモニターに戻りません");
            yield return Click("SelectRoomB_Button");
            yield return Click("Button_Torture");
            yield return SkipDialogueByButton();

            // C: 交渉
            yield return Click("Button_ToMonitor");
            yield return Click("SelectRoomC_Button");
            yield return Click("Button_Negotiate");
            yield return SkipDialogueByButton();

            // D: 聞き取り（A について）
            yield return Click("Button_ToMonitor");
            yield return Click("SelectRoomD_Button");
            yield return Click("Button_HearAbout");
            Assert.IsTrue(IsPanelActive("HearingPanel"), "聞き取りパネルが開きません");
            Assert.IsFalse(FindButton("Button_D", "CharacterButtonContainer").gameObject.activeSelf, "取り調べ中の本人(D)の聞き取りボタンが表示されています");

            yield return Click("Button_A", "CharacterButtonContainer");
            Assert.IsFalse(IsPanelActive("HearingPanel"), "聞き取り対象を選んでもパネルが閉じません");
            Assert.IsTrue(InterrogationManager.HasInterrogatedToday("D"), "聞き取りが実行されていません");
            yield return SkipDialogueByButton();

            // 行動回数の上限
            Assert.AreEqual(4, InterrogationManager.CurrentDailyActions);
            Assert.IsTrue(InterrogationManager.IsActionLimitReached);
            Assert.GreaterOrEqual(InterrogationManager.CurrentPoints, 0);
        }

        [UnityTest]
        public IEnumerator 会話はページ送りされログに記録される()
        {
            yield return Click("SelectRoomA_Button");
            yield return Click("Button_Talk");

            var dialogueUI = FindComponent<DialogueUI>();
            Assert.IsTrue(dialogueUI.IsDialoguePlaying, "アクション後に会話が始まっていません");

            yield return SkipDialogueByButton();

            // ログ画面を開くと会話が記録されている
            yield return Click("Button_Log", "Interrogation_Chatbox");
            Assert.IsTrue(IsPanelActive("Log Panel"), "ログ画面が開きません");

            int logCount = Object.FindObjectsByType<LogItemUI>(FindObjectsInactive.Exclude).Length;
            Assert.Greater(logCount, 0, "ログに会話が1件も記録されていません");

            yield return Click("Button_Back", "Log Panel");
            Assert.IsFalse(IsPanelActive("Log Panel"), "ログ画面が閉じません");
        }

        [UnityTest]
        public IEnumerator プロフィールをポイントで解禁できる()
        {
            InterrogationManager.SetPoints(1000);

            yield return Click("Profeofile_Button", "MonitorPanel");
            Assert.IsTrue(IsPanelActive("ProfilePanel"), "プロフィール画面が開きません");

            yield return Click("ACharacterChange_Button");
            CharacterInstance characterA = GetCharacter("A");

            // 基礎情報
            int pointsBefore = InterrogationManager.CurrentPoints;
            yield return Click("Button_UnlockBasicInfo");
            Assert.IsTrue(characterA.IsBasicInfoUnlocked, "基礎情報が解禁されていません");
            Assert.AreEqual(pointsBefore - characterA.ProfileData.basicInfo.unlockCost, InterrogationManager.CurrentPoints, "解禁コスト分のポイントが減っていません");
            Assert.IsFalse(FindButton("Button_UnlockBasicInfo").gameObject.activeSelf, "解禁後も解禁ボタンが表示されたままです");

            // 過去情報（パネルに置き場所がある項目のみ表示しているため、表示中の項目だけ確認する）
            int checkedItemCount = 0;
            for (int i = 1; i <= 3; i++)
            {
                if (!FindButton($"Button_UnlockItem_{i}").transform.parent.gameObject.activeInHierarchy) continue;

                checkedItemCount++;
                yield return Click($"Button_UnlockItem_{i}");
                Assert.IsTrue(characterA.IsHistoryUnlocked($"PAST_{i}"), $"PAST_{i} が解禁されていません");
            }
            Assert.Greater(checkedItemCount, 0, "表示中の過去情報の解禁ボタンが1つもありません");

            // 解禁状態はキャラごとに独立している
            yield return Click("BCharacterChange_Button");
            Assert.IsFalse(GetCharacter("B").IsBasicInfoUnlocked, "A の解禁が B にも反映されています");
            Assert.IsTrue(FindButton("Button_UnlockBasicInfo").gameObject.activeSelf, "B の基礎情報が未解禁なのに解禁ボタンが出ていません");

            yield return Click("Button_Back", "ProfilePanel");
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "プロフィールの戻るボタンでモニターに戻りません");
        }

        [UnityTest]
        public IEnumerator リセットで朝の状態に戻る()
        {
            int morningPoints = InterrogationManager.CurrentPoints;
            CharacterInstance characterA = GetCharacter("A");
            int morningAffection = characterA.CurrentAffection;

            // 行動・解禁・解放をしてから戻す
            InterrogationManager.SetPoints(1000);
            yield return Click("SelectRoomA_Button");
            yield return Click("Button_Talk");
            yield return SkipDialogueByButton();

            yield return Click("Button_ToMonitor");
            yield return Click("SelectRoomB_Button");
            yield return Click("Button_Liberation");
            yield return SkipDialogueByButton();
            Assert.IsTrue(GetCharacter("B").IsReleased, "解放されていません");

            FindComponent<GameUIController>().TryUnlockProfileItemForCharacter("A", "BASIC");
            Assert.IsTrue(characterA.IsBasicInfoUnlocked);

            yield return Click("Button_ToMonitor");
            yield return Click("Reset_Button", "MonitorPanel");
            Assert.IsTrue(IsPanelActive("ResetConfirmDialog"), "リセット確認ダイアログが開きません");
            yield return Click("YesButton", "ResetConfirmDialog");

            Assert.IsFalse(IsPanelActive("ResetConfirmDialog"), "リセット後もダイアログが閉じません");
            Assert.AreEqual(0, InterrogationManager.CurrentDailyActions, "行動回数が戻っていません");
            Assert.AreEqual(morningPoints, InterrogationManager.CurrentPoints, "ポイントが戻っていません");
            Assert.AreEqual(morningAffection, characterA.CurrentAffection, "好感度が戻っていません");
            Assert.IsFalse(characterA.IsBasicInfoUnlocked, "プロフィールの解禁が戻っていません");
            Assert.IsFalse(GetCharacter("B").IsReleased, "解放が戻っていません");
            Assert.IsFalse(InterrogationManager.HasInterrogatedToday("A"), "取り調べ済みの記録が戻っていません");
            Assert.IsTrue(FindButton("SelectRoomB_Button").interactable, "解放を戻したのに B の部屋に入れません");
        }

        [UnityTest]
        public IEnumerator 一日を終えるとリザルトを経てDay2になる()
        {
            yield return Click("SelectRoomA_Button");
            yield return Click("Button_Talk");
            yield return SkipDialogueByButton();
            yield return Click("Button_ToMonitor");

            yield return Click("Day_Button", "MonitorPanel");
            Assert.IsTrue(IsPanelActive("DayEndConfirmDialog"), "1日終了の確認ダイアログが開きません");
            yield return Click("YesButton", "DayEndConfirmDialog");

            Assert.IsTrue(IsPanelActive("ResultPanel"), "リザルト画面が表示されません");

            yield return Click("NextDay_Button", "ResultPanel");
            Assert.AreEqual(2, GameManager.CurrentDay, "Day2 に進んでいません");
            Assert.AreEqual(0, InterrogationManager.CurrentDailyActions, "翌日に行動回数がリセットされていません");
            Assert.IsFalse(InterrogationManager.HasInterrogatedToday("A"), "翌日に取り調べ済みの記録がリセットされていません");
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "翌日がモニター画面から始まりません");
        }
    }
}
#endif
