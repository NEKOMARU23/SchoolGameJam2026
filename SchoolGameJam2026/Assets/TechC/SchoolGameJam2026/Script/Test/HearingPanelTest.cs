#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static TechC.SchoolGameJam2026.Scripts.Test.InGameTestUtility;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 「友人について」（聞き取り）のパネルのテスト
    /// </summary>
    public class HearingPanelTest
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadInGameScene();
        }

        [Test]
        public void 聞き取りのボタンにはそのキャラの名前プレートが貼られている()
        {
            // モニターの名前プレートと同じ絵が、聞き取りのボタンにも貼られているか（前はカネルのボタンにマキナのプレートが貼られていた）
            foreach (string id in new[] { "A", "B", "C", "D" })
            {
                Image hearing = FindButton("Button_" + id, "HearingPanel").GetComponent<Image>();
                Transform plate = FindButton($"SelectRoom{id}_Button", "MonitorPanel").transform.Find("NamePlate");
                Assert.IsNotNull(plate, $"前提: モニター {id} の名前プレートがある");
                Sprite expected = plate.GetComponent<Image>().sprite;

                Assert.AreEqual(expected.name, hearing.sprite != null ? hearing.sprite.name : "なし",
                    $"聞き取りの {id} のボタンに、ほかのキャラの名前プレートが貼られています");
            }
        }

        [UnityTest]
        public IEnumerator 聞き取りでは取り調べ中の人以外の3人のボタンが出る()
        {
            foreach (string room in new[] { "A", "B", "C", "D" })
            {
                yield return Click($"SelectRoom{room}_Button");
                yield return Click("Button_HearAbout");

                foreach (string id in new[] { "A", "B", "C", "D" })
                {
                    bool shown = FindButton("Button_" + id, "HearingPanel").gameObject.activeInHierarchy;
                    Assert.AreEqual(id != room, shown, $"部屋 {room} で聞き取りをしたとき、{id} のボタンの出方が違います");
                }

                yield return Click("CloseButton", "HearingPanel");
                yield return Click("Button_ToMonitor");
            }
        }
    }
}
#endif
