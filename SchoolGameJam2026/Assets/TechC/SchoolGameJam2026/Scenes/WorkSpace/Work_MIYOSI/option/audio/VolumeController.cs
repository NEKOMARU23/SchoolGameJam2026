using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using UnityEngine;
using UnityEngine.UI;

public class VolumeController: MonoBehaviour
{
    private Slider slider;

    [SerializeField] private bool isBGM;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        slider = GetComponent<Slider>();

        //AudioManagerから現在の音量を取得してスライダーに反映
        if (isBGM)
        {
            slider.value = AudioManager.Instance.BGMVolume;
            //スライダーが動いたときに音量を変わるようにイベントを登録
            slider.onValueChanged.AddListener(AudioManager.Instance.SetBGMVolume);
        }
        else
        {
            slider.value = AudioManager.Instance.SEVolume;
            slider.onValueChanged.AddListener(AudioManager.Instance.SetSEVolume);
        }
    }
}
    