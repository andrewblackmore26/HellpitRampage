using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    public class InventoryGridMoveTests
    {
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

        private static ItemData MakeItem(string name, ItemShape shape)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.ItemName = name;
            item.Shape = shape;
            return item;
        }

        private static ItemShape Shape1x1() => MakeShape((0, 0));
        private static ItemShape Shape2x2() => MakeShape((0, 0), (1, 0), (0, 1), (1, 1));
        private static ItemShape Shape3x3() => MakeShape(
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2));
        private static ItemShape Shape1x2Vertical() => MakeShape((0, 0), (0, 1));

        [Test]
        public void MoveBag_ToValidEmpty_Succeeds()
        {
            var grid = new InventoryGrid();
            var bag = grid.PlaceBag(MakeBag("Bag", Shape2x2()), new Vector2Int(0, 0));

            bool moved = grid.MoveBag(bag, new Vector2Int(2, 2));

            Assert.IsTrue(moved);
            Assert.AreEqual(new Vector2Int(2, 2), bag.Origin);
        }

        [Test]
        public void MoveBag_ToOverlapAnotherBag_Fails()
        {
            var grid = new InventoryGrid();
            var a = grid.PlaceBag(MakeBag("A", Shape2x2()), new Vector2Int(0, 0));
            var b = grid.PlaceBag(MakeBag("B", Shape2x2()), new Vector2Int(3, 0));

            bool moved = grid.MoveBag(a, new Vector2Int(2, 0)); // Would overlap b at (3,0).

            Assert.IsFalse(moved);
            Assert.AreEqual(new Vector2Int(0, 0), a.Origin);
            Assert.AreEqual(new Vector2Int(3, 0), b.Origin);
        }

        [Test]
        public void MoveBag_ToSameOrigin_IsNoOp()
        {
            var grid = new InventoryGrid();
            var bag = grid.PlaceBag(MakeBag("Bag", Shape2x2()), new Vector2Int(0, 0));

            bool moved = grid.MoveBag(bag, new Vector2Int(0, 0));

            Assert.IsTrue(moved);
            Assert.AreEqual(new Vector2Int(0, 0), bag.Origin);
        }

        [Test]
        public void MoveItem_ToValidCellInSameBag_Succeeds()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var item = grid.PlaceItem(MakeItem("Knife", Shape1x1()), new Vector2Int(1, 1));

            bool moved = grid.MoveItem(item, new Vector2Int(2, 2), Rotation.Deg0);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Vector2Int(2, 2), item.Origin);
        }

        [Test]
        public void MoveItem_RotateToFitTightSpace_Succeeds()
        {
            // 3x3 bag at (0,0), vertical 1x2 stick at (0,0)+(0,1).
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var item = grid.PlaceItem(MakeItem("Stick", Shape1x2Vertical()), new Vector2Int(0, 0));
            Assert.IsNotNull(item);

            // Move + rotate 90: shape becomes horizontal {(0,0),(1,0)}, origin (1,1) -> cells (1,1)+(2,1). Inside the bag.
            bool moved = grid.MoveItem(item, new Vector2Int(1, 1), Rotation.Deg90);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Vector2Int(1, 1), item.Origin);
            Assert.AreEqual(Rotation.Deg90, item.Rotation);
        }

        [Test]
        public void MoveItem_OutOfBag_Fails()
        {
            // 2x2 bag at (0,0), item inside at (0,0). Try to move outside the bag.
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape2x2()), new Vector2Int(0, 0));
            var item = grid.PlaceItem(MakeItem("Knife", Shape1x1()), new Vector2Int(0, 0));

            bool moved = grid.MoveItem(item, new Vector2Int(3, 3), Rotation.Deg0);

            Assert.IsFalse(moved);
            Assert.AreEqual(new Vector2Int(0, 0), item.Origin);
        }
    }
}
