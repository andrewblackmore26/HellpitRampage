using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;

        private Rigidbody2D _rb;
        private PlayerInputActions _input;
        private Vector2 _moveInput;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            // Hot-reload during Play mode resets non-serialized fields to null but only fires OnEnable
            // (not Awake). Re-init defensively so a /domain-reload doesn't NRE here.
            EnsureInitialized();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            _input?.Player.Disable();
        }

        private void EnsureInitialized()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_input == null) _input = new PlayerInputActions();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void Update()
        {
            _moveInput = _input.Player.Movement.ReadValue<Vector2>();
            // Keyboard 2DVector composite can return magnitude > 1 on diagonals; gamepad stick already
            // respects the unit circle so the branch is a no-op there. Result: cardinals and diagonals
            // travel at the same speed.
            if (_moveInput.sqrMagnitude > 1f) _moveInput = _moveInput.normalized;
        }

        private void FixedUpdate()
        {
            _rb.linearVelocity = _moveInput * _moveSpeed;
        }
    }
}
