using HellpitRampage.Core;
using TMPro;
using UnityEngine;

namespace HellpitRampage.UI
{
    public class GoldDisplayController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;

        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<GoldChangedEvent>(HandleGoldChanged);
            UpdateDisplay(RunManager.Instance != null ? RunManager.Instance.CurrentGold : 0);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<GoldChangedEvent>(HandleGoldChanged);
        }

        private void HandleGoldChanged(GoldChangedEvent evt) => UpdateDisplay(evt.NewAmount);

        private void UpdateDisplay(int amount)
        {
            if (_label != null) _label.text = $"Gold: {amount}";
        }
    }
}
