using TechC.SchoolGameJam2026.Scripts.Core.Event;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// BGMおよびSEの再生・音量を一括管理するマネージャー
    /// Rule 10 準拠
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("-- オーディオソース --")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;

        [Header("-- BGM 素材リスト --")]
        [SerializeField] private AudioClip[] bgmClips;

        [Header("-- SE 素材リスト --")]
        [SerializeField] private AudioClip[] seClips;

        [Header("-- シーン別BGM設定 --")]
        [SerializeField] private AudioClip titleBGM;
        [SerializeField] private AudioClip gameBGM;
        [SerializeField] private AudioClip resultBGM;

        public float BGMVolume => bgmSource != null ? bgmSource.volume : 0f;
        public float SEVolume => seSource != null ? seSource.volume : 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                SetBGMVolume(0.5f);
                SetSEVolume(0.5f);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            EventManager.Subscribe<PlaySEEvent>(OnPlaySEEvent);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<PlaySEEvent>(OnPlaySEEvent);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            // Manager シーンは最初のシーンより後に Additive ロードされるため、
            // 起動時のシーンの sceneLoaded は受け取れない。ここで初回分を再生する。
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch (scene.name)
            {
                case "Title":
                    PlayBGMClip(titleBGM);
                    break;
                case "InGame":
                    PlayBGMClip(gameBGM);
                    break;
                case "Result":
                    PlayBGMClip(resultBGM);
                    break;
            }
        }

        private void OnPlaySEEvent(PlaySEEvent evt)
        {
            PlaySE(evt.SeIndex);
        }

        public void PlayBGM(int index)
        {
            if (bgmClips == null || index < 0 || index >= bgmClips.Length) return;
            PlayBGMClip(bgmClips[index]);
        }

        public void PlayBGMClip(AudioClip clip)
        {
            if (clip == null || bgmSource == null || (bgmSource.isPlaying && bgmSource.clip == clip)) return;

            bgmSource.Stop();
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
            }
        }

        public void PlaySE(int index)
        {
            if (seSource == null || seClips == null || index < 0 || index >= seClips.Length) return;
            seSource.PlayOneShot(seClips[index]);
        }

        /// <summary>
        /// 指定した音を SE として鳴らす（Excel の本文シートの SE 列など、名前から探した音を鳴らすとき）
        /// </summary>
        public void PlaySE(AudioClip clip)
        {
            if (seSource == null || clip == null) return;
            seSource.PlayOneShot(clip);
        }

        public void SetBGMVolume(float volume)
        {
            if (bgmSource != null)
            {
                bgmSource.volume = Mathf.Clamp01(volume);
            }
        }

        public void SetSEVolume(float volume)
        {
            if (seSource != null)
            {
                seSource.volume = Mathf.Clamp01(volume);
            }
        }
    }
}