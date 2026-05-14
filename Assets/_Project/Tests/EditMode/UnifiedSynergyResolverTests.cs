using System.Collections.Generic;
using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// v3 resolver tests. Covers the unified Target=Self / Target=Neighbor model with
    /// per-star activation. Stacking is now expected: two stars matching → effect applied
    /// twice (was forbidden in the v2 recipient-only model).
    /// </summary>
    public class UnifiedSynergyResolverTests
    {
        // ---------------- Helpers ----------------

        private static ItemShape MakeShape(params (int x, int y)[] cells)
        {
            var shape = ScriptableObject.CreateInstance<ItemShape>();
            foreach (var c in cells) shape.Cells.Add(new Vector2Int(c.x, c.y));
            return shape;
        }

        private static BagData MakeBag(string name, ItemShape shape)
        {
            var bag = ScriptableObject.CreateInstance<BagData>();
            bag.BagName = name;
            bag.Shape = shape;
            return bag;
        }

        private static ItemData MakeItem(string name, ItemShape shape, ItemTag[] tags,
            StarredEdge[] stars = null, ConditionalEffect[] effects = null)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.ItemName = name;
            item.Shape = shape;
            item.Tags = new List<ItemTag>(tags ?? System.Array.Empty<ItemTag>());
            item.StarredEdges = stars != null ? new List<StarredEdge>(stars) : new List<StarredEdge>();
            item.ConditionalEffects = effects != null ? new List<ConditionalEffect>(effects) : new List<ConditionalEffect>();
            return item;
        }

        private static StarredEdge Star(int x, int y, EdgeDirection dir) =>
            new() { Cell = new Vector2Int(x, y), Direction = dir };

        private static ConditionalEffect ModifierEffect(ItemTag activator, ConditionalEffectTarget target,
            ModifierKind kind, float magnitude, string name = "M") =>
            new()
            {
                ActivatorTag = activator,
                Target = target,
                EffectType = ConditionalEffectType.Modifier,
                ModifierKind = kind,
                Magnitude = magnitude,
                DisplayName = name
            };

        private static ConditionalEffect BehaviorEffect(ItemTag activator, ConditionalEffectTarget target,
            int triggerCount, BehaviorAction action, string name = "B") =>
            new()
            {
                ActivatorTag = activator,
                Target = target,
                EffectType = ConditionalEffectType.Behavior,
                TriggerCount = triggerCount,
                BehaviorAction = action,
                DisplayName = name
            };

        private static ItemShape Shape1x1() => MakeShape((0, 0));
        private static ItemShape Shape1x2H() => MakeShape((0, 0), (1, 0));
        private static ItemShape Shape3x3() => MakeShape(
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2));

        // ---------------- 1. No starred items → empty ----------------

        [Test]
        public void Resolve_NoStarredItems_ReturnsEmpty()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Plain", Shape1x1(), new[] { ItemTag.Weapon }), new Vector2Int(1, 1));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(0, r.Modifiers.Count);
            Assert.AreEqual(0, r.BehaviorsByRecipient.Count);
            Assert.AreEqual(0, r.ActiveStars.Count);
        }

        // ---------------- 2. Star → matching Neighbor → applies to neighbor ----------------

        [Test]
        public void Resolve_StarFacingMatchingTag_TargetNeighbor_ModifierApplied()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Whet", Shape1x1(), System.Array.Empty<ItemTag>(),
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: new[] { ModifierEffect(ItemTag.Weapon, ConditionalEffectTarget.Neighbor, ModifierKind.DamageBonus, 1f) }),
                new Vector2Int(0, 0));
            var neighbor = grid.PlaceItem(MakeItem("Knife", Shape1x1(), new[] { ItemTag.Weapon }),
                new Vector2Int(1, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.IsTrue(r.Modifiers.ContainsKey(neighbor.InstanceID), "Modifier should land on the NEIGHBOR's InstanceID.");
            Assert.AreEqual(1f, r.Modifiers[neighbor.InstanceID].DamageBonus, 1e-4);
        }

        // ---------------- 3. Star → matching Neighbor → Target=Self applies to starred ----------------

        [Test]
        public void Resolve_StarFacingMatchingTag_TargetSelf_ModifierApplied()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var sword = grid.PlaceItem(MakeItem("MysticSword", Shape1x1(), new[] { ItemTag.Weapon },
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: new[] { ModifierEffect(ItemTag.Sharpening, ConditionalEffectTarget.Self, ModifierKind.DamageBonus, 3f) }),
                new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Sharp", Shape1x1(), new[] { ItemTag.Sharpening }),
                new Vector2Int(1, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.IsTrue(r.Modifiers.ContainsKey(sword.InstanceID), "Target=Self should write to the STARRED item's InstanceID.");
            Assert.AreEqual(3f, r.Modifiers[sword.InstanceID].DamageBonus, 1e-4);
        }

        // ---------------- 4. Wrong tag → no activation ----------------

        [Test]
        public void Resolve_StarFacingWrongTag_NoActivation()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Whet", Shape1x1(), System.Array.Empty<ItemTag>(),
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: new[] { ModifierEffect(ItemTag.Weapon, ConditionalEffectTarget.Neighbor, ModifierKind.DamageBonus, 1f) }),
                new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Bottle", Shape1x1(), new[] { ItemTag.Sustain }),
                new Vector2Int(1, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(0, r.Modifiers.Count);
            Assert.AreEqual(0, r.ActiveStars.Count);
        }

        // ---------------- 5. Two stars + Target=Self → stacks on starred (×2) ----------------

        [Test]
        public void Resolve_TwoStarsBothActive_TargetSelf_StacksOnSelf()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var sword = grid.PlaceItem(MakeItem("Sword", Shape1x1(), new[] { ItemTag.Weapon },
                stars: new[] { Star(0, 0, EdgeDirection.Left), Star(0, 0, EdgeDirection.Right) },
                effects: new[] { ModifierEffect(ItemTag.Sharpening, ConditionalEffectTarget.Self, ModifierKind.DamageBonus, 3f) }),
                new Vector2Int(1, 1));
            grid.PlaceItem(MakeItem("SharpL", Shape1x1(), new[] { ItemTag.Sharpening }), new Vector2Int(0, 1));
            grid.PlaceItem(MakeItem("SharpR", Shape1x1(), new[] { ItemTag.Sharpening }), new Vector2Int(2, 1));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(6f, r.Modifiers[sword.InstanceID].DamageBonus, 1e-4,
                "Two stars matching Target=Self should stack the magnitude (3 + 3 = 6).");
            Assert.AreEqual(2, r.ActiveStars.Count, "Both stars should be tracked active.");
        }

        // ---------------- 6. Two stars + Target=Neighbor → each neighbor gets one ----------------

        [Test]
        public void Resolve_TwoStarsBothActive_TargetNeighbor_AppliesToBothNeighbors()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Whet", Shape1x1(), System.Array.Empty<ItemTag>(),
                stars: new[] { Star(0, 0, EdgeDirection.Left), Star(0, 0, EdgeDirection.Right) },
                effects: new[] { ModifierEffect(ItemTag.Weapon, ConditionalEffectTarget.Neighbor, ModifierKind.DamageBonus, 1f) }),
                new Vector2Int(1, 1));
            var knifeL = grid.PlaceItem(MakeItem("KnifeL", Shape1x1(), new[] { ItemTag.Weapon }), new Vector2Int(0, 1));
            var knifeR = grid.PlaceItem(MakeItem("KnifeR", Shape1x1(), new[] { ItemTag.Weapon }), new Vector2Int(2, 1));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(1f, r.Modifiers[knifeL.InstanceID].DamageBonus, 1e-4, "Left neighbor gets +1.");
            Assert.AreEqual(1f, r.Modifiers[knifeR.InstanceID].DamageBonus, 1e-4, "Right neighbor gets +1.");
            Assert.AreEqual(2, r.ActiveStars.Count);
        }

        // ---------------- 7. Star pointing out of grid bounds → no activation, no error ----------------

        [Test]
        public void Resolve_StarOutOfBounds_NoActivation()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape1x1()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Whet", Shape1x1(), System.Array.Empty<ItemTag>(),
                stars: new[] { Star(0, 0, EdgeDirection.Left) }, // points off-grid at (-1, 0)
                effects: new[] { ModifierEffect(ItemTag.Weapon, ConditionalEffectTarget.Neighbor, ModifierKind.DamageBonus, 1f) }),
                new Vector2Int(0, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(0, r.Modifiers.Count);
            Assert.AreEqual(0, r.ActiveStars.Count);
        }

        // ---------------- 8. Star pointing into starred item's own shape → no activation ----------------

        [Test]
        public void Resolve_StarPointingIntoOwnShape_NoActivation()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            // 1×2 horizontal item with star at (0,0) pointing Right → faces (1,0), which is THE ITEM'S OWN second cell.
            grid.PlaceItem(MakeItem("Stick", Shape1x2H(), new[] { ItemTag.Weapon },
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: new[] { ModifierEffect(ItemTag.Weapon, ConditionalEffectTarget.Self, ModifierKind.DamageBonus, 1f) }),
                new Vector2Int(0, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(0, r.Modifiers.Count, "Stars facing the starred item's own cells must not activate.");
            Assert.AreEqual(0, r.ActiveStars.Count);
        }

        // ---------------- 9. Stars but no conditional effects → no activation ----------------

        [Test]
        public void Resolve_ItemWithStarsButNoConditionalEffects_NoActivation()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Decor", Shape1x1(), System.Array.Empty<ItemTag>(),
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: null),
                new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Knife", Shape1x1(), new[] { ItemTag.Weapon }), new Vector2Int(1, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(0, r.Modifiers.Count);
            Assert.AreEqual(0, r.ActiveStars.Count, "An item with stars but no effects should never light a star.");
        }

        // ---------------- 10. Effects but no stars → no activation ----------------

        [Test]
        public void Resolve_ItemWithConditionalEffectsButNoStars_NoActivation()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Ghost", Shape1x1(), System.Array.Empty<ItemTag>(),
                stars: null,
                effects: new[] { ModifierEffect(ItemTag.Weapon, ConditionalEffectTarget.Neighbor, ModifierKind.DamageBonus, 1f) }),
                new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Knife", Shape1x1(), new[] { ItemTag.Weapon }), new Vector2Int(1, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.AreEqual(0, r.Modifiers.Count, "Effects without stars are unreachable.");
        }

        // ---------------- 11. Behavior effect → recorded by recipient, not in Modifiers ----------------

        [Test]
        public void Resolve_BehaviorEffect_RecordedSeparately()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var sword = grid.PlaceItem(MakeItem("Sword", Shape1x1(), new[] { ItemTag.Weapon },
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: new[] { BehaviorEffect(ItemTag.Tempo, ConditionalEffectTarget.Self, 5, BehaviorAction.ExtraProjectile) }),
                new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Tempo", Shape1x1(), new[] { ItemTag.Tempo }), new Vector2Int(1, 0));

            var r = SynergyResolver.Resolve(grid);

            Assert.IsFalse(r.Modifiers.ContainsKey(sword.InstanceID),
                "Behavior-only activation must not pollute the Modifiers map.");
            Assert.IsTrue(r.BehaviorsByRecipient.ContainsKey(sword.InstanceID));
            Assert.AreEqual(1, r.BehaviorsByRecipient[sword.InstanceID].Count);
        }

        // ---------------- 12. ActiveStars set captures every (starred, cell, dir) tuple that fired ----------------

        [Test]
        public void Resolve_MultipleStarsAndEffects_AllActiveStarsTracked()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var sword = grid.PlaceItem(MakeItem("Sword", Shape1x1(), new[] { ItemTag.Weapon },
                stars: new[] { Star(0, 0, EdgeDirection.Left), Star(0, 0, EdgeDirection.Right) },
                effects: new[] { ModifierEffect(ItemTag.Sharpening, ConditionalEffectTarget.Self, ModifierKind.DamageBonus, 3f) }),
                new Vector2Int(1, 1));
            grid.PlaceItem(MakeItem("SharpL", Shape1x1(), new[] { ItemTag.Sharpening }), new Vector2Int(0, 1));
            grid.PlaceItem(MakeItem("SharpR", Shape1x1(), new[] { ItemTag.Sharpening }), new Vector2Int(2, 1));

            var r = SynergyResolver.Resolve(grid);

            Assert.IsTrue(r.ActiveStars.Contains((sword.InstanceID, new Vector2Int(0, 0), EdgeDirection.Left)));
            Assert.IsTrue(r.ActiveStars.Contains((sword.InstanceID, new Vector2Int(0, 0), EdgeDirection.Right)));
            Assert.AreEqual(2, r.ActiveStars.Count);
        }
    }
}
