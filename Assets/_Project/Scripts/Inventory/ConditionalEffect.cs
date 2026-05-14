using UnityEngine;

namespace HellpitRampage.Inventory
{
    public enum ConditionalEffectType { Modifier = 0, Behavior = 1 }
    public enum ConditionalEffectTarget { Self = 0, Neighbor = 1 }
    public enum ModifierKind { DamageBonus = 0, CooldownReduction = 1, RangeBonus = 2 }
    public enum BehaviorAction { ExtraProjectile = 0, AoEPulse = 1, HealingBurst = 2 }

    /// <summary>
    /// One conditional effect declared on a starred item: "when a neighbor with ActivatorTag
    /// occupies the cell facing one of my stars, apply this effect to the Target."
    /// Target = Self → effect applies to the starred item. Target = Neighbor → effect applies
    /// to the matching adjacent item (Brotato-style cross-buff).
    /// </summary>
    [System.Serializable]
    public class ConditionalEffect
    {
        [Tooltip("Tag a neighbor must have for this effect to activate.")]
        public ItemTag ActivatorTag;

        [Tooltip("Self = effect applies to THIS starred item. Neighbor = effect applies to the matching adjacent item.")]
        public ConditionalEffectTarget Target;

        [Tooltip("Modifier = stat change. Behavior = added action every Nth base attack of the recipient.")]
        public ConditionalEffectType EffectType;

        [Header("Modifier (when EffectType == Modifier)")]
        public ModifierKind ModifierKind;

        [Tooltip("Modifier magnitude: flat for damage/range, fractional for cooldown (0.15 = 15% faster). Behavior actions use BehaviorMagnitude below.")]
        public float Magnitude = 1f;

        [Header("Behavior (when EffectType == Behavior)")]
        [Tooltip("Behavior triggers every Nth base attack.")]
        public int TriggerCount = 5;

        public BehaviorAction BehaviorAction;

        [Tooltip("Action-specific magnitude. Ignored by ExtraProjectile; would be heal amount for HealingBurst, damage for AoEPulse.")]
        public float BehaviorMagnitude;

        [Header("Display")]
        public string DisplayName = "";

        [TextArea(2, 3)]
        public string Description = "";
    }
}
