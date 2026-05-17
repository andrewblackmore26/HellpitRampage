using System.Reflection;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// EditMode tests run in edit mode, where <c>AddComponent</c> does NOT invoke a
    /// MonoBehaviour's <c>Awake</c>/<c>OnEnable</c> — those fire only in play mode. Singletons
    /// that wire their static <c>Instance</c> in <c>Awake</c> therefore stay unwired, and
    /// event subscriptions made in <c>OnEnable</c> never happen. This helper runs that startup
    /// lifecycle explicitly so a fixture's components behave as they would at runtime.
    ///
    /// The teardown half needs no help: <c>Object.DestroyImmediate</c> (used in fixture
    /// <c>[TearDown]</c>) already invokes <c>OnDisable</c>/<c>OnDestroy</c> in edit mode.
    /// </summary>
    internal static class EditModeLifecycle
    {
        /// <summary>Runs <c>Awake</c> then <c>OnEnable</c> on a freshly added component.</summary>
        internal static T Wake<T>(T component) where T : Component
        {
            Invoke(component, "Awake");
            Invoke(component, "OnEnable");
            return component;
        }

        /// <summary>Creates a GameObject hosting <typeparamref name="T"/> and wakes it.</summary>
        internal static T NewComponent<T>(string name = null) where T : Component
        {
            var go = new GameObject(name ?? typeof(T).Name + "TestHost");
            return Wake(go.AddComponent<T>());
        }

        private static void Invoke(Component component, string method)
        {
            // Awake/OnEnable are conventionally private Unity messages — declared on the
            // component's own type, so a non-public instance lookup finds them. Missing
            // methods (e.g. a component with no OnEnable) resolve to null and are skipped.
            MethodInfo m = component.GetType().GetMethod(
                method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            m?.Invoke(component, null);
        }
    }
}
