using UnityEngine;
using UnityEngine.SceneManagement;

namespace HellpitRampage.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Boot,
            MainMenu,
            InRun
        }

        public GameState CurrentState { get; private set; } = GameState.Boot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // DontDestroyOnLoad only works on root GameObjects. The singleton sits under a
            // `Managers` parent in Boot.unity for hierarchy organization, so persist the root.
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void TransitionTo(GameState newState)
        {
            if (newState == CurrentState)
            {
                Debug.LogWarning($"GameManager: TransitionTo ignored — already in state {newState}.");
                return;
            }

            GameState oldState = CurrentState;
            CurrentState = newState;

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Publish(new GameStateChangedEvent
                {
                    OldState = oldState,
                    NewState = newState
                });
            }

            string sceneName = SceneNameFor(newState);
            SceneManager.LoadScene(sceneName);
        }

        private static string SceneNameFor(GameState state)
        {
            switch (state)
            {
                case GameState.Boot:     return "Boot";
                case GameState.MainMenu: return "MainMenu";
                case GameState.InRun:    return "Game";
                default:
                    Debug.LogError($"GameManager: no scene mapping for state {state}.");
                    return "Boot";
            }
        }
    }
}
