namespace TechC.SchoolGameJam2026.Core.Event
{
    /// <summary>
    /// SE（効果音）の再生を要求するイベントデータ
    /// Rule 10 準拠
    /// </summary>
    public readonly struct PlaySEEvent
    {
        /// <summary>
        /// 再生したいSEのインデックス番号
        /// </summary>
        public int SeIndex { get; }

        public PlaySEEvent(int seIndex)
        {
            SeIndex = seIndex;
        }
    }
}