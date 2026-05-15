using System;
using System.Collections.Generic;

namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-013: between-rounds run save. Captured on ShopPhaseStartedEvent. References content by
    /// stable string Id (resolved via DataRegistry on load) so saves survive asset reshuffles.
    /// </summary>
    [Serializable]
    public class RunSaveData
    {
        public int SaveFormatVersion = 1;
        public string GameVersion;
        public string SavedAtUtc;

        public int CurrentRound;
        public string HeroId;
        public int Gold;
        public float PlayerCurrentHp;
        public float PlayerMaxHp;

        public List<BagSaveEntry> Bags = new();
        public List<ItemSaveEntry> Items = new();
        public List<GroundItemSaveEntry> GroundItems = new();
    }

    [Serializable]
    public class BagSaveEntry
    {
        public string BagId;
        public int OriginX;
        public int OriginY;
        public bool IsLocked;
    }

    [Serializable]
    public class ItemSaveEntry
    {
        public string ItemId;
        public int OriginX;
        public int OriginY;
        public int Rotation; // raw enum int (0/90/180/270 — see Rotation.cs)
        public bool IsLocked;
    }

    [Serializable]
    public class GroundItemSaveEntry
    {
        public string ItemId;
        public int Rotation;
        public bool IsLocked;
    }
}
