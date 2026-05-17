namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-014.B: published when a round-start companion appearance has finished — its
    /// grace period elapsed, the player dismissed it, or the round is a silent one
    /// (23-24). The combat orchestrator waits for this before activating waves / the
    /// boss, so the companion beat always plays out before enemies appear.
    /// </summary>
    public struct CompanionAppearanceCompleteEvent : IGameEvent { }
}
