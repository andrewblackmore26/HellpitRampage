namespace HellpitRampage.Core
{
    /// <summary>
    /// Fired by <see cref="HellpitRampage.Inventory.SynergyService"/> after every Recompute.
    /// Visual subscribers (StarIndicatorOverlay, tooltip refresh) listen to repaint.
    /// </summary>
    public struct SynergyChangedEvent : IGameEvent { }
}
