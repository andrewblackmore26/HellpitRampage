namespace HellpitRampage.Combat
{
    /// <summary>
    /// Pure math for count-based behavior triggers. Extracted so the modulo logic is testable
    /// without standing up PlayerWeapons + InventoryService + PoolManager in EditMode tests.
    /// </summary>
    public static class BehaviorMath
    {
        /// <summary>
        /// True iff a behavior with the given TriggerCount should fire on the attack that
        /// brings the counter to <paramref name="count"/>. Counter convention: incremented
        /// AFTER each base fire, so the first fire produces count=1.
        /// </summary>
        public static bool ShouldTrigger(int count, int triggerCount)
        {
            if (triggerCount <= 0) return false;
            if (count <= 0) return false;
            return count % triggerCount == 0;
        }
    }
}
