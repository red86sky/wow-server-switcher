namespace WowRegionSwitcher
{
    // Pure state transition predicate, shared by UI entry-point and offline tests.
    // A switch is not finished until the POSTLAUNCH verification completes.
    internal static class SwitchReentryPolicy
    {
        internal static bool CanStart(bool busy, bool verificationPending)
        {
            return !busy && !verificationPending;
        }
    }
}
