namespace ManaMune;

/// <summary>
/// Whether ManaMune should be driving the character at all right now.
///
/// Both filters answer the same question - "is this a moment the effect is
/// wanted" - so they live together and are decided in one place. When the
/// answer is no the temporary profile comes off entirely, leaving the player
/// wearing their own Customize+ profile rather than a frozen leftover.
/// </summary>
public static class Applicability
{
    public static bool ShouldApply(uint classJobId, bool onlyMpJobs, bool inCombat, bool inCombatOnly)
    {
        if (onlyMpJobs && !MpJobs.UsesMp(classJobId))
            return false;

        if (inCombatOnly && !inCombat)
            return false;

        return true;
    }
}
