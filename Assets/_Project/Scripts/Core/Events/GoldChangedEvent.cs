namespace HellpitRampage.Core
{
    public struct GoldChangedEvent : IGameEvent
    {
        public int OldAmount;
        public int NewAmount;
    }
}
