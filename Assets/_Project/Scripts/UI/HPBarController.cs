using HellpitRampage.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class HPBarController : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;
        [SerializeField] private Health _playerHealth;

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged.AddListener(HandleHealthChanged);
                HandleHealthChanged(_playerHealth.CurrentHP, _playerHealth.MaxHP);
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null) _playerHealth.OnHealthChanged.RemoveListener(HandleHealthChanged);
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_fillImage == null || max <= 0f) return;
            _fillImage.fillAmount = current / max;
        }
    }
}
