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

        // WS-013: scratch field set by Resume → consumed by RunRestoreController on next Game-scene
        // load → cleared by the controller. Null in the normal new-run path; non-null exactly while
        // we are transitioning into a restore.
        public RunSaveData PendingResume { get; set; }

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

        /// <summary>
        /// WS-013: Resume-Run path. Loads the run save, stashes it for the Game scene's
        /// RunRestoreController to consume after scene load, then transitions to InRun.
        /// No-op (with error log) if no valid run save exists.
        /// </summary>
        public void StartResumeFromSave()
        {
            if (SaveManager.Instance == null)
            {
                Debug.LogError("GameManager.StartResumeFromSave: SaveManager.Instance is null.");
                return;
            }
            var data = SaveManager.Instance.LoadRun();
            if (data == null)
            {
                Debug.LogError("GameManager.StartResumeFromSave: LoadRun returned null. Resume aborted.");
                return;
            }
            PendingResume = data;
            TransitionTo(GameState.InRun);
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
