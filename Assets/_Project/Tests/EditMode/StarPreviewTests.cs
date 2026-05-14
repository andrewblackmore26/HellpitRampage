using System.Collections.Generic;
using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.1 fix-pass: stars must follow a dragged item AND light up when a matching
    /// contributor is in their target cell mid-drag. <see cref="StarIndicatorOverlay"/>
    /// implements this by temporarily mutating the dragged item's Origin/Rotation, calling
    /// <see cref="SynergyResolver.Resolve"/>, then restoring. These tests pin the resolver
    /// semantics under that mutation — if Resolve ever started caching item positions or
    /// firing events as a side effect, these would fail.
    /// </summary>
    public class StarPreviewTests
    {
        // ---------- Same helpers as UnifiedSynergyResolverTests ----------

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

        private static ConditionalEffect Mod(ItemTag activator, ConditionalEffectTarget target) =>
            new()
            {
                ActivatorTag = activator,
                Target = target,
                EffectType = ConditionalEffectType.Modifier,
                ModifierKind = ModifierKind.DamageBonus,
                Magnitude = 1f,
                DisplayName = "Test"
            };

        private static ItemShape Shape1x1() => MakeShape((0, 0));
        private static ItemShape Shape3x3() => MakeShape(
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2));

        // ---------- Tests ----------

        [Test]
        public void PreviewMutation_DraggedWeaponEntersStarTarget_StarActivates()
        {
            // Whetstone at (0,0) with a star pointing Right → target (1,0). No initial neighbor → idle.
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var whetstoneData = MakeItem("Whetstone", Shape1x1(), new[] { ItemTag.Sharpening },
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: new[] { Mod(ItemTag.Weapon, ConditionalEffectTarget.Neighbor) });
            grid.PlaceItem(whetstoneData, new Vector2Int(0, 0));

            // Weapon initially at (2, 0) — NOT adjacent.
            var weaponData = MakeItem("Knife", Shape1x1(), new[] { ItemTag.Weapon });
            var weapon = grid.PlaceItem(weaponData, new Vector2Int(2, 0));

            // Committed state: no star active.
            var committed = SynergyResolver.Resolve(grid);
            Assert.AreEqual(0, committed.ActiveStars.Count);

            // Simulate the preview: mutate weapon to (1,0) (the star's target), resolve, then restore.
            Vector2Int savedOrigin = weapon.Origin;
            Rotation savedRotation = weapon.Rotation;
            try
            {
                weapon.Origin = new Vector2Int(1, 0);
                var preview = SynergyResolver.Resolve(grid);
                Assert.AreEqual(1, preview.ActiveStars.Count,
                    "Preview should activate the whetstone's star when a Weapon enters its target cell.");
                Assert.IsTrue(
                    preview.ActiveStars.Contains((1, new Vector2Int(0, 0), EdgeDirection.Right)),
                    "Active-star tuple should identify Whetstone (id=1) at star (0,0)/Right.");
            }
            finally
            {
                weapon.Origin = savedOrigin;
                weapon.Rotation = savedRotation;
            }

            // After restore: committed state is unchanged.
            var afterRestore = SynergyResolver.Resolve(grid);
            Assert.AreEqual(0, afterRestore.ActiveStars.Count);
            Assert.AreEqual(savedOrigin, weapon.Origin);
            Assert.AreEqual(savedRotation, weapon.Rotation);
        }

        [Test]
        public void PreviewMutation_DraggedStarredItem_StarFollowsToNewTarget()
        {
            // Stationary Weapon at (5, 0). Whetstone initially at (0, 0) → star target (1, 0) (no neighbor).
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceBag(MakeBag("Host2", Shape3x3()), new Vector2Int(3, 0));
            var whetstoneData = MakeItem("Whetstone", Shape1x1(), new[] { ItemTag.Sharpening },
                stars: new[] { Star(0, 0, EdgeDirection.Right) },
                effects: new[] { Mod(ItemTag.Weapon, ConditionalEffectTarget.Neighbor) });
            var whetstone = grid.PlaceItem(whetstoneData, new Vector2Int(0, 0));

            var weaponData = MakeItem("Knife", Shape1x1(), new[] { ItemTag.Weapon });
            grid.PlaceItem(weaponData, new Vector2Int(5, 0));

            // Committed state: not adjacent.
            Assert.AreEqual(0, SynergyResolver.Resolve(grid).ActiveStars.Count);

            // Preview: drag Whetstone to (4, 0) so its star target is (5, 0) = where the Weapon sits.
            Vector2Int savedOrigin = whetstone.Origin;
            try
            {
                whetstone.Origin = new Vector2Int(4, 0);
                var preview = SynergyResolver.Resolve(grid);
                Assert.AreEqual(1, preview.ActiveStars.Count,
                    "Preview should activate Whetstone's star when dragging it adjacent to a Weapon.");
            }
            finally
            {
                whetstone.Origin = savedOrigin;
            }
        }
    }
}
