using HellpitRampage.Core;
using HellpitRampage.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

namespace HellpitRampage.UI
{
    public class ShopController : MonoBehaviour
    {
        [SerializeField] private ItemPool _pool;
        [SerializeField] private ShopSlot[] _slots = new ShopSlot[5];
        [SerializeField] private Button _rerollButton;
        [SerializeField] private TextMeshProUGUI _rerollLabel;

        private int _rerollsThisShop;
        private Random _rng;

        private void OnEnable()
        {
            // L-007 safety: OnEnable is the entry path. Lazy-init survives domain reload during Play.
            if (_rng == null) _rng = new Random();

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<RoundEndedEvent>(HandleRoundEnded);
                EventBus.Instance.Subscribe<RoundStartedEvent>(HandleRoundStarted);
                EventBus.Instance.Subscribe<GoldChangedEvent>(HandleGoldChanged);
            }
            if (_rerollButton != null) _rerollButton.onClick.AddListener(HandleRerollClicked);
            UpdateRerollLabel();
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<RoundEndedEvent>(HandleRoundEnded);
                EventBus.Instance.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
                EventBus.Instance.Unsubscribe<GoldChangedEvent>(HandleGoldChanged);
            }
            if (_rerollButton != null) _rerollButton.onClick.RemoveListener(HandleRerollClicked);
        }

        public ScriptableObject TakeOfferFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return null;
            if (_slots[slotIndex] == null) return null;
            var offer = _slots[slotIndex].CurrentOffer;
            _slots[slotIndex].MarkSold();
            UpdateAllAffordability();
            return offer;
        }

        private void HandleRoundEnded(RoundEndedEvent _)
        {
            _rerollsThisShop = 0;
            PopulateAllSlots();
            UpdateRerollLabel();
            UpdateAllAffordability();
        }

        private void HandleRoundStarted(RoundStartedEvent _)
        {
            foreach (var slot in _slots) if (slot != null) slot.MarkSold();
        }

        private void HandleGoldChanged(GoldChangedEvent _) => UpdateAllAffordability();

        private void HandleRerollClicked()
        {
            if (RunManager.Instance == null) return;
            int cost = CurrentRerollCost(_rerollsThisShop);
            if (!RunManager.Instance.SpendGold(cost)) return;

            _rerollsThisShop++;
            PopulateUnsoldSlots();
            UpdateRerollLabel();
        }

        public static int CurrentRerollCost(int rerollsThisShop)
        {
            if (rerollsThisShop < 5) return 1;
            if (rerollsThisShop < 10) return 2;
            return 3;
        }

        private void UpdateRerollLabel()
        {
            if (_rerollLabel != null) _rerollLabel.text = $"Reroll ({CurrentRerollCost(_rerollsThisShop)}g)";
        }

        private void PopulateAllSlots()
        {
            if (_pool == null) return;
            var draws = _pool.DrawWeighted(_slots.Length, _rng);
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].SetOffer(i < draws.Count ? draws[i] : null);
            }
        }

        private void PopulateUnsoldSlots()
        {
            if (_pool == null) return;
            int needed = 0;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null && !_slots[i].IsSold) needed++;
            if (needed == 0) return;

            var draws = _pool.DrawWeighted(needed, _rng);
            int drawIdx = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null || _slots[i].IsSold) continue;
                if (drawIdx < draws.Count) _slots[i].SetOffer(draws[drawIdx++]);
            }
        }

        private void UpdateAllAffordability()
        {
            if (RunManager.Instance == null) return;
            int gold = RunManager.Instance.CurrentGold;
            foreach (var slot in _slots) if (slot != null) slot.UpdateAffordability(gold);
        }
    }
}
