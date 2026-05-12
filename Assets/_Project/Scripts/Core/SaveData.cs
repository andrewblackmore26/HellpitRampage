using System;

namespace HellpitRampage.Core
{
    [Serializable]
    public class SaveData
    {
        public int SchemaVersion = 1;
        public PlayerProfile Profile = new PlayerProfile();
        public MetaProgress Meta = new MetaProgress();
        public CurrentRun Run = null;
    }

    [Serializable]
    public class PlayerProfile
    {
    }

    [Serializable]
    public class MetaProgress
    {
    }

    [Serializable]
    public class CurrentRun
    {
    }
}
