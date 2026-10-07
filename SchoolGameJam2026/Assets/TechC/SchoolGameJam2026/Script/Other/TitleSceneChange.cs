using UnityEngine;
using UnityEngine.SceneManagement;


namespace TechC.SchoolGameJam2026.Scripts.Other
{

    /// <summary>
    /// Title シーンに遷移する。
    /// </summary>
    public class TitleSceneChange : MonoBehaviour
    {
        public void change_button()
        {
            SceneManager.LoadScene("Title");
        }

    }
}