namespace HellpitRampage.Inventory
{
    /// <summary>
    /// Tags applied to items. Used by ConditionalEffect.ActivatorTag to match contributors,
    /// and as a generic identifier other systems can key off later. Stable backing integers —
    /// never renumber a value; only append new ones at the end.
    /// </summary>
    public enum ItemTag
    {
        None = 0,
        Weapon = 1,
        Charm = 2,
        Sharpening = 3,
        Tempo = 4,
        Sustain = 5
        // 6 (Source) was used in WS-011 and removed in WS-011.5. Do not re-use the integer 6
        // for a new tag — any stale Source value left in an asset will silently deserialize
        // to the new enum entry at integer 6 if one is added.
    }
}
