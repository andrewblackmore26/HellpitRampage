namespace HellpitRampage.Core
{
    // WS-013: emitted by SaveManager after a successful auto-save at shop-phase entry.
    public struct RunSaveCompletedEvent : IGameEvent { }

    // WS-013: emitted when SaveManager fails to write the run save. Reason is the exception
    // message (disk full, permissions, antivirus, file locked).
    public struct RunSaveFailedEvent : IGameEvent
    {
        public string Reason;
    }

    // WS-013: emitted by RunRestoreController after a successful run restore. Useful for any
    // future system that wants to suppress an intro line / round-start dialogue replay etc.
    public struct RunResumedEvent : IGameEvent { }
}
