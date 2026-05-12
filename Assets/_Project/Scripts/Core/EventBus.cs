using System;
using System.Collections.Generic;
using UnityEngine;

namespace HellpitRampage.Core
{
    public class EventBus : MonoBehaviour
    {
        public static EventBus Instance { get; private set; }

        public bool DebugLogEvents = false;

        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

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
            _handlers.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            Type key = typeof(T);
            if (!_handlers.TryGetValue(key, out List<Delegate> list))
            {
                list = new List<Delegate>();
                _handlers[key] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            Type key = typeof(T);
            if (_handlers.TryGetValue(key, out List<Delegate> list))
            {
                list.Remove(handler);
            }
        }

        public void Publish<T>(T eventData) where T : IGameEvent
        {
            Type key = typeof(T);
            if (DebugLogEvents)
            {
                Debug.Log($"EventBus: publishing {key.Name}");
            }

            if (!_handlers.TryGetValue(key, out List<Delegate> list) || list.Count == 0)
            {
                return;
            }

            Delegate[] snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    ((Action<T>)snapshot[i]).Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
