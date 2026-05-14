using UnityEngine;

namespace HellpitRampage.Core
{
    public struct PlayerDiedEvent : IGameEvent
    {
        public GameObject PlayerObject;
    }
}
