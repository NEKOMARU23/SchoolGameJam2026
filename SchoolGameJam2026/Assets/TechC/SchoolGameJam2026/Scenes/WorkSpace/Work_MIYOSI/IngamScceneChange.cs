using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class IngameSceneChange : MonoBehaviour
{
    public void OnStartButton()
    {
        SceneManager.LoadScene("InGame");
    }
}