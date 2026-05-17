using UnityEngine;
using UnityEngine.SceneManagement;

namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-015: centralizes loads of the two gameplay scenes (Combat, Shop) plus the menu.
    /// Persistent singleton — lives under Managers in Boot.unity — so a round transition
    /// can be driven from anywhere (RunManager round-end, the shop's Next Round button)
    /// without scattering <see cref="SceneManager.LoadScene(string)"/> calls.
    /// </summary>
    public class SceneRouter : MonoBehaviour
    {
        public static SceneRouter Instance { get; private set; }

        // Public so EditMode tests can assert the routing constants without loading scenes.
        public const string CombatScene = "Combat";
        public const string ShopScene = "Shop";
        public const string MainMenuScene = "MainMenu";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // L-001: persist the root, not the child. Guarded — DontDestroyOnLoad is
            // play-mode-only and throws when instantiated in an EditMode test.
            if (Application.isPlaying) DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Loads the Combat scene — a combat round.</summary>
        public void LoadCombat() => SceneManager.LoadScene(CombatScene);

        /// <summary>Loads the Shop scene — the between-rounds shop phase.</summary>
        public void LoadShop() => SceneManager.LoadScene(ShopScene);

        /// <summary>Loads the Main Menu.</summary>
        public void LoadMainMenu() => SceneManager.LoadScene(MainMenuScene);
    }
}
