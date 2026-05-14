namespace HellpitRampage.Core
{
    // WS-012.5: published when RunManager transitions to RunPhase.Shop at the end of a combat
    // round. Consumed by GroundManager (activate GroundArea), DragModeService (reset to Items),
    // and any other shop-only UI that needs an explicit "shop is open" signal.
    public struct ShopPhaseStartedEvent : IGameEvent
    {
        public int RoundNumber;
    }
}
