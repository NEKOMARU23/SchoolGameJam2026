#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// オプション画面（Manager シーンにあり、タイトル・ゲーム中で共有）のテスト
    /// テスト中に変えた設定は、終了時に元の設定ファイルへ戻す
    /// </summary>
    public class OptionTest
    {
        private string backupSettingsJson;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            backupSettingsJson = File.Exists(SettingsManager.FilePath) ? File.ReadAllText(SettingsManager.FilePath) : null;
            yield return LoadInGameScene();
            Assert.IsNotNull(SettingsManager.Instance, "SettingsManager が見つかりません（Manager シーンに Option が置かれていない）");
            Assert.IsNotNull(OptionUI.Instance, "OptionUI が見つかりません");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (OptionUI.IsOpen) OptionUI.Instance.Close();

            if (backupSettingsJson != null) File.WriteAllText(SettingsManager.FilePath, backupSettingsJson);
            else if (File.Exists(SettingsManager.FilePath)) File.Delete(SettingsManager.FilePath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ゲーム中の設定ボタンで開き戻るボタンで閉じる()
        {
            Assert.IsFalse(OptionUI.IsOpen, "最初からオプションが開いています");

            yield return Click("Setting_Button", "Canvas");
            Assert.IsTrue(OptionUI.IsOpen, "設定ボタンでオプションが開きません");

            yield return Click("BackButton", "OptionPanel");
            Assert.IsFalse(OptionUI.IsOpen, "「ゲームに戻る」で閉じません");
            Assert.IsTrue(File.Exists(SettingsManager.FilePath), "閉じたときに設定が保存されていません");
        }

        [UnityTest]
        public IEnumerator 音量を変えるとBGMとSEに反映される()
        {
            yield return Click("Setting_Button", "Canvas");

            FindSlider("BgmSlider").value = 0.2f;
            FindSlider("SeSlider").value = 0.8f;
            yield return null;

            Assert.AreEqual(0.2f, AudioManager.Instance.BGMVolume, 0.001f, "BGM の音量が反映されていません");
            Assert.AreEqual(0.8f, AudioManager.Instance.SEVolume, 0.001f, "SE の音量が反映されていません");
        }

        [UnityTest]
        public IEnumerator 文字の速さを変えると会話の速さに反映される()
        {
            yield return Click("Setting_Button", "Canvas");

            yield return Click("TextSpeed_2", "OptionPanel");  // 速い
            Assert.AreEqual(TextSpeed.Fast, SettingsManager.Instance.Current.textSpeed);
            Assert.AreEqual(0.025f, GetDialogueTextSpeed(), 0.0001f, "取調室の会話の速さに反映されていません");

            yield return Click("TextSpeed_0", "OptionPanel");  // 遅い
            Assert.AreEqual(0.08f, GetDialogueTextSpeed(), 0.0001f, "取調室の会話の速さに反映されていません");
        }

        [UnityTest]
        public IEnumerator 設定はファイルに保存され次回も読み込まれる()
        {
            yield return Click("Setting_Button", "Canvas");
            FindSlider("BgmSlider").value = 0.3f;
            yield return Click("ScreenMode_1", "OptionPanel");  // ウィンドウ
            yield return Click("AutoWait_2", "OptionPanel");    // 長い
            yield return Click("SkipMode_1", "OptionPanel");    // すべて
            yield return Click("BackButton", "OptionPanel");

            var saved = JsonUtility.FromJson<GameSettings>(File.ReadAllText(SettingsManager.FilePath));
            Assert.AreEqual(0.3f, saved.bgmVolume, 0.001f);
            Assert.IsFalse(saved.isFullScreen);
            Assert.AreEqual(AutoWait.Long, saved.autoWait);
            Assert.AreEqual(SkipMode.All, saved.skipMode);
        }

        [UnityTest]
        public IEnumerator タイトル画面からも同じオプションを開ける()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;

            yield return Click("OptionButton");
            Assert.IsTrue(OptionUI.IsOpen, "タイトル画面のオプションボタンで開きません");

            yield return Click("BackButton", "OptionPanel");
            Assert.IsFalse(OptionUI.IsOpen);
        }

        private static Slider FindSlider(string name)
        {
            foreach (Slider slider in Object.FindObjectsByType<Slider>(FindObjectsInactive.Include))
            {
                if (slider.name == name) return slider;
            }
            Assert.Fail($"スライダー '{name}' が見つかりません");
            return null;
        }

        private static float GetDialogueTextSpeed()
        {
            FieldInfo field = typeof(DialogueUI).GetField("textSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)field.GetValue(FindComponent<DialogueUI>());
        }
    }
}
#endif
