using System.Collections.Generic;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    /// <summary>
    /// Stateless resolver under the v3 unified-target model: each star is evaluated independently,
    /// and a matching neighbor's tag fires conditional effects on either the starred item (Self)
    /// or the neighbor itself (Neighbor). Stacking falls out naturally — two stars both activated
    /// fire the effect twice.
    /// </summary>
    public static class SynergyResolver
    {
        public class Resolution
        {
            public Dictionary<int, ItemStatModifiers> Modifiers = new();
            // Behaviors keyed by RECIPIENT instance ID (the one whose attack count drives the trigger).
            public Dictionary<int, List<ConditionalEffect>> BehaviorsByRecipient = new();
            // Star tracking for visual overlay. Tuple = (starred item ID, star cell in rotated/normalized coords, rotated direction).
            public HashSet<(int starredID, Vector2Int cell, EdgeDirection dir)> ActiveStars = new();
        }

        public static Resolution Resolve(InventoryGrid grid)
        {
            var result = new Resolution();
            if (grid == null) return result;

            foreach (var starred in grid.Items)
            {
                if (starred?.Data == null) continue;
                if (starred.Data.StarredEdges == null || starred.Data.StarredEdges.Count == 0) continue;
                if (starred.Data.ConditionalEffects == null || starred.Data.ConditionalEffects.Count == 0) continue;

                foreach (var star in starred.EffectiveStarredEdges())
                {
                    Vector2Int absStarCell = starred.Origin + star.Cell;
                    Vector2Int targetCell = absStarCell + DirectionOffset(star.Direction);

                    if (!grid.IsCellInBounds(targetCell)) continue;
                    // Don't activate stars that target a cell of the starred item's own shape (defensive
                    // for multi-cell items with internal-facing stars; for 1×1 items this never fires).
                    if (starred.OccupiesCell(targetCell)) continue;

                    ItemInstance neighbor = grid.GetItemAt(targetCell);
                    if (neighbor == null) continue;
                    if (neighbor.InstanceID == starred.InstanceID) continue;
                    if (neighbor.Data?.Tags == null) continue;

                    foreach (var ce in starred.Data.ConditionalEffects)
                    {
                        if (ce == null) continue;
                        if (ce.ActivatorTag == ItemTag.None) continue;
                        if (!neighbor.Data.Tags.Contains(ce.ActivatorTag)) continue;

                        // This star fires for this conditional effect.
                        result.ActiveStars.Add((starred.InstanceID, star.Cell, star.Direction));

                        ItemInstance recipient = ce.Target == ConditionalEffectTarget.Self ? starred : neighbor;

                        if (ce.EffectType == ConditionalEffectType.Modifier)
                        {
                            ItemStatModifiers existing = result.Modifiers.TryGetValue(recipient.InstanceID, out var m)
                                ? m : ItemStatModifiers.Identity;
                            ItemStatModifiers delta = MakeModifierDelta(ce);
                            result.Modifiers[recipient.InstanceID] = existing.Combine(delta);
                        }
                        else if (ce.EffectType == ConditionalEffectType.Behavior)
                        {
                            if (!result.BehaviorsByRecipient.TryGetValue(recipient.InstanceID, out var list))
                            {
                                list = new List<ConditionalEffect>();
                                result.BehaviorsByRecipient[recipient.InstanceID] = list;
                            }
                            list.Add(ce);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Tooltip helper: returns the conditional effects on this starred item that are currently active
        /// (at least one of its stars faces a neighbor whose tags contain the effect's ActivatorTag).
        /// </summary>
        public static List<ConditionalEffect> GetActiveEffectsOn(ItemInstance starred, InventoryGrid grid)
        {
            var result = new List<ConditionalEffect>();
            if (starred?.Data?.ConditionalEffects == null || grid == null) return result;
            if (starred.Data.StarredEdges == null || starred.Data.StarredEdges.Count == 0) return result;

            var stars = starred.EffectiveStarredEdges();

            foreach (var ce in starred.Data.ConditionalEffects)
            {
                if (ce == null) continue;
                if (ce.ActivatorTag == ItemTag.None) continue;

                bool anyStarActive = false;
                foreach (var star in stars)
                {
                    Vector2Int absStarCell = starred.Origin + star.Cell;
                    Vector2Int targetCell = absStarCell + DirectionOffset(star.Direction);

                    if (!grid.IsCellInBounds(targetCell)) continue;
                    if (starred.OccupiesCell(targetCell)) continue;

                    var neighbor = grid.GetItemAt(targetCell);
                    if (neighbor == null || neighbor.InstanceID == starred.InstanceID) continue;
                    if (neighbor.Data?.Tags == null) continue;

                    if (neighbor.Data.Tags.Contains(ce.ActivatorTag))
                    {
                        anyStarActive = true;
                        break;
                    }
                }

                if (anyStarActive) result.Add(ce);
            }
            return result;
        }

        // ---------- Helpers ----------

        private static Vector2Int DirectionOffset(EdgeDirection dir) => dir switch
        {
            EdgeDirection.Up => new Vector2Int(0, 1),
            EdgeDirection.Down => new Vector2Int(0, -1),
            EdgeDirection.Left => new Vector2Int(-1, 0),
            EdgeDirection.Right => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };

        private static ItemStatModifiers MakeModifierDelta(ConditionalEffect ce)
        {
            var delta = ItemStatModifiers.Identity;
            switch (ce.ModifierKind)
            {
                case ModifierKind.DamageBonus:
                    delta.DamageBonus = ce.Magnitude;
                    break;
                case ModifierKind.CooldownReduction:
                    delta.CooldownMultiplier = 1f - ce.Magnitude;
                    break;
                case ModifierKind.RangeBonus:
                    delta.RangeBonus = ce.Magnitude;
                    break;
            }
            return delta;
        }
    }
}
