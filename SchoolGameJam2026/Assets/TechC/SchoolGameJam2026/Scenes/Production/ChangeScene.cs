using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    public void OnClickToNextScene()
    {
        SceneManager.LoadScene("GAMECLEAR");
    }
}