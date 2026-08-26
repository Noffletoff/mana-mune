using System.Collections.Generic;

namespace ManaMune;

/// <summary>
/// Which jobs actually spend mana.
///
/// Since Endwalker every job reports a maximum of 10000 MP, so the game cannot
/// be asked "does this job have a mana bar" - a Samurai sits at 100% forever.
/// That is not harmful, but it leaves a temporary Customize+ profile applied
/// while doing nothing, which quietly overrides the profile the player would
/// otherwise be wearing. Knowing the list lets the plugin get out of the way
/// instead.
/// </summary>
public static class MpJobs
{
    // ClassJob sheet row ids.
    private static readonly HashSet<uint> Users = new()
    {
        6,  // Conjurer
        7,  // Thaumaturge
        19, // Paladin
        24, // White Mage
        25, // Black Mage
        26, // Arcanist
        27, // Summoner
        28, // Scholar
        32, // Dark Knight
        33, // Astrologian
        35, // Red Mage
        36, // Blue Mage
        40, // Sage
        42, // Pictomancer
    };

    public static bool UsesMp(uint classJobId) => Users.Contains(classJobId);
}
