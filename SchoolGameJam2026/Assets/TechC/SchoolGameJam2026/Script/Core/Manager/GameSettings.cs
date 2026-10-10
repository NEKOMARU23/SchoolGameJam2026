using System;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary> 文字送りの速さ </summary>
    public enum TextSpeed
    {
        Slow,
        Normal,
        Fast
    }

    /// <summary> オートで次のページへ進むまでの待ち時間 </summary>
    public enum AutoWait
    {
        Short,
        Normal,
        Long
    }

    /// <summary> スキップで飛ばす会話の範囲 </summary>
    public enum SkipMode
    {
        ReadOnly,   // 一度読んだ会話だけ
        All         // まだ読んでいない会話も
    }

    /// <summary>
    /// オプションの設定内容（セーブ枠とは別に保存し、すべてのセーブで共通）
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public float bgmVolume = 0.5f;
        public float seVolume = 0.5f;
        public bool isFullScreen = true;
        public TextSpeed textSpeed = TextSpeed.Normal;
        public AutoWait autoWait = AutoWait.Normal;
        public SkipMode skipMode = SkipMode.ReadOnly;

        /// <summary> 1文字を表示する間隔（秒） </summary>
        public float TextIntervalSeconds
        {
            get
            {
                switch (textSpeed)
                {
                    case TextSpeed.Slow: return 0.08f;
                    case TextSpeed.Fast: return 0.025f;
                    default: return 0.05f;
                }
            }
        }

        /// <summary> オートで次のページへ進むまでの待ち時間（秒） </summary>
        public float AutoWaitSeconds
        {
            get
            {
                switch (autoWait)
                {
                    case AutoWait.Short: return 0.5f;
                    case AutoWait.Long: return 2.5f;
                    default: return 1.2f;
                }
            }
        }

        public GameSettings Clone()
        {
            return (GameSettings)MemberwiseClone();
        }
    }
}
