using HellpitRampage.UI;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.1: the detail tooltip clamps to the canvas bounds so a click near the edge
    /// doesn't render off-screen (spec §3 gotcha #12). The clamp math is extracted to
    /// <see cref="DetailTooltipController.ClampPanelCenter"/> so we can unit-test it without
    /// instantiating the whole controller.
    /// </summary>
    public class CanvasClampTests
    {
        // 1920x1080 canvas centered at origin, panel 480x360.
        private static readonly Rect Canvas = new Rect(-960f, -540f, 1920f, 1080f);
        private static readonly Vector2 PanelSize = new Vector2(480f, 360f);

        [Test]
        public void Center_WhenInsideBounds_PassesThrough()
        {
            var desired = new Vector2(0f, 0f);
            var result = DetailTooltipController.ClampPanelCenter(desired, PanelSize, Canvas);
            Assert.AreEqual(desired, result);
        }

        [Test]
        public void Center_NearRightEdge_ClampsLeftByHalfWidth()
        {
            // Cursor at the canvas right edge (x = 960). Panel center must be at most xMax - halfW.
            var desired = new Vector2(960f, 0f);
            var result = DetailTooltipController.ClampPanelCenter(desired, PanelSize, Canvas);
            Assert.AreEqual(960f - PanelSize.x * 0.5f, result.x, 0.001f);
            Assert.AreEqual(0f, result.y, 0.001f);
        }

        [Test]
        public void Center_NearLeftEdge_ClampsRightByHalfWidth()
        {
            var desired = new Vector2(-960f, 0f);
            var result = DetailTooltipController.ClampPanelCenter(desired, PanelSize, Canvas);
            Assert.AreEqual(-960f + PanelSize.x * 0.5f, result.x, 0.001f);
        }

        [Test]
        public void Center_NearTopEdge_ClampsDownByHalfHeight()
        {
            var desired = new Vector2(0f, 540f);
            var result = DetailTooltipController.ClampPanelCenter(desired, PanelSize, Canvas);
            Assert.AreEqual(540f - PanelSize.y * 0.5f, result.y, 0.001f);
        }

        [Test]
        public void Center_NearBottomEdge_ClampsUpByHalfHeight()
        {
            var desired = new Vector2(0f, -540f);
            var result = DetailTooltipController.ClampPanelCenter(desired, PanelSize, Canvas);
            Assert.AreEqual(-540f + PanelSize.y * 0.5f, result.y, 0.001f);
        }

        [Test]
        public void Center_NearBottomRightCorner_ClampsBothAxes()
        {
            var desired = new Vector2(960f, -540f);
            var result = DetailTooltipController.ClampPanelCenter(desired, PanelSize, Canvas);
            Assert.AreEqual(960f - PanelSize.x * 0.5f, result.x, 0.001f);
            Assert.AreEqual(-540f + PanelSize.y * 0.5f, result.y, 0.001f);
        }
    }
}
