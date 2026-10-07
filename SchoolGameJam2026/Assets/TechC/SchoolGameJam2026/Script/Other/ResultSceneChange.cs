using UnityEngine;
using UnityEngine.SceneManagement;


namespace TechC.SchoolGameJam2026.Scripts.Other
{

    /// <summary>
    /// InGame シーンに遷移する（タイトル画面のスタートボタン用）。
    /// </summary>
    public class ResultSceneChange : MonoBehaviour
    {
        public void change_button()
        {
            SceneManager.LoadScene("InGame");
        }

    }
}
