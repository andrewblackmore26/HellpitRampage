using UnityEngine;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.5: tag-style component attached to the backpack grid's RectTransform
    /// (GridAnchor in Game.unity). Exposes the RectTransform via a static accessor so
    /// DropZoneClassifier can answer "is this screen position over the backpack column?"
    /// without scene lookups every frame.
    /// </summary>
    public class BackpackBoundsProvider : MonoBehaviour
    {
        public static RectTransform Current { get; private set; }

        private RectTransform _rt;

        private void Awake()
        {
            _rt = transform as RectTransform;
            Current = _rt;
        }

        private void OnEnable()
        {
            // L-007: hot-reload safety — re-assign Current if Awake didn't run.
            if (_rt == null) _rt = transform as RectTransform;
            Current = _rt;
        }

        private void OnDestroy()
        {
            if (Current == _rt) Current = null;
        }
    }
}
