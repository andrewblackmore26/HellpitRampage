namespace HellpitRampage.Combat
{
    /// <summary>
    /// Pure functions for weapon cooldown / targeting logic. Lifted out of
    /// <see cref="PlayerWeapons"/> so the rules can be unit-tested without
    /// needing a live scene, pool, or MonoBehaviour.
    /// </summary>
    public static class WeaponMath
    {
        /// <summary>
        /// Advances a per-weapon cooldown by one frame.
        /// Returns the new cooldown-remaining value. <paramref name="shouldFire"/> is true
        /// iff the cooldown reached zero AND a target is available - in which case the
        /// cooldown resets to <paramref name="maxCooldown"/>. With no target, the value
        /// pauses at 0 (never goes negative) so the next frame fires immediately when a
        /// target appears.
        /// </summary>
        public static float StepCooldown(float current, float deltaTime, float maxCooldown, bool targetAvailable, out bool shouldFire)
        {
            shouldFire = false;
            float next = current - deltaTime;
            if (next > 0f) return next;

            if (targetAvailable)
            {
                shouldFire = true;
                return maxCooldown;
            }

            return 0f;
        }
    }
}
