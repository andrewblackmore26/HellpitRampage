using System.Text.RegularExpressions;
using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HellpitRampage.Tests
{
    public class PoolManagerTests
    {
        private GameObject _managerGO;
        private PoolManager _poolManager;
        private GameObject _testPrefab;

        [SetUp]
        public void SetUp()
        {
            _managerGO = new GameObject("PoolManagerTestHost");
            _poolManager = _managerGO.AddComponent<PoolManager>();

            // Plain GameObject used as a pool source. Not a real prefab asset, but ObjectPool's
            // createFunc accepts any Object reference for Instantiate, so this works fine in EditMode.
            _testPrefab = new GameObject("TestPrefab");
            _testPrefab.SetActive(false); // keep the template inert; PoolManager activates on Get
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy any pooled children first so they don't dangle.
            if (_managerGO != null)
            {
                Object.DestroyImmediate(_managerGO);
            }
            if (_testPrefab != null)
            {
                Object.DestroyImmediate(_testPrefab);
            }
            _managerGO = null;
            _poolManager = null;
            _testPrefab = null;
        }

        [Test]
        public void Get_ReturnsActiveInstance()
        {
            GameObject instance = _poolManager.Get(_testPrefab);

            Assert.IsNotNull(instance, "Get should return a non-null instance.");
            Assert.IsTrue(instance.activeSelf, "Instance should be active after Get.");

            PooledObject marker = instance.GetComponent<PooledObject>();
            Assert.IsNotNull(marker, "Instance should have a PooledObject marker component.");
            Assert.AreSame(_testPrefab, marker.SourcePrefab, "Marker should reference the source prefab.");
        }

        [Test]
        public void Release_DeactivatesInstance()
        {
            GameObject instance = _poolManager.Get(_testPrefab);
            _poolManager.Release(instance);

            Assert.IsFalse(instance.activeSelf, "Instance should be inactive after Release.");
        }

        [Test]
        public void GetAfterRelease_ReturnsSameInstance()
        {
            GameObject first = _poolManager.Get(_testPrefab);
            _poolManager.Release(first);
            GameObject second = _poolManager.Get(_testPrefab);

            Assert.AreSame(first, second, "Pool should reuse the released instance on next Get.");
            Assert.IsTrue(second.activeSelf, "Reused instance should be active.");
        }

        [Test]
        public void Prewarm_DoesNotProduceActiveInstances()
        {
            const int prewarmCount = 5;
            _poolManager.Prewarm(_testPrefab, prewarmCount);

            // After prewarm, instances are pooled as inactive children of the PoolManager.
            int activeChildren = 0;
            foreach (Transform child in _managerGO.transform)
            {
                if (child.gameObject.activeSelf) activeChildren++;
            }
            Assert.AreEqual(0, activeChildren, "Prewarm should leave all instances inactive.");

            int childCountBefore = _managerGO.transform.childCount;
            Assert.AreEqual(prewarmCount, childCountBefore, "Prewarm should produce exactly N children.");

            // Getting `prewarmCount` instances should not require any new instantiations.
            for (int i = 0; i < prewarmCount; i++)
            {
                GameObject inst = _poolManager.Get(_testPrefab);
                Assert.IsNotNull(inst);
            }
            int childCountAfter = _managerGO.transform.childCount;
            Assert.AreEqual(childCountBefore, childCountAfter, "Get up to prewarm count should not instantiate new objects.");
        }

        [Test]
        public void Release_WithMissingPooledObject_LogsErrorAndDestroys()
        {
            GameObject orphan = new GameObject("OrphanNoPooledObject");
            LogAssert.Expect(LogType.Error, new Regex("no PooledObject/SourcePrefab"));

            Assert.DoesNotThrow(() => _poolManager.Release(orphan));
        }
    }
}
