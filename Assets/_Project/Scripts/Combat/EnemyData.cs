using UnityEngine;

namespace HellpitRampage.Combat
{
    [CreateAssetMenu(fileName = "NewEnemy_Enemy", menuName = "HellpitRampage/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("WS-013: stable string identifier used by the save system. Snake_case. Must match the asset stem and be unique across all EnemyData.")]
        [SerializeField] private string _id;
        public string Id => _id;

        [Tooltip("The runtime prefab that visualizes and behaves as this enemy.")]
        public GameObject Prefab;

        [Tooltip("Movement speed in units/second toward the player.")]
        public float MoveSpeed = 3f;

        [Tooltip("Maximum HP. Enemy dies when HP reaches 0.")]
        public float MaxHP = 3f;

        [Tooltip("Damage dealt to the player on contact.")]
        public float ContactDamage = 10f;

        [Tooltip("Gold dropped when this enemy dies. 0 disables the drop.")]
        public int GoldDropAmount = 1;
    }
}
