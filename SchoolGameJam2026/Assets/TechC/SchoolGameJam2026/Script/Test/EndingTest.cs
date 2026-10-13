#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// エンディングの分岐・再生・見た記録・タイトル画面のエンディング一覧のテスト
    /// （Excel の仮のエンディング: 1 ゲームオーバー / 2 冤罪エンド / 3 通常エンド を使う）
    /// </summary>
    public class EndingTest
    {
        [TearDown]
        public void TearDown()
        {
            EndingRecordStore.SetFilePathForTest(null);
        }

        #region エンディングの選び方と記録

        [Test]
        public void 判定結果とフラグで優先度の小さいエンディングが選ばれる()
        {
            var db = ScriptableObject.CreateInstance<ScenarioDatabase>();
            db.endings.Add(new ScenarioEnding { priority = 3, result = EndingResult.Any, endingId = "END_どれでも" });
            db.endings.Add(new ScenarioEnding { priority = 1, result = EndingResult.ReleasedAI, endingId = "END_ゲームオーバー" });
            db.endings.Add(new ScenarioEnding { priority = 2, result = EndingResult.AllCorrect, endingId = "END_真エンド", requiredFlags = { "flag_和解" } });

            var flags = new FlagStore();
            Assert.AreEqual("END_ゲームオーバー", db.SelectEnding(EndingResult.ReleasedAI, flags).endingId);
            Assert.AreEqual("END_どれでも", db.SelectEnding(EndingResult.AllCorrect, flags).endingId, "必要フラグがないので真エンドにならない");

            flags.TurnOn("flag_和解");
            Assert.AreEqual("END_真エンド", db.SelectEnding(EndingResult.AllCorrect, flags).endingId);
            Assert.AreEqual("END_どれでも", db.SelectEnding(EndingResult.ConfirmedHuman, flags).endingId, "指定なしはどの判定結果にも当てはまる");

            db.endings.Clear();
            Assert.IsNull(db.SelectEnding(EndingResult.AllCorrect, flags));
        }

        [Test]
        public void 見たエンディングはファイルに保存され次に起動しても残る()
        {
            UseTestEndingRecord();
            string path = EndingRecordStore.FilePath;

            Assert.IsFalse(EndingRecordStore.IsSeen("END_通常クリア"));
            EndingRecordStore.MarkSeen("END_通常クリア");
            EndingRecordStore.MarkSeen("END_通常クリア");
            Assert.IsTrue(File.Exists(path), "記録のファイルが作られていません");

            // 読み込み直しても残っている
            EndingRecordStore.SetFilePathForTest(path);
            Assert.IsTrue(EndingRecordStore.IsSeen("END_通常クリア"));
            Assert.AreEqual(1, EndingRecordStore.SeenEndingIds.Count, "同じエンディングが2回記録されています");
        }

        #endregion

        #region 最終日の後

        [UnityTest]
        public IEnumerator 全員を正しく判定すると通常エンドを見てからクリア画面に移る()
        {
            yield return LoadInGameScene();
            GetCharacter("A").SetAIFlag(true);
            yield return JudgeEveryone(confirmA: true, releaseA: false);

            yield return EndDayByButton();
            EndingPlayerUI player = FindComponent<EndingPlayerUI>();
            yield return WaitUntilPlaying(player);
            StringAssert.Contains("全員を正しく判定した", BodyText(player), "通常エンドの文章が出ていません");
            Assert.AreEqual(SceneManager.GetActiveScene().name, InGameSceneName, "エンディングを見る前に画面が移りました");

            player.Advance();   // 文字の表示を飛ばす
            player.Advance();   // 最後のページを閉じる
            yield return WaitForScene(ClearSceneName);
            Assert.IsTrue(EndingRecordStore.IsSeen("END_通常クリア"), "見たエンディングが記録されていません");
        }

        [UnityTest]
        public IEnumerator AIを解放するとゲームオーバーのエンディングを見てからゲームオーバー画面に移る()
        {
            yield return LoadInGameScene();
            GetCharacter("A").SetAIFlag(true);
            yield return JudgeEveryone(confirmA: false, releaseA: true);

            yield return EndDayByButton();
            EndingPlayerUI player = FindComponent<EndingPlayerUI>();
            yield return WaitUntilPlaying(player);
            StringAssert.Contains("AIを解放してしまった", BodyText(player));

            yield return WaitForScene(GameOverSceneName);
            Assert.IsTrue(EndingRecordStore.IsSeen("END_ゲームオーバー"));
            Assert.IsFalse(EndingRecordStore.IsSeen("END_通常クリア"));
        }

        #endregion

        #region タイトル画面のエンディング一覧

        [UnityTest]
        public IEnumerator エンディング一覧は見ていないものを隠し見たものは再生できる()
        {
            UseTestEndingRecord();
            EndingRecordStore.MarkSeen("END_冤罪");
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;

            yield return Click("EndingListButton");
            EndingListUI list = FindComponent<EndingListUI>();
            Assert.IsTrue(list.IsOpen, "エンディング一覧が開きません");
            CollectionAssert.AreEqual(new[] { EndingListUI.HiddenName, "冤罪エンド", EndingListUI.HiddenName }, list.ShownNames);
            Transform count = list.transform.Find("EndingListPanel/CountText");
            Assert.AreEqual("見たエンディング 1 / 3", count.GetComponent<TMPro.TextMeshProUGUI>().text);
            Assert.IsFalse(FindButton("EndingEntry_1").interactable, "見ていないエンディングを押せます");

            yield return Click("EndingEntry_2");
            EndingPlayerUI player = FindComponent<EndingPlayerUI>();
            Assert.IsTrue(player.IsPlaying, "見たエンディングを再生できません");
            Assert.IsFalse(list.IsOpen, "再生中も一覧が開いたままです");

            for (int i = 0; i < 20 && player.IsPlaying; i++)
            {
                player.Advance();
                yield return null;
            }
            Assert.IsFalse(player.IsPlaying);
            Assert.IsTrue(list.IsOpen, "再生が終わっても一覧に戻りません");

            yield return Click("CloseButton", "EndingListPanel");
            Assert.IsFalse(list.IsOpen);
        }

        #endregion

        #region ヘルパー

        private static IEnumerator JudgeEveryone(bool confirmA, bool releaseA)
        {
            Assert.IsTrue(GameManager.JumpToDay(GameManager.MaxDays));
            yield return null;
            yield return JudgeOnFinalDay("A", confirmAI: confirmA && !releaseA);
            yield return JudgeOnFinalDay("B", confirmAI: false);
            yield return JudgeOnFinalDay("C", confirmAI: false);
            yield return JudgeOnFinalDay("D", confirmAI: false);
        }

        private static IEnumerator WaitUntilPlaying(EndingPlayerUI player)
        {
            for (int i = 0; i < 300 && !player.IsPlaying; i++) yield return null;
            Assert.IsTrue(player.IsPlaying, "エンディングの会話が始まりません");
        }

        private static string BodyText(EndingPlayerUI player)
        {
            return player.transform.Find("TextWindow/Body").GetComponent<TMPro.TextMeshProUGUI>().text;
        }

        #endregion
    }
}
#endif
