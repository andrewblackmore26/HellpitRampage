using HellpitRampage.UI;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.4: the tooltip's bottom-left corner anchors at the cursor (+12px offset) by default
    /// and flips to alternate pivots near the right and/or top canvas edges so the panel never
    /// extends off-screen. Pure-math tests against <see cref="TooltipController.ComputeAnchor"/>;
    /// no Canvas required.
    /// </summary>
    public class TooltipAnchorTests
    {
        // 1920x1080 canvas centered at origin, panel 480x360, cursor offset 12px.
        private static readonly Rect Canvas = new Rect(-960f, -540f, 1920f, 1080f);
        private static readonly Vector2 PanelSize = new Vector2(480f, 360f);
        private const float Offset = 12f;

        [Test]
        public void CursorAwayFromEdges_PivotIsBottomLeft_PosOffsetUpRight()
        {
            Vector2 cursor = new Vector2(0f, 0f);
            var (pivot, pos) = TooltipController.ComputeAnchor(cursor, PanelSize, Canvas);
            Assert.AreEqual(Vector2.zero, pivot);
            Assert.AreEqual(cursor.x + Offset, pos.x, 0.001f);
            Assert.AreEqual(cursor.y + Offset, pos.y, 0.001f);
        }

        [Test]
        public void CursorNearRightEdge_XPivotFlipsToOne_PanelExtendsLeft()
        {
            // Cursor far enough right that cursor.x + offset + panel.x overflows xMax (960).
            // 700 + 12 + 480 = 1192 > 960 → flip.
            Vector2 cursor = new Vector2(700f, 0f);
            var (pivot, pos) = TooltipController.ComputeAnchor(cursor, PanelSize, Canvas);
            Assert.AreEqual(1f, pivot.x, 0.001f);
            Assert.AreEqual(0f, pivot.y, 0.001f);
            Assert.AreEqual(cursor.x - Offset, pos.x, 0.001f);
        }

        [Test]
        public void CursorNearTopEdge_YPivotFlipsToOne_PanelExtendsDown()
        {
            // 300 + 12 + 360 = 672 > 540 → flip.
            Vector2 cursor = new Vector2(0f, 300f);
            var (pivot, pos) = TooltipController.ComputeAnchor(cursor, PanelSize, Canvas);
            Assert.AreEqual(0f, pivot.x, 0.001f);
            Assert.AreEqual(1f, pivot.y, 0.001f);
            Assert.AreEqual(cursor.y - Offset, pos.y, 0.001f);
        }

        [Test]
        public void CursorNearTopRightCorner_BothPivotsFlip()
        {
            Vector2 cursor = new Vector2(700f, 300f);
            var (pivot, pos) = TooltipController.ComputeAnchor(cursor, PanelSize, Canvas);
            Assert.AreEqual(1f, pivot.x, 0.001f);
            Assert.AreEqual(1f, pivot.y, 0.001f);
            Assert.AreEqual(cursor.x - Offset, pos.x, 0.001f);
            Assert.AreEqual(cursor.y - Offset, pos.y, 0.001f);
        }
    }
}
