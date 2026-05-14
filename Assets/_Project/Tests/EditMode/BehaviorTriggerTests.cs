using HellpitRampage.Combat;
using NUnit.Framework;

namespace HellpitRampage.Tests
{
    public class BehaviorTriggerTests
    {
        // ---------------- 1. First fires at exactly N ----------------

        [Test]
        public void Counter_FirstFireTriggersAtN()
        {
            const int trigger = 5;
            for (int c = 1; c < trigger; c++)
                Assert.IsFalse(BehaviorMath.ShouldTrigger(c, trigger),
                    $"Trigger should not fire at count={c}.");

            Assert.IsTrue(BehaviorMath.ShouldTrigger(trigger, trigger),
                "Trigger should fire on the Nth attack (count==N).");
        }

        // ---------------- 2. Fires on every Nth ----------------

        [Test]
        public void Counter_FiresOnEveryNthAttack()
        {
            const int trigger = 5;
            for (int c = 1; c <= 2 * trigger; c++)
            {
                bool expected = c % trigger == 0;
                Assert.AreEqual(expected, BehaviorMath.ShouldTrigger(c, trigger),
                    $"count={c}: expected {expected}");
            }
        }

        // ---------------- 3. TriggerCount=1 fires every attack ----------------

        [Test]
        public void Counter_TriggerCountOne_FiresEveryAttack()
        {
            for (int c = 1; c <= 10; c++)
                Assert.IsTrue(BehaviorMath.ShouldTrigger(c, 1),
                    $"TriggerCount=1 must fire on every attack (failed at count={c}).");
        }

        // ---------------- 4. TriggerCount=0 never fires (no div-by-zero) ----------------

        [Test]
        public void Counter_TriggerCountZero_DoesNotFire()
        {
            for (int c = 0; c <= 10; c++)
                Assert.IsFalse(BehaviorMath.ShouldTrigger(c, 0),
                    "TriggerCount=0 must be a defensive no-op (never fires; never throws).");

            Assert.IsFalse(BehaviorMath.ShouldTrigger(5, -3),
                "Negative TriggerCount also must not fire.");
        }
    }
}
