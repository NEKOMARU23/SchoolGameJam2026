using TechC.SchoolGameJam2026.Core.Event;
using TechC.SchoolGameJam2026.Script.UI;
using UnityEngine;
using UnityEngine.InputSystem; // 新しい Input System の参照を追加

namespace TechC.SchoolGameJam2026.Script.Core.Manager
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("--オーディオソース--")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;

        [Header("--BGM 素材リスト--")]
        [SerializeField] private AudioClip[] bgmClips;

        [Header("--SE 素材リスト--")]
        [SerializeField] private AudioClip[] seClips;

        // 現在の音量を保持するプロパティ
        public float BGMVolume => bgmSource != null ? bgmSource.volume : 0f;
        public float SEVolume => seSource != null ? seSource.volume : 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // 初期の音量を設定
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
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<PlaySEEvent>(OnPlaySEEvent);
        }

        private void OnPlaySEEvent(PlaySEEvent evt)
        {
            PlaySE(evt.SeIndex);
        }

        /// <summary>
        /// BGMを再生する(インデックス指定)
        /// </summary>
        public void PlayBGM(int index)
        {
            if (bgmClips == null || index < 0 || index >= bgmClips.Length) return;

            // すでに同じBGMが流れている場合は何もしない
            if (bgmSource.isPlaying && bgmSource.clip == bgmClips[index]) return;

            bgmSource.clip = bgmClips[index];
            bgmSource.loop = true;
            bgmSource.Play();
        }

        /// <summary>
        /// BGMを停止する
        /// </summary>
        public void StopBGM()
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
            }
        }

        /// <summary>
        /// SE(効果音)を再生する(インデックス指定)
        /// </summary>
        public void PlaySE(int index)
        {
            if (seClips == null || index < 0 || index >= seClips.Length) return;

            // PlayOneShotを使えば、１つのAudioSourceで複数のSEを重ねて再生できる
            seSource.PlayOneShot(seClips[index]);
        }

        /// <summary>
        /// BGMの音量を変更する（UIのスライダー等から呼び出す、値は0.0～1.0）
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            if (bgmSource != null)
            {
                bgmSource.volume = Mathf.Clamp01(volume);
            }
        }

        /// <summary>
        /// SEの音量を変更する（UIのスライダー等から呼び出す、値は0.0～1.0）
        /// </summary>
        public void SetSEVolume(float volume)
        {
            if (seSource != null)
            {
                seSource.volume = Mathf.Clamp01(volume);
            }
        }
    }
}