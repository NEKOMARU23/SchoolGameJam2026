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
    /// Day1〜7 を通しでプレイし、クリア / ゲームオーバーの判定が正しいかを確認する
    /// 毎日全員に「対話」を行うため、全日程のシナリオデータの読み込みエラーも検出できる
    /// </summary>
    public class InGameFullWeekTest
    {
        private const string ConfirmAIButtonName = "Button_AI confirmed (1)";

        private GameUIController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
            controller = FindComponent<GameUIController>();
        }

        [UnityTest]
        public IEnumerator 最終日にAIを当てるとクリア()
        {
            CharacterInstance ai = GetAICharacter();

            yield return PlayUntilFinalDay();

            // 最終日は行動回数を使い切っていても AI 確定できる
            yield return TalkToEveryone();
            Assert.IsTrue(InterrogationManager.IsActionLimitReached, "前提: 行動回数を使い切っている");

            yield return ConfirmAI(ai.CharacterId);
            yield return WaitForScene(ClearSceneName);
        }

        [UnityTest]
        public IEnumerator 最終日に人間をAIと判定するとゲームオーバー()
        {
            CharacterInstance ai = GetAICharacter();
            CharacterInstance human = GameManager.AllCharacters.Find(c => !c.IsAI);

            yield return PlayUntilFinalDay();

            yield return ConfirmAI(human.CharacterId);
            yield return WaitForScene(GameOverSceneName);
        }

        [UnityTest]
        public IEnumerator AIを解放するとゲームオーバー()
        {
            CharacterInstance ai = GetAICharacter();
            CharacterInstance human = GameManager.AllCharacters.Find(c => !c.IsAI);

            yield return Release(ai.CharacterId);
            Assert.IsTrue(ai.IsReleased);

            yield return PlayUntilFinalDay();

            // 解放済みの AI は部屋に入れないので、他のキャラを判定しても助からない
            yield return ConfirmAI(human.CharacterId);
            yield return WaitForScene(GameOverSceneName);
        }

        [UnityTest]
        public IEnumerator AIを解放してもその日をリセットすればクリアできる()
        {
            CharacterInstance ai = GetAICharacter();

            yield return Release(ai.CharacterId);
            Assert.IsTrue(ai.IsReleased);

            controller.OnConfirmResetDay();
            yield return null;
            Assert.IsFalse(ai.IsReleased, "リセットで AI の解放が戻っていません");

            yield return PlayUntilFinalDay();

            yield return ConfirmAI(ai.CharacterId);
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

                // 最終日以外は AI 確定ボタンが出ない
                controller.SelectCharacter(GameManager.AllCharacters.Find(c => !c.IsReleased).CharacterId);
                yield return null;
                Assert.IsFalse(FindButton(ConfirmAIButtonName).gameObject.activeSelf, $"Day{day} なのに AI 確定ボタンが表示されています");

                yield return TalkToEveryone();
                yield return EndDay();

                Assert.AreEqual(day + 1, GameManager.CurrentDay, $"Day{day} から翌日に進んでいません");
            }

            Assert.AreEqual(GameManager.MaxDays, GameManager.CurrentDay);
        }

        /// <summary>
        /// 解放されていない全員に「対話」を行う（行動回数が尽きたら終了）
        /// </summary>
        private IEnumerator TalkToEveryone()
        {
            foreach (CharacterInstance character in GameManager.AllCharacters)
            {
                if (character.IsReleased || InterrogationManager.IsActionLimitReached) continue;

                controller.SelectCharacter(character.CharacterId);
                yield return null;

                int actionsBefore = InterrogationManager.CurrentDailyActions;
                controller.ExecuteInterrogationAction((int)InterrogationActionType.Talk);
                yield return SkipDialogue();

                Assert.AreEqual(actionsBefore + 1, InterrogationManager.CurrentDailyActions,
                    $"Day{GameManager.CurrentDay} の {character.CharacterId} への対話が実行されていません");
            }
        }

        private IEnumerator Release(string characterId)
        {
            controller.SelectCharacter(characterId);
            yield return null;
            controller.ExecuteReleaseCharacter();
            yield return SkipDialogue();
        }

        private IEnumerator EndDay()
        {
            controller.OnConfirmEndDay();
            yield return null;
            Assert.IsTrue(IsPanelActive("ResultPanel"), "リザルト画面が表示されません");

            controller.OnClickCloseResultAndAdvanceDay();
            yield return null;
        }

        /// <summary>
        /// 最終日に対象キャラの部屋で AI 確定ボタンを押し、リザルトを閉じる
        /// </summary>
        private IEnumerator ConfirmAI(string characterId)
        {
            Assert.IsTrue(GameManager.IsFinalDay, "前提: 最終日である");

            controller.SelectCharacter(characterId);
            yield return null;

            yield return Click(ConfirmAIButtonName);
            Assert.IsTrue(IsPanelActive("ResultPanel"), "AI 確定後にリザルト画面が表示されません");

            yield return SkipDialogue();
            controller.OnClickCloseResultAndAdvanceDay();
            yield return null;
        }

        #endregion
    }
}
#endif
