using UnityEngine;

namespace HellpitRampage.Core
{
    public class BootController : MonoBehaviour
    {
        private void Start()
        {
            if (GameManager.Instance == null) { Debug.LogError("BootController: GameManager.Instance is null. Check Boot scene setup."); return; }
            if (EventBus.Instance == null)    { Debug.LogError("BootController: EventBus.Instance is null. Check Boot scene setup."); return; }
            if (SaveManager.Instance == null) { Debug.LogError("BootController: SaveManager.Instance is null. Check Boot scene setup."); return; }

            _ = SaveManager.Instance.Load();

            GameManager.Instance.TransitionTo(GameManager.GameState.MainMenu);
        }
    }
}
