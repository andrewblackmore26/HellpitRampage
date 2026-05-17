using HellpitRampage.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-014.B: verifies the round → biome backdrop colour mapping
    /// (Outskirts 1-10, Inner Town 11-20, Depths 21-30).
    /// </summary>
    public class BiomeTransitionTests
    {
        [Test]
        public void Round1_OutskirtsColor()
        {
            var go = new GameObject("Test");
            var ctrl = go.AddComponent<BiomeTransitionController>();
            Assert.AreEqual(new Color(0.4f, 0.5f, 0.6f), ctrl.ColorForRound(1));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Round11_InnerTownColor()
        {
            var go = new GameObject("Test");
            var ctrl = go.AddComponent<BiomeTransitionController>();
            Assert.AreEqual(new Color(0.5f, 0.4f, 0.4f), ctrl.ColorForRound(11));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Round21_DepthsColor()
        {
            var go = new GameObject("Test");
            var ctrl = go.AddComponent<BiomeTransitionController>();
            Assert.AreEqual(new Color(0.3f, 0.2f, 0.35f), ctrl.ColorForRound(21));
            Object.DestroyImmediate(go);
        }
    }
}
