using UnityEngine;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.5: helpers for classifying a screen-space drop position. The "backpack column"
    /// is the X-range of the grid extended downward to cover the ground area — a drop here
    /// either lands in a grid cell or falls to the ground. A drop outside that column
    /// reverts to the drag origin (existing cancel semantics).
    /// </summary>
    public static class DropZoneClassifier
    {
        /// <summary>
        /// True if <paramref name="screenPos"/> is within the X-range of the backpack and the
        /// Y-range of (backpack bottom − ground area height) to (backpack top). Returns false
        /// if either bounds provider isn't ready (defensive — caller falls back to cancel).
        /// </summary>
        public static bool IsWithinBackpackXRange(Vector2 screenPos)
        {
            RectTransform bp = BackpackBoundsProvider.Current;
            if (bp == null) return false;

            var corners = new Vector3[4];
            bp.GetWorldCorners(corners);
            // 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right (screen-space pixels for Overlay canvas).
            float minX = Mathf.Min(corners[0].x, corners[2].x);
            float maxX = Mathf.Max(corners[0].x, corners[2].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y);

            // Extend the Y range downward by the ground area's height so drops in the ground
            // strip below the grid still count as in-range.
            RectTransform groundRT = GroundManager.Current != null ? GroundManager.Current.GroundAreaRT : null;
            if (groundRT != null)
            {
                var gc = new Vector3[4];
                groundRT.GetWorldCorners(gc);
                float groundMinY = Mathf.Min(gc[0].y, gc[1].y);
                float groundMaxY = Mathf.Max(gc[0].y, gc[1].y);
                minY = Mathf.Min(minY, groundMinY);
                maxY = Mathf.Max(maxY, groundMaxY);
            }

            return screenPos.x >= minX && screenPos.x <= maxX
                && screenPos.y >= minY && screenPos.y <= maxY;
        }
    }
}
