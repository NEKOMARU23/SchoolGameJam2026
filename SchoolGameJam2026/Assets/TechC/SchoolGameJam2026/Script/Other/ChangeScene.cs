using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechC.SchoolGameJam2026.Scripts.Other
{
    public class ChangeScene : MonoBehaviour
    {
        public void OnClickToNextScene()
        {
            SceneManager.LoadScene("GAMECLEAR");
        }
    }
}
