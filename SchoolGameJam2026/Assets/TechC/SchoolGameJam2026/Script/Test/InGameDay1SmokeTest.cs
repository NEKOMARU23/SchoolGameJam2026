#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.UI;
using TechC.SchoolGameJam2026.Scripts.UI.Dialogue;
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

        [UnityTest]
        public IEnumerator 日付切り替え時はロード画面で覆ってから翌日になる()
        {
            var transition = FindComponent<DayTransitionUI>();
            var cover = transition.GetComponent<CanvasGroup>();

            // 他のテストでは演出を省略しているので、ここだけ実際に再生する
            transition.SetDurations(0.2f, 0.3f);
            Assert.AreEqual(0f, cover.alpha, "最初からロード画面が見えています");
            Assert.IsFalse(cover.blocksRaycasts, "最初からロード画面が操作を遮っています");

            yield return Click("Day_Button", "MonitorPanel");
            yield return Click("YesButton", "DayEndConfirmDialog");

            // 連打しても1日しか進まない
            Button nextDayButton = FindButton("NextDay_Button", "ResultPanel");
            nextDayButton.onClick.Invoke();
            nextDayButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(transition.IsPlaying, "ロード画面の演出が始まっていません");
            Assert.IsTrue(cover.blocksRaycasts, "演出中に下の画面を操作できてしまいます");
            Assert.AreEqual(1, GameManager.CurrentDay, "画面を覆い終わる前に日付が進んでいます");

            float timeout = 5f;
            while (transition.IsPlaying && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(transition.IsPlaying, "ロード画面の演出が終わりません");
            Assert.AreEqual(2, GameManager.CurrentDay, "連打で日付が進みすぎているか、翌日に進んでいません");
            Assert.AreEqual(0f, cover.alpha, "演出後もロード画面が残っています");
            Assert.IsFalse(cover.blocksRaycasts, "演出後も操作が遮られたままです");
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "演出後にモニター画面に戻っていません");
        }

        [UnityTest]
        public IEnumerator スマホで補助ちゃんと会話でき回数を使い切るとお知らせが出る()
        {
            var talk = FindComponent<DialogueClickable>();
            int limit = GameManager.Scenario.GetSmartphoneTalkCount(1);
            Assert.Greater(limit, 0, "前提: 1日目のセリフが1件以上用意されている");

            for (int i = 1; i <= limit; i++)
            {
                yield return Click("Button_Help", "MonitorPanel");
                Assert.IsTrue(IsPanelActive("SmartphoneTalkPanel"), "スマホを押しても会話画面が開きません");
                Assert.IsTrue(talk.IsOpen, "会話が始まっていません");
                StringAssert.Contains("1日目", FindDialogueText().text, "1日目のセリフが表示されていません");

                yield return Click("CloseButton", "SmartphoneTalkPanel");
                Assert.IsFalse(talk.IsOpen, "閉じるボタンで会話が終わりません");
                Assert.IsTrue(IsPanelActive("MonitorPanel"), "会話を閉じてもモニター画面に戻りません");
            }

            // 使い切った後に押すと、会話画面は開かずにお知らせが出る
            yield return AssertSmartphoneNoTalkNotice();

            // その日をやり直すと回数も朝の状態に戻る
            yield return Click("Reset_Button", "MonitorPanel");
            yield return Click("YesButton", "ResetConfirmDialog");
            yield return Click("Button_Help", "MonitorPanel");
            Assert.IsTrue(talk.IsOpen, "リセットしてもスマホの回数が戻りません");
        }

        /// <summary>
        /// スマホを押しても会話画面が開かず、「今日はもう連絡ができない」が表示されることを確認する
        /// </summary>
        private static IEnumerator AssertSmartphoneNoTalkNotice()
        {
            yield return Click("Button_Help", "MonitorPanel");
            Assert.IsFalse(IsPanelActive("SmartphoneTalkPanel"), "回数を使い切っても会話画面が開いてしまいます");
            Assert.IsTrue(IsPanelActive("MonitorPanel"), "モニター画面から移動してしまいました");
            Assert.IsTrue(IsPanelActive("SmartphoneNotice"), "「今日はもう連絡ができない」が表示されません");

            Transform noticeText = FindComponent<MonitorUI>().transform.Find("SmartphoneNotice/SmartphoneNoticeText");
            Assert.IsNotNull(noticeText, "お知らせの文字欄が見つかりません");
            Assert.AreEqual("今日はもう連絡ができない", noticeText.GetComponent<TMPro.TextMeshProUGUI>().text);
        }

        [UnityTest]
        public IEnumerator スマホの会話回数は翌日に戻り翌日のセリフになる()
        {
            var talk = FindComponent<DialogueClickable>();
            for (int i = 0; i < GameManager.Scenario.GetSmartphoneTalkCount(1); i++)
            {
                yield return Click("Button_Help", "MonitorPanel");
                yield return Click("CloseButton", "SmartphoneTalkPanel");
            }
            yield return AssertSmartphoneNoTalkNotice();

            yield return Click("Day_Button", "MonitorPanel");
            yield return Click("YesButton", "DayEndConfirmDialog");
            yield return Click("NextDay_Button", "ResultPanel");
            Assert.AreEqual(2, GameManager.CurrentDay);

            yield return Click("Button_Help", "MonitorPanel");
            Assert.IsTrue(talk.IsOpen, "翌日になってもスマホで会話できません");
            StringAssert.Contains("2日目", FindDialogueText().text, "2日目のセリフが表示されていません");
        }

        private static TMPro.TextMeshProUGUI FindDialogueText()
        {
            Transform text = FindComponent<DialogueClickable>().transform.Find("SmartphoneTalk_Chatbox/DialogueText");
            Assert.IsNotNull(text, "スマホ会話画面のセリフ欄が見つかりません");
            return text.GetComponent<TMPro.TextMeshProUGUI>();
        }

        [UnityTest]
        public IEnumerator 日付切り替えのロード画面に正しい日付が表示される()
        {
            var transition = FindComponent<DayTransitionUI>();
            var cover = transition.GetComponent<CanvasGroup>();
            TMPro.TextMeshProUGUI label = FindComponent<DayTransitionUI>().transform.Find("DayTransitionDayText").GetComponent<TMPro.TextMeshProUGUI>();

            // 本番と同じ演出時間で確認する
            transition.SetDurations(0.4f, 1.2f);
            var shownLabels = new System.Collections.Generic.List<string>();

            for (int day = 1; day <= GameManager.MaxDays; day++)
            {
                Assert.AreEqual(day, GameManager.CurrentDay, "日付がずれています");

                // その日の行動（A と対話）→ 1日終了 → 次の日へ
                yield return Click("SelectRoomA_Button");
                yield return Click("Button_Talk");
                yield return SkipDialogueByButton();
                yield return Click("Button_ToMonitor");
                yield return Click("Day_Button", "MonitorPanel");
                yield return Click("YesButton", "DayEndConfirmDialog");
                yield return Click("NextDay_Button", "ResultPanel");

                // 画面が覆い終わった時点の表示を記録
                float timeout = 5f;
                while (cover.alpha < 1f && timeout > 0f)
                {
                    timeout -= Time.unscaledDeltaTime;
                    yield return null;
                }
                shownLabels.Add(label.text);
                Debug.Log($"[日付切り替えテスト] {day}日目の終了時に表示された文字: {label.text}");

                string expected = day < GameManager.MaxDays ? $"{day + 1}日目" : "最終結果";
                Assert.AreEqual(expected, label.text, $"{day}日目の終了時の表示が違います（これまでの表示: {string.Join(", ", shownLabels)}）");

                if (day == GameManager.MaxDays) break;

                // 演出が終わるまで待つ
                timeout = 5f;
                while (transition.IsPlaying && timeout > 0f)
                {
                    timeout -= Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.IsFalse(transition.IsPlaying, $"{day}日目の終了時の演出が終わりません");
            }
        }
    }
}
#endif
