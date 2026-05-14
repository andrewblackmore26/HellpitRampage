using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    public class InventoryGridTests
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
        private static ItemShape Shape1x2Horizontal() => MakeShape((0, 0), (1, 0));

        // ---------------- 1. PlaceBag in empty grid ----------------

        [Test]
        public void PlaceBag_InEmptyGrid_Succeeds()
        {
            var grid = new InventoryGrid();
            var bag = MakeBag("TestBag", Shape2x2());

            var placed = grid.PlaceBag(bag, new Vector2Int(0, 0));

            Assert.IsNotNull(placed, "PlaceBag should succeed in an empty grid.");
            Assert.AreEqual(1, grid.Bags.Count, "Grid should report one bag after a successful place.");
        }

        // ---------------- 2. PlaceBag out of bounds ----------------

        [Test]
        public void PlaceBag_OutOfBounds_Fails()
        {
            var grid = new InventoryGrid();
            var bag = MakeBag("TestBag", Shape2x2());

            // Origin (5,8) with a 2x2 shape occupies x=5,6 and y=8,9. In the v3 9x6 landscape grid,
            // y=8 and y=9 are both out of bounds (max y = 5), so placement must fail.
            var placed = grid.PlaceBag(bag, new Vector2Int(5, 8));

            Assert.IsNull(placed, "PlaceBag must return null when any cell falls outside the 9x6 grid.");
            Assert.AreEqual(0, grid.Bags.Count);
        }

        // ---------------- 3. PlaceBag overlapping existing ----------------

        [Test]
        public void PlaceBag_OverlappingExistingBag_Fails()
        {
            var grid = new InventoryGrid();
            var bagA = MakeBag("A", Shape2x2());
            var bagB = MakeBag("B", Shape2x2());

            grid.PlaceBag(bagA, new Vector2Int(0, 0));
            var overlap = grid.PlaceBag(bagB, new Vector2Int(1, 0));

            Assert.IsNull(overlap, "Overlapping placement must fail.");
            Assert.AreEqual(1, grid.Bags.Count, "Failed placement must not mutate the bag list.");
        }

        // ---------------- 4. RemoveBag also removes its items ----------------

        [Test]
        public void RemoveBag_RemovesContainedItems()
        {
            var grid = new InventoryGrid();
            var bag = grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var item = grid.PlaceItem(MakeItem("Knife", Shape1x1()), new Vector2Int(1, 1));
            Assert.IsNotNull(item, "Precondition: item should place inside the 3x3 bag.");

            bool removed = grid.RemoveBag(bag);

            Assert.IsTrue(removed);
            Assert.AreEqual(0, grid.Bags.Count);
            Assert.AreEqual(0, grid.Items.Count, "Removing a bag must also remove items hosted by it.");
        }

        // ---------------- 5. PlaceItem with no bag at target ----------------

        [Test]
        public void PlaceItem_NoBagAtTarget_Fails()
        {
            var grid = new InventoryGrid();
            var item = grid.PlaceItem(MakeItem("Knife", Shape1x1()), new Vector2Int(3, 3));

            Assert.IsNull(item, "Items cannot occupy empty grid cells outside any bag.");
            Assert.AreEqual(0, grid.Items.Count);
        }

        // ---------------- 6. PlaceItem inside bag ----------------

        [Test]
        public void PlaceItem_InsideBag_Succeeds()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));

            var item = grid.PlaceItem(MakeItem("Knife", Shape1x1()), new Vector2Int(1, 1));

            Assert.IsNotNull(item);
            Assert.AreEqual(1, grid.Items.Count);
        }

        // ---------------- 7. PlaceItem outside bag cells ----------------

        [Test]
        public void PlaceItem_OutsideBagCells_Fails()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0)); // covers (0..2, 0..2)

            // (3,3) is outside the bag, but still inside the grid.
            var item = grid.PlaceItem(MakeItem("Knife", Shape1x1()), new Vector2Int(3, 3));

            Assert.IsNull(item);
        }

        // ---------------- 8. PlaceItem spanning two bags ----------------

        [Test]
        public void PlaceItem_SpanningTwoBags_Fails()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Left", Shape2x2()), new Vector2Int(0, 0)); // (0..1, 0..1)
            grid.PlaceBag(MakeBag("Right", Shape2x2()), new Vector2Int(2, 0)); // (2..3, 0..1)

            // 1x2 horizontal item at origin (1,0) → cells (1,0) in Left and (2,0) in Right.
            var item = grid.PlaceItem(MakeItem("CrossKnife", Shape1x2Horizontal()), new Vector2Int(1, 0));

            Assert.IsNull(item, "Items must not straddle two different bags.");
            Assert.AreEqual(0, grid.Items.Count);
        }

        // ---------------- 9. PlaceItem on existing item ----------------

        [Test]
        public void PlaceItem_OnExistingItem_Fails()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("First", Shape1x1()), new Vector2Int(1, 1));

            var second = grid.PlaceItem(MakeItem("Second", Shape1x1()), new Vector2Int(1, 1));

            Assert.IsNull(second);
            Assert.AreEqual(1, grid.Items.Count);
        }

        // ---------------- 10. Adjacency: no neighbors ----------------

        [Test]
        public void GetAdjacentItems_NoNeighbors_ReturnsEmpty()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var item = grid.PlaceItem(MakeItem("Lonely", Shape1x1()), new Vector2Int(1, 1));

            var neighbors = grid.GetAdjacentItems(item);

            Assert.AreEqual(0, neighbors.Count);
        }

        // ---------------- 11. Adjacency: one orthogonal neighbor ----------------

        [Test]
        public void GetAdjacentItems_OneOrthogonalNeighbor_ReturnsIt()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var a = grid.PlaceItem(MakeItem("A", Shape1x1()), new Vector2Int(0, 0));
            var b = grid.PlaceItem(MakeItem("B", Shape1x1()), new Vector2Int(1, 0));

            var fromA = grid.GetAdjacentItems(a);
            var fromB = grid.GetAdjacentItems(b);

            Assert.AreEqual(1, fromA.Count);
            Assert.AreSame(b, fromA[0]);
            Assert.AreEqual(1, fromB.Count);
            Assert.AreSame(a, fromB[0]);
        }

        // ---------------- 12. Adjacency: diagonals do NOT count ----------------

        [Test]
        public void GetAdjacentItems_DiagonalNeighbor_DoesNotCount()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            var a = grid.PlaceItem(MakeItem("A", Shape1x1()), new Vector2Int(0, 0));
            var b = grid.PlaceItem(MakeItem("B", Shape1x1()), new Vector2Int(1, 1));

            var fromA = grid.GetAdjacentItems(a);
            var fromB = grid.GetAdjacentItems(b);

            Assert.AreEqual(0, fromA.Count, "Diagonal neighbors must not count as adjacent.");
            Assert.AreEqual(0, fromB.Count);
        }

        // ---------------- 13. Adjacency across bag boundaries ----------------

        [Test]
        public void GetAdjacentItems_AcrossBagBoundary_StillReportsAdjacency()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Left", Shape1x1()), new Vector2Int(0, 0));
            grid.PlaceBag(MakeBag("Right", Shape1x1()), new Vector2Int(1, 0));

            var a = grid.PlaceItem(MakeItem("A", Shape1x1()), new Vector2Int(0, 0));
            var b = grid.PlaceItem(MakeItem("B", Shape1x1()), new Vector2Int(1, 0));

            var fromA = grid.GetAdjacentItems(a);

            Assert.AreEqual(1, fromA.Count, "Adjacency must ignore bag boundaries.");
            Assert.AreSame(b, fromA[0]);
        }

        // ---------------- 14. Multi-cell item: each neighbor reported once ----------------

        [Test]
        public void GetAdjacentItems_MultiCellItem_ReturnsEachNeighborOnce()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));

            // 2x2 item covering (0,0),(1,0),(0,1),(1,1).
            var bigItem = grid.PlaceItem(MakeItem("Big", Shape2x2()), new Vector2Int(0, 0));
            // 1x1 neighbors at (2,0), (2,1), (0,2). Each is adjacent to exactly one cell of bigItem.
            var n1 = grid.PlaceItem(MakeItem("N1", Shape1x1()), new Vector2Int(2, 0));
            var n2 = grid.PlaceItem(MakeItem("N2", Shape1x1()), new Vector2Int(2, 1));
            var n3 = grid.PlaceItem(MakeItem("N3", Shape1x1()), new Vector2Int(0, 2));

            var neighbors = grid.GetAdjacentItems(bigItem);

            Assert.AreEqual(3, neighbors.Count, "Each neighbor must appear exactly once.");
            CollectionAssert.Contains(neighbors, n1);
            CollectionAssert.Contains(neighbors, n2);
            CollectionAssert.Contains(neighbors, n3);
        }

        // ---------------- 15. Clear resets all state ----------------

        [Test]
        public void Clear_ResetsAllState()
        {
            var grid = new InventoryGrid();
            grid.PlaceBag(MakeBag("Host", Shape3x3()), new Vector2Int(0, 0));
            grid.PlaceItem(MakeItem("Knife", Shape1x1()), new Vector2Int(1, 1));

            grid.Clear();

            Assert.AreEqual(0, grid.Bags.Count);
            Assert.AreEqual(0, grid.Items.Count);

            // After Clear, instance IDs should restart at 1 — verify by placing again and checking the ID.
            var bag = grid.PlaceBag(MakeBag("Fresh", Shape1x1()), new Vector2Int(0, 0));
            Assert.IsNotNull(bag);
            Assert.AreEqual(1, bag.InstanceID, "Clear must reset the InstanceID counter so fresh placements start at 1.");
        }
    }
}
