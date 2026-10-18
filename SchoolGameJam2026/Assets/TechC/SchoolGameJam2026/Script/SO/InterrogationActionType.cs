namespace TechC.SchoolGameJam2026.Scripts.SO
{
    /// <summary>
    /// 取り調べ時の行動種別（解放とAI確定を追加）
    /// </summary>
    public enum InterrogationActionType
    {
        Torture,    // 1. 拷問
        Talk,       // 2. 対話
        Negotiate,  // 3. 交渉
        HearAbout,  // 4. 他の人について聞き取り
        Release,    // 5. 解放
        ConfirmAI   // 6. AI確定（排除/判定）
    }
}
