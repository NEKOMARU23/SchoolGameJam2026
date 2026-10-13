using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 押すとロードの枠を開くボタン（タイトル画面の「つづきから」）
    /// ロードの画面は Manager シーンにあるので、OpenOptionButton と同じく Instance 経由で開く
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OpenLoadButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (SaveLoadController.Instance != null) SaveLoadController.Instance.OpenLoad();
        }
    }
}
