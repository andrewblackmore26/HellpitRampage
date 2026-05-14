using UnityEngine;

namespace HellpitRampage.Core
{
    public class GameSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("GameSceneBootstrap: RunManager.Instance is null. Boot scene must have RunManager under Managers.");
                return;
            }
            RunManager.Instance.StartNewRun();
        }
    }
}
