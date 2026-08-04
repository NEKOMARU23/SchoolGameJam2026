using UnityEngine;

public class optionbutton : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    private void Start()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void OnStartButton()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void OnBackButton()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
}
