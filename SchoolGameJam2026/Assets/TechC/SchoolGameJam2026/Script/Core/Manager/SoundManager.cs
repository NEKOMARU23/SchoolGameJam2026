using UnityEngine;
using UnityEngine.SceneManagement;
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("各画面のBGM(インスペクターで設定)")]
    [SerializeField] private AudioClip titleBGM;
    [SerializeField] private AudioClip gameBGM;
    [SerializeField] private AudioClip ResultBGM;

    private AudioSource audioSource;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            audioSource = GetComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        Debug.Log($"[SoundManager] シーンが読み込まれました。シーン名:{ scene.name}");
        switch(scene.name)
        {
            case "TitleScene":
                PlayBGM(titleBGM);
                break;
            case "GameScene":
                PlayBGM(gameBGM);
                break;
            case "Result":
                PlayBGM(ResultBGM);
                break;
        }
    }

    private void PlayBGM(AudioClip clip)
    {
        if (clip == null || audioSource.clip == clip) return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }
}
