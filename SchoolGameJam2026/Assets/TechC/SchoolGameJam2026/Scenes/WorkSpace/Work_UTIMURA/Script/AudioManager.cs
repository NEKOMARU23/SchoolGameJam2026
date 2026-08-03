
using Unity.VisualScripting;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scene.WorkSpace.Work_UTIMURA.Script
{
    public class AudioManager: MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("--オーディオソース--")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;

        [Header("--BGM 素材リスト--")]
        [SerializeField] private AudioClip[] bgmClips;

        [Header("--SE 素材リスト--")]
        [SerializeField] private AudioClip[] seClips;

        private void Awake()
        {

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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
    }
}