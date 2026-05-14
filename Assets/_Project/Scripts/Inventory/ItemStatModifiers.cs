namespace HellpitRampage.Inventory
{
    public struct ItemStatModifiers
    {
        public float DamageBonus;
        public float CooldownMultiplier;
        public float RangeBonus;

        public static ItemStatModifiers Identity => new() { DamageBonus = 0f, CooldownMultiplier = 1f, RangeBonus = 0f };

        public ItemStatModifiers Combine(ItemStatModifiers other) => new()
        {
            DamageBonus = DamageBonus + other.DamageBonus,
            CooldownMultiplier = CooldownMultiplier * other.CooldownMultiplier,
            RangeBonus = RangeBonus + other.RangeBonus
        };
    }
}
