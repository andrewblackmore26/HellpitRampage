using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    public class GoldDropController : MonoBehaviour
    {
        [SerializeField] private GameObject _goldPickupPrefab;
        [SerializeField] private int _prewarmCount = 30;

        private void Start()
        {
            if (_goldPickupPrefab != null && PoolManager.Instance != null)
                PoolManager.Instance.Prewarm(_goldPickupPrefab, _prewarmCount);
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<EnemyDiedEvent>(HandleEnemyDied);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<EnemyDiedEvent>(HandleEnemyDied);
        }

        private void HandleEnemyDied(EnemyDiedEvent evt)
        {
            if (_goldPickupPrefab == null || PoolManager.Instance == null) return;
            if (evt.GoldAmount <= 0) return;

            GameObject pickup = PoolManager.Instance.Get(_goldPickupPrefab);
            if (pickup == null) return;
            pickup.transform.position = evt.Position;
            pickup.transform.rotation = Quaternion.identity;

            GoldPickup gp = pickup.GetComponent<GoldPickup>();
            if (gp != null) gp.Initialize(evt.GoldAmount);
        }
    }
}
