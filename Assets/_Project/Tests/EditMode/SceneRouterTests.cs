using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-015: SceneRouter — verifies the routing constants and singleton wiring. Actual
    /// scene loads cannot run in EditMode, so transition behaviour beyond this is covered
    /// by the designer playtest.
    /// </summary>
    public class SceneRouterTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void SceneNameConstants_MatchSceneAssetNames()
        {
            Assert.AreEqual("Combat", SceneRouter.CombatScene);
            Assert.AreEqual("Shop", SceneRouter.ShopScene);
            Assert.AreEqual("MainMenu", SceneRouter.MainMenuScene);
        }

        [Test]
        public void Awake_WiresSingletonInstance()
        {
            _go = new GameObject("SceneRouterTestHost");
            var router = _go.AddComponent<SceneRouter>();
            EditModeLifecycle.Wake(router);

            Assert.AreSame(router, SceneRouter.Instance);
        }

        // Note: OnDestroy clearing the static Instance is correct runtime behaviour but is
        // not EditMode-testable — Unity never started the component's lifecycle (AddComponent
        // does not run Awake in edit mode), so DestroyImmediate does not invoke OnDestroy.
    }
}
