using UnityEngine;

public class test : MonoBehaviour
{
    bool kai = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        kai = false;
        Debug.Log(kai);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void Onfale()
    {
        kai = true;
        Debug.Log(kai);
    }
}
