using UnityEngine;
using UnityEngine.SceneManagement;


namespace TechC.SchoolGameJam2026.Script.Other
{

    /// <summary>
    /// Result Scene‚É•ÎˆÚ‚·‚éB
    /// </summary>
    public class ResultSceneChange : MonoBehaviour
    {
        public void change_button()
        {
            SceneManager.LoadScene("Result");
        }

    }
}