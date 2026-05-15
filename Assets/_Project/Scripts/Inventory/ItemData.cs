using System.Collections.Generic;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    public enum ItemRarity { Common, Uncommon, Rare, Legendary, Mythic }

    [CreateAssetMenu(fileName = "NewItem_Item", menuName = "HellpitRampage/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("WS-013: stable string identifier used by the save system. Snake_case. Must match the asset stem and be unique across all ItemData.")]
        [SerializeField] private string _id;
        public string Id => _id;

        [Tooltip("Display name shown in shop, inventory, codex.")]
        public string ItemName = "Unnamed Item";

        [Tooltip("Flavor / mechanical description.")]
        [TextArea(2, 4)]
        public string Description = "";

        [Tooltip("Icon shown in inventory grid and shop.")]
        public Sprite Icon;

        [Tooltip("WS-012.2: per-cell tint while real sprite art is pending. Multiplies with Icon if set; renders alone otherwise.")]
        public Color PlaceholderColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("The cell footprint of this item. Most starter items are 1x1.")]
        public ItemShape Shape;

        [Tooltip("Tags applied to this item. Recipients check the activator tag against a contributor's tag list; contributors expose their identity via these tags.")]
        public List<ItemTag> Tags = new();

        [Tooltip("Edges of this item that act as activation points for its ConditionalEffects. Empty on pure-contributor items.")]
        public List<StarredEdge> StarredEdges = new();

        [Tooltip("Effects this item performs when a contributor with the matching ActivatorTag is adjacent to any of its stars.")]
        public List<ConditionalEffect> ConditionalEffects = new();

        [Header("Economy")]
        public ItemRarity Rarity = ItemRarity.Common;

        [Tooltip("Price to buy. If 0, derived from Rarity.")]
        public int BasePrice = 0;

        public int EffectivePrice => BasePrice > 0 ? BasePrice : DefaultPriceForRarity(Rarity);

        public static int DefaultPriceForRarity(ItemRarity r) => r switch
        {
            ItemRarity.Common => 1,
            ItemRarity.Uncommon => 3,
            ItemRarity.Rare => 6,
            ItemRarity.Legendary => 10,
            ItemRarity.Mythic => 18,
            _ => 1
        };

        [Header("Weapon behavior (null/zero for non-weapon items)")]
        [Tooltip("Projectile prefab to spawn when firing. Null means this item is not a weapon.")]
        public GameObject ProjectilePrefab;

        [Tooltip("Seconds between fires.")]
        public float Cooldown = 1f;

        [Tooltip("Max distance the auto-aim will search for a target.")]
        public float Range = 8f;

        [Tooltip("Projectile travel speed in units/second.")]
        public float ProjectileSpeed = 10f;

        [Tooltip("Maximum lifetime of a fired projectile before auto-despawn.")]
        public float ProjectileLifetime = 3f;

        [Tooltip("Damage applied to a target on projectile contact.")]
        public float Damage = 1f;
    }
}
