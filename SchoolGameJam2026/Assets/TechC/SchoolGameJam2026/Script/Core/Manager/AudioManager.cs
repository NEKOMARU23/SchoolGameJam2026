
using TechC.SchoolGameJam2026.Script.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using TechC.SchoolGameJam2026.Core.Event;
namespace TechC.SchoolGameJam2026.Script.Core.Manager
{
    public class AudioManager: MonoBehaviour
    {
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
        public static AudioManager Instance { get; private set; }

        [Header("--オーディオソース--")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;

        [Header("--BGM 素材リスト--")]
        [SerializeField] private AudioClip[] bgmClips;

        [Header("--SE 素材リスト--")]
        [SerializeField] private AudioClip[] seClips;


        //現在の音量を保持するプロパティ
        public float BGMVolume => bgmSource.volume;
        public float SEVolume => seSource.volume;

        private void Awake()
        {

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                //初期の音量を設定
                SetBGMVolume(0.5f);
                SetSEVolume(0.5f);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        ///<summary>
        ///BGMを再生する(インデックス指定)
        /// </summary>
        public void PlayBGM(int index)
        {
            if (index < 0 || index >= bgmClips.Length) return;

            //すでに同じBGMが流れている場合は何もしない
            if (bgmSource.isPlaying && bgmSource.clip == bgmClips[index]) return;

            bgmSource.clip = bgmClips[index];
            bgmSource.loop = true;
            bgmSource.Play();
        }
        ///<summary>
        ///BGMを停止する
        /// </summary>
        public void StopBGM()
        {
            bgmSource.Stop();
        }

        ///<summary>
        ///SE(効果音)を再生する(インデックス指定)
        /// </summary>
        public void PlaySE(int index)
        {
            if (index < 0 || index >= seClips.Length) return;

            //PlayOneShotを使えば、１つのAudioSourceで複数のSEを重ねて再生できる
            seSource.PlayOneShot(seClips[index]);
        }

        ///<summary>
        ///BGMの音量を変更する（UIのスライダー等から呼び出す、値は0.0～1.0）
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            bgmSource.volume = Mathf.Clamp01(volume);
        }

        ///<summary>
        ///SEの音量を変更する（UIのスライダー等から呼び出す、値は０．０～１．０）
        /// </summary>
        public void SetSEVolume(float volume)
        {
            seSource.volume = Mathf.Clamp01(volume);
        }
    }
}