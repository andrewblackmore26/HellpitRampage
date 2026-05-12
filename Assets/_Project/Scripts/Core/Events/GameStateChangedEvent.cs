namespace HellpitRampage.Core
{
    public struct GameStateChangedEvent : IGameEvent
    {
        public GameManager.GameState OldState;
        public GameManager.GameState NewState;
    }
}
