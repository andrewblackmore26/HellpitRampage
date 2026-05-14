using System.Text;
using HellpitRampage.Inventory;
using UnityEngine;

namespace HellpitRampage.UI
{
    public struct TooltipContent
    {
        public string Title;
        public string RarityLabel;
        public Color RarityColor;
        public string StatLines;
        public string Description;
        public string SynergiesText; // re-purposed: now holds the conditional-effects list

        public static TooltipContent FromItem(ItemData item)
        {
            if (item == null) return default;
            string stats = item.ProjectilePrefab != null
                ? $"DMG: {item.Damage}\nCooldown: {item.Cooldown:0.0}s\nRange: {item.Range}\nSpeed: {item.ProjectileSpeed}"
                : "(Effect pending)";

            string effectsText = BuildEffectsText(item, activeEffects: null);

            return new TooltipContent
            {
                Title = item.ItemName,
                RarityLabel = item.Rarity.ToString(),
                RarityColor = ShopSlot.ColorForRarity(item.Rarity),
                StatLines = stats,
                Description = item.Description,
                SynergiesText = effectsText
            };
        }

        public static TooltipContent FromBag(BagData bag)
        {
            if (bag == null) return default;
            int w = bag.Shape != null ? bag.Shape.BoundingWidth : 0;
            int h = bag.Shape != null ? bag.Shape.BoundingHeight : 0;
            return new TooltipContent
            {
                Title = bag.BagName + " (Bag)",
                RarityLabel = bag.Rarity.ToString(),
                RarityColor = ShopSlot.ColorForRarity(bag.Rarity),
                StatLines = $"Shape: {w}x{h}",
                Description = bag.PassiveDescription
            };
        }

        /// <summary>
        /// Like <see cref="FromItem"/> but marks currently-active conditional effects with a
        /// distinct prefix so the player can see which entries are firing right now.
        /// </summary>
        public static TooltipContent FromItemInstance(ItemInstance instance, InventoryGrid grid)
        {
            if (instance == null) return default;
            TooltipContent content = FromItem(instance.Data);

            if (grid != null && instance.Data?.ConditionalEffects != null && instance.Data.ConditionalEffects.Count > 0)
            {
                var active = SynergyResolver.GetActiveEffectsOn(instance, grid);
                content.SynergiesText = BuildEffectsText(instance.Data, active);
            }
            return content;
        }

        private static string BuildEffectsText(ItemData item, System.Collections.Generic.List<ConditionalEffect> activeEffects)
        {
            if (item?.ConditionalEffects == null || item.ConditionalEffects.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("Conditional Effects:");
            foreach (var ce in item.ConditionalEffects)
            {
                if (ce == null) continue;
                bool isActive = activeEffects != null && activeEffects.Contains(ce);
                sb.Append(isActive ? "> " : "- ");
                string text = string.IsNullOrEmpty(ce.Description) ? ce.DisplayName : ce.Description;
                sb.AppendLine(text);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
