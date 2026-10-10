using System;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// スマホ（補助ちゃん）との会話回数を管理するマネージャー
    /// 1日に聞ける回数はその日に用意されたセリフの数まで。日付が変わる・その日をリセットすると0回に戻る
    /// </summary>
    public class SmartphoneTalkManager : MonoBehaviour
    {
        public static SmartphoneTalkManager Instance { get; private set; }

        private int talkCountToday = 0;

        /// <summary> 今日すでに会話した回数 </summary>
        public int TalkCountToday => talkCountToday;

        /// <summary> 会話回数が変わったときに通知（引数は今日の会話回数） </summary>
        public event Action<int> OnTalkCountChanged;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // GameManager の Awake 完了後に購読する
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayStarted += HandleDayStarted;
                GameManager.Instance.OnDayReset += ResetTalkCount;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayStarted -= HandleDayStarted;
                GameManager.Instance.OnDayReset -= ResetTalkCount;
            }

            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 今日まだ会話できるか
        /// </summary>
        /// <param name="dailyLimit">その日に聞ける回数（用意されたセリフの数）</param>
        public bool CanTalk(int dailyLimit)
        {
            return talkCountToday < dailyLimit;
        }

        /// <summary>
        /// 会話を1回分消費する
        /// </summary>
        /// <param name="dailyLimit">その日に聞ける回数（用意されたセリフの数）</param>
        /// <param name="talkNumber">今日何回目の会話か（1〜）</param>
        /// <returns>回数が残っていて会話を始められた場合 true</returns>
        public bool TryStartTalk(int dailyLimit, out int talkNumber)
        {
            if (!CanTalk(dailyLimit))
            {
                talkNumber = 0;
                return false;
            }

            talkCountToday++;
            talkNumber = talkCountToday;
            OnTalkCountChanged?.Invoke(talkCountToday);
            return true;
        }

        private void HandleDayStarted(int day)
        {
            ResetTalkCount();
        }

        /// <summary>
        /// 会話回数を0に戻す（翌日・その日のリセット時。朝の時点では常に0回のため）
        /// </summary>
        private void ResetTalkCount()
        {
            talkCountToday = 0;
            OnTalkCountChanged?.Invoke(talkCountToday);
        }
    }
}
