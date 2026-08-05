using UnityEngine;

public class optionbutton : MonoBehaviour
{
    [SerializeField] private GameObject panel;
     [SerializeField] private GameObject tutorial;

    private void Start()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void OnOpenButton()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void OnCloseButton()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void OnTutorialButton()
    {
        if (tutorial != null)
        {
            tutorial.SetActive(false);
        }
    }

    public void OnEndButton()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}