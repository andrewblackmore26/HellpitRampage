using HellpitRampage.Combat;
using NUnit.Framework;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// Unit tests for <see cref="WeaponMath.StepCooldown"/>. The full firing pipeline
    /// (auto-aim + spawn + projectile travel) is verified by manual playtest — it's
    /// PlayMode territory and not worth the test-setup cost yet.
    /// </summary>
    public class PlayerWeaponsTests
    {
        private const float Epsilon = 0.0001f;

        [Test]
        public void StepCooldown_WhenCoolingDown_DecrementsByDeltaTime()
        {
            float next = WeaponMath.StepCooldown(
                current: 1.0f,
                deltaTime: 0.1f,
                maxCooldown: 0.5f,
                targetAvailable: true,
                out bool shouldFire);

            Assert.AreEqual(0.9f, next, Epsilon, "Cooldown should decrement by deltaTime.");
            Assert.IsFalse(shouldFire, "Should not fire while still cooling down.");
        }

        [Test]
        public void StepCooldown_AtZero_WithTarget_FiresAndResets()
        {
            float next = WeaponMath.StepCooldown(
                current: 0.05f,
                deltaTime: 0.1f,
                maxCooldown: 0.5f,
                targetAvailable: true,
                out bool shouldFire);

            Assert.IsTrue(shouldFire, "Should fire when cooldown reaches zero and a target is available.");
            Assert.AreEqual(0.5f, next, Epsilon, "Cooldown should reset to max after firing.");
        }

        [Test]
        public void StepCooldown_AtZero_NoTarget_PausesAtReady()
        {
            float next = WeaponMath.StepCooldown(
                current: 0.05f,
                deltaTime: 0.1f,
                maxCooldown: 0.5f,
                targetAvailable: false,
                out bool shouldFire);

            Assert.IsFalse(shouldFire, "Should not fire without a target.");
            Assert.AreEqual(0f, next, Epsilon, "Cooldown should pause at 0, not go negative.");
            Assert.GreaterOrEqual(next, 0f, "Cooldown must never be negative — would delay next fire after target appears.");
        }

        [Test]
        public void StepCooldown_AtZero_NoTarget_NextFrame_StillReady()
        {
            // Frame 1: cooldown hits zero, no target.
            float afterFrame1 = WeaponMath.StepCooldown(
                current: 0.05f,
                deltaTime: 0.1f,
                maxCooldown: 0.5f,
                targetAvailable: false,
                out bool fired1);

            // Frame 2: still at zero, still no target.
            float afterFrame2 = WeaponMath.StepCooldown(
                current: afterFrame1,
                deltaTime: 0.1f,
                maxCooldown: 0.5f,
                targetAvailable: false,
                out bool fired2);

            Assert.IsFalse(fired1, "Frame 1: no target, no fire.");
            Assert.IsFalse(fired2, "Frame 2: still no target, still no fire.");
            Assert.AreEqual(0f, afterFrame1, Epsilon, "After frame 1, cooldown paused at 0.");
            Assert.AreEqual(0f, afterFrame2, Epsilon, "After frame 2, cooldown still at 0 (pause holds across frames).");
        }
    }
}
