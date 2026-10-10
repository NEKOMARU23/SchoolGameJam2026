#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.SO;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// Day1〜7 を通しでプレイし、最終日の判定でクリアになるかを確認する
    /// 毎日全員に「対話」を行うため、全日程のシナリオデータの読み込みエラーも検出できる
    /// </summary>
    public class InGameFullWeekTest
    {
        private GameUIController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
            controller = FindComponent<GameUIController>();
        }

        [UnityTest]
        public IEnumerator 七日間を通して遊び最終日に全員を正しく判定するとクリア()
        {
            // ゲーム開始時は全員人間。テストでは A を AI にしておく
            CollectionAssert.IsEmpty(GameManager.AllCharacters.FindAll(c => c.IsAI), "ゲーム開始時に AI のキャラがいます");
            GetCharacter("A").SetAIFlag(true);

            yield return PlayUntilFinalDay();

            yield return JudgeOnFinalDay("A", confirmAI: true);
            yield return JudgeOnFinalDay("B", confirmAI: false);
            yield return JudgeOnFinalDay("C", confirmAI: false);
            yield return JudgeOnFinalDay("D", confirmAI: false);

            yield return EndDayByButton();
            yield return WaitForScene(ClearSceneName);
        }

        #region 操作ヘルパー

        /// <summary>
        /// 毎日全員と対話しながら、最終日の朝まで進める
        /// </summary>
        private IEnumerator PlayUntilFinalDay()
        {
            while (!GameManager.IsFinalDay)
            {
                int day = GameManager.CurrentDay;

                // 最終日以外は判定（解放・AI確定）のボタンが出ない
                controller.SelectCharacter("A");
                yield return null;
                Assert.IsFalse(FindButton(ConfirmAIButtonName).gameObject.activeSelf, $"Day{day} なのに AI確定ボタンが表示されています");
                Assert.IsFalse(FindButton(ReleaseButtonName).gameObject.activeSelf, $"Day{day} なのに解放ボタンが表示されています");

                yield return TalkToEveryone();
                yield return EndDayByButton();

                Assert.AreEqual(day + 1, GameManager.CurrentDay, $"Day{day} から翌日に進んでいません");
            }

            Assert.AreEqual(GameManager.MaxDays, GameManager.CurrentDay);
        }

        /// <summary>
        /// 全員に「対話」を行う
        /// </summary>
        private IEnumerator TalkToEveryone()
        {
            foreach (CharacterInstance character in GameManager.AllCharacters)
            {
                controller.SelectCharacter(character.CharacterId);
                yield return null;

                int actionsBefore = InterrogationManager.CurrentDailyActions;
                controller.ExecuteInterrogationAction((int)InterrogationActionType.Talk);
                yield return SkipDialogue();

                Assert.AreEqual(actionsBefore + 1, InterrogationManager.CurrentDailyActions,
                    $"Day{GameManager.CurrentDay} の {character.CharacterId} への対話が実行されていません");
            }
        }

        #endregion
    }
}
#endif
