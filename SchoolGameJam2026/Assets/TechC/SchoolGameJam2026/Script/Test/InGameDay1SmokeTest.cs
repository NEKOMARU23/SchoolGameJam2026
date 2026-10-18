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
        public IEnumerator モニターに各部屋のキャラの顔が映っている()
        {
            Transform grid = FindComponent<MonitorUI>().transform.Find("MonitorGrid");
            foreach (string id in new[] { "A", "B", "C", "D" })
            {
                Transform face = grid.Find($"MonitorRoom_{id}/SelectRoom{id}_Button/Screen/Face");
                Assert.IsNotNull(face, $"部屋 {id} のモニターに顔の表示がありません");

                Image faceImage = face.GetComponent<Image>();
                CharacterInstance character = GameManager.AllCharacters.Find(c => c.CharacterId == id);
                Assert.IsTrue(faceImage.enabled, $"部屋 {id} の顔が表示されていません");
                Assert.AreEqual(character.ProfileData.characterIcon, faceImage.sprite, $"部屋 {id} に別のキャラの顔が映っています");
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator モニター画面の左上のアイコンに文字が出ている()
        {
            var expected = new (string button, string label)[]
            {
                ("Day_Button", "1日を終える"),
                ("Reset_Button", "今日をリセット"),
                ("Button_Log", "ログ"),
                ("Setting_Button", "設定"),
            };
            foreach (var (button, label) in expected)
            {
                Transform text = FindButton(button, "MonitorPanel").transform.Find("Label/Text");
                Assert.IsNotNull(text, $"{button} に文字の札がありません");
                Assert.IsTrue(text.gameObject.activeInHierarchy, $"{button} の文字が表示されていません");
                Assert.AreEqual(label, text.GetComponent<TMPro.TextMeshProUGUI>().text);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator スマホにマウスを乗せると持ち上がって名前が出る()
        {
            Button phone = FindButton("Button_Help", "MonitorPanel");
            var hover = phone.GetComponent<HoverHighlightUI>();
            Assert.IsNotNull(hover, "スマホにマウスを乗せたときの反応がありません");

            Transform panel = FindComponent<MonitorUI>().transform;
            GameObject label = panel.Find("SmartphoneLabel").gameObject;
            GameObject glow = panel.Find("SmartphoneGlow").gameObject;
            var rect = (RectTransform)phone.transform;
            Vector2 basePosition = rect.anchoredPosition;
            Assert.IsFalse(label.activeSelf, "乗せる前から名前が出ています");

            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            hover.OnPointerEnter(pointer);
            yield return null;
            Assert.IsTrue(label.activeSelf, "乗せても「スマホ」の名前が出ません");
            Assert.IsTrue(glow.activeSelf, "乗せてもふちが光りません");
            Assert.Greater(rect.anchoredPosition.y, basePosition.y, "乗せても持ち上がりません");

            hover.OnPointerExit(pointer);
            yield return null;
            Assert.IsFalse(label.activeSelf, "離しても名前が消えません");
            Assert.IsFalse(glow.activeSelf, "離しても光が消えません");
            Assert.AreEqual(basePosition, rect.anchoredPosition, "離しても元の位置に戻りません");
        }

        [UnityTest]
        public IEnumerator 行動の会話が終わると入ったポイントが出て0のときは出ない()
        {
            var pointGain = FindComponent<PointGainUI>();

            // ココアに正解行動（対話）: 会話の間は出ず、会話が終わったら出る
            yield return Click("SelectRoomA_Button");
            int before = InterrogationManager.TodayEarnedPoints;
            yield return Click("Button_Talk");
            int earned = InterrogationManager.TodayEarnedPoints - before;
            Assert.Greater(earned, 0, "前提: 1日目のココアへの対話でポイントが入る");
            Assert.IsFalse(pointGain.IsShowing, "会話の途中でポイントが出ました");

            yield return SkipDialogueByButton();
            Assert.IsTrue(pointGain.IsShowing, "会話が終わってもポイントが出ません");
            Assert.AreEqual($"+{earned}pt", pointGain.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text);

            // メリに NG行動（拷問）: ポイントは0なので出ない
            yield return Click("Button_ToMonitor");
            yield return Click("SelectRoomB_Button");
            Assert.IsFalse(pointGain.IsShowing, "部屋を移っても前のポイントが残っています");
            before = InterrogationManager.TodayEarnedPoints;
            yield return Click("Button_Torture");
            yield return SkipDialogueByButton();
            Assert.AreEqual(before, InterrogationManager.TodayEarnedPoints, "前提: 1日目のメリへの拷問（NG行動）はポイントが入らない");
            Assert.IsFalse(pointGain.IsShowing, "ポイントが入っていないのに出ました");
        }

        [UnityTest]
        public IEnumerator 行動を選ぶとボタンが消え入り直すと今日はもう取り調べましたと出る()
        {
            string[] actionButtons = { "Button_Torture", "Button_Talk", "Button_Negotiate" };

            yield return Click("SelectRoomA_Button");
            Assert.IsFalse(IsPanelActive("InterrogatedNotice"), "取り調べる前から「今日はもう取り調べました」が出ています");
            Assert.IsTrue(FindButton("Button_Talk").gameObject.activeInHierarchy, "取り調べる前に行動のボタンがありません");

            // 行動を選んだその場では、ボタンが消えるだけで文は出ない
            yield return Click("Button_Talk");
            foreach (string name in actionButtons)
            {
                Assert.IsFalse(FindButton(name).gameObject.activeInHierarchy, $"行動を選んだあとも {name} が残っています");
            }
            yield return SkipDialogueByButton();
            Assert.IsFalse(IsPanelActive("InterrogatedNotice"), "行動を選んだその場で「今日はもう取り調べました」が出ました");

            // 部屋に入り直すと文が出る
            yield return Click("Button_ToMonitor");
            yield return Click("SelectRoomA_Button");
            Assert.IsTrue(IsPanelActive("InterrogatedNotice"), "入り直しても「今日はもう取り調べました」が出ません");
            foreach (string name in actionButtons)
            {
                Assert.IsFalse(FindButton(name).gameObject.activeInHierarchy, $"入り直したら {name} が出ています");
            }

            // まだ取り調べていない部屋では、ボタンが出て札は出ない
            yield return Click("Button_ToMonitor");
            yield return Click("SelectRoomB_Button");
            Assert.IsFalse(IsPanelActive("InterrogatedNotice"), "まだ取り調べていない部屋で「今日はもう取り調べました」が出ています");
            Assert.IsTrue(FindButton("Button_Talk").gameObject.activeInHierarchy, "まだ取り調べていない部屋に行動のボタンがありません");
        }

        [UnityTest]
        public IEnumerator 文字が全部出ると文の最後に次へ進む印が出る()
        {
            var dialogueUI = FindComponent<DialogueUI>();
            NextPageMarkUI mark = FindNextPageMark("InterrogationPanel");

            yield return Click("SelectRoomA_Button");
            yield return Click("Button_Talk");
            yield return null;
            Assert.IsFalse(mark.IsShowing, "文字送りの途中で「▼」が出ています");

            // 全文を出すと「▼」が出る（次のページが残っているとき）
            dialogueUI.OnClickDialogueArea();
            yield return null;
            yield return null;
            Assume.That(dialogueUI.IsDialoguePlaying, "前提: 1日目のココアへの対話は2ページ以上ある");
            Assert.IsTrue(mark.IsShowing, "文字が全部出ても「▼」が出ません");

            // 最後のページを表示し終えると会話が終わるので、「▼」は出ない
            for (int i = 0; i < 50 && dialogueUI.IsDialoguePlaying; i++) dialogueUI.OnClickDialogueArea();
            yield return null;
            Assert.IsFalse(mark.IsShowing, "会話が終わっても「▼」が残っています");

            // スマホの会話でも出る
            yield return Click("Button_ToMonitor");
            yield return Click("Button_Help", "MonitorPanel");
            var talk = FindComponent<DialogueClickable>();
            Assert.IsTrue(talk.IsOpen, "前提: スマホの会話が開く");
            NextPageMarkUI phoneMark = FindNextPageMark("SmartphoneTalkPanel");
            talk.AdvanceDialoguePage();   // 文字送りの途中なら全文を出す
            yield return null;
            yield return null;
            Assert.IsTrue(phoneMark.IsShowing, "スマホの会話で文字が全部出ても「▼」が出ません");
        }

        private static NextPageMarkUI FindNextPageMark(string panelName)
        {
            foreach (var m in Object.FindObjectsByType<NextPageMarkUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                for (Transform t = m.transform; t != null; t = t.parent)
                {
                    if (t.name == panelName) return m;
                }
            }
            Assert.Fail($"{panelName} の「▼」が見つかりません");
            return null;
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
            yield return ClickConfirmYes();
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
                yield return ClickConfirmYes();
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

            // 行動・解禁・フラグの獲得をしてから戻す（解放と AI確定は最終日のテストで確認する）
            InterrogationManager.SetPoints(1000);
            yield return Click("SelectRoomA_Button");
            yield return Click("Button_Talk");
            yield return SkipDialogueByButton();

            FindComponent<GameUIController>().TryUnlockProfileItemForCharacter("A", "BASIC");
            yield return ClickConfirmYes();
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
            Assert.IsFalse(InterrogationManager.HasInterrogatedToday("A"), "取り調べ済みの記録が戻っていません");
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
        public IEnumerator スマホでマキナと会話でき回数を使い切るとお知らせが出る()
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

                // その日の行動（A と対話。最終日は全員を判定）→ 1日終了 → 次の日へ
                if (GameManager.IsFinalDay)
                {
                    foreach (string id in new[] { "A", "B", "C", "D" }) yield return JudgeOnFinalDay(id, confirmAI: false);
                    FindComponent<ScreenNavigationUI>().ShowMonitorPanel();
                    yield return null;
                }
                else
                {
                    yield return Click("SelectRoomA_Button");
                    yield return Click("Button_Talk");
                    yield return SkipDialogueByButton();
                    yield return Click("Button_ToMonitor");
                }
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
