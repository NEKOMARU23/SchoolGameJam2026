using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 押すとオプション画面を開くボタン（タイトル画面・ゲーム中の設定ボタンに付ける）
    /// オプション画面は Manager シーンにあるため、Inspector では直接つなげずコードで呼び出す
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OpenOptionButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (OptionUI.Instance != null)
            {
                OptionUI.Instance.Open();
            }
            else
            {
                Debug.LogWarning("[OpenOptionButton] オプション画面が見つかりません（Manager シーンが読み込まれていない可能性があります）。");
            }
        }
    }
}
