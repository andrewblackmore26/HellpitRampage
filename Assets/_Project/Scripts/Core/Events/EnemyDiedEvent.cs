using UnityEngine;

namespace HellpitRampage.Core
{
    public struct EnemyDiedEvent : IGameEvent
    {
        public Vector3 Position;
        public int GoldAmount;
    }
}
