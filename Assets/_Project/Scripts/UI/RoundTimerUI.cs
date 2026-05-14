using HellpitRampage.Combat;
using HellpitRampage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class RoundTimerUI : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private CombatRoundController _round;

        private int _currentRound;

        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<RoundStartedEvent>(HandleRoundStarted);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
        }

        private void HandleRoundStarted(RoundStartedEvent evt)
        {
            _currentRound = evt.RoundNumber;
        }

        private void Update()
        {
            if (_label == null || _round == null) return;
            float t = Mathf.Max(0f, _round.TimeRemaining);
            int seconds = Mathf.CeilToInt(t);
            int mm = seconds / 60;
            int ss = seconds % 60;
            _label.text = $"Round {_currentRound} — {mm}:{ss:00}";
        }
    }
}
