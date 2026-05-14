using UnityEngine;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.5: lightweight 2D physics for items on the spillover floor. Runs in
    /// FixedUpdate at canvas-local coordinates against the GroundArea's RectTransform
    /// bounds — never touches Rigidbody2D / world space. Gravity, AABB wall collision
    /// (left/right/bottom), bounce dampening, ground friction, sleep on rest. Item-vs-item
    /// collision is resolved at the GroundManager level so a single 3-pass push-out can
    /// see all pairs at once.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GroundItemPhysics : MonoBehaviour
    {
        private const float GRAVITY = -1200f;       // px/s²
        private const float BOUNCE_DAMP = 0.5f;
        private const float FRICTION = 0.92f;
        private const float SLEEP_THRESHOLD = 5f;

        public bool IsHeld;
        public Vector2 Velocity;

        private RectTransform _rt;
        private RectTransform _groundRT;
        private bool _isAsleep;

        public void Initialize(Vector2 initialVelocity, RectTransform groundRT)
        {
            _rt = transform as RectTransform;
            _groundRT = groundRT;
            Velocity = initialVelocity;
            _isAsleep = false;
            IsHeld = false;
        }

        public void Wake() => _isAsleep = false;

        /// <summary>
        /// AABB in the GroundArea's local space (centered on anchoredPosition,
        /// sized by sizeDelta). Pivot expected to be (0.5, 0.5).
        /// </summary>
        public Rect GetLocalAABB()
        {
            if (_rt == null) return Rect.zero;
            Vector2 pos = _rt.anchoredPosition;
            Vector2 size = _rt.sizeDelta;
            return new Rect(pos.x - size.x * 0.5f, pos.y - size.y * 0.5f, size.x, size.y);
        }

        private void FixedUpdate()
        {
            if (IsHeld || _isAsleep) return;
            if (_rt == null || _groundRT == null) return;

            float dt = Time.fixedDeltaTime;
            Velocity.y += GRAVITY * dt;

            Vector2 newPos = _rt.anchoredPosition + Velocity * dt;
            Vector2 size = _rt.sizeDelta;
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;

            Rect bounds = _groundRT.rect;
            bool grounded = false;

            // Left wall
            if (newPos.x - halfW < bounds.xMin)
            {
                newPos.x = bounds.xMin + halfW;
                Velocity.x = -Velocity.x * BOUNCE_DAMP;
            }
            // Right wall
            if (newPos.x + halfW > bounds.xMax)
            {
                newPos.x = bounds.xMax - halfW;
                Velocity.x = -Velocity.x * BOUNCE_DAMP;
            }
            // Floor
            if (newPos.y - halfH < bounds.yMin)
            {
                newPos.y = bounds.yMin + halfH;
                Velocity.y = -Velocity.y * BOUNCE_DAMP;
                grounded = true;
            }

            if (grounded) Velocity.x *= FRICTION;

            _rt.anchoredPosition = newPos;

            if (grounded && Velocity.magnitude < SLEEP_THRESHOLD)
            {
                Velocity = Vector2.zero;
                _isAsleep = true;
            }
        }
    }
}
