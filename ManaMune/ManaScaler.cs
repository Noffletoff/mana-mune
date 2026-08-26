using System;

namespace ManaMune;

/// <summary>
/// The whole mapping from a mana bar to a bone scale, with no game or Dalamud
/// types anywhere in it so it can be tested with the game closed.
/// </summary>
public static class ManaScaler
{
    /// <summary>
    /// Customize+ clamps bone scale itself; these are the bounds the UI offers
    /// and the bounds a saved config is pulled back into. Zero scale collapses
    /// a bone and mangles everything parented to it, so the floor is never 0.
    /// </summary>
    public const float MinAllowed = 0.05f;
    public const float MaxAllowed = 5.0f;

    /// <summary>
    /// Current mana as a whole percent, 0-100.
    ///
    /// Whole percent rather than the raw value on purpose: it is the change
    /// detector for the whole plugin. Standing still with full mana produces
    /// the same bucket every frame, so no IPC call is made at all.
    ///
    /// A zero maximum happens while zoning and between logins. Treating that
    /// as full avoids a visible collapse to the minimum every loading screen.
    /// </summary>
    public static int Bucket(uint current, uint max)
    {
        if (max == 0)
            return 100;

        if (current >= max)
            return 100;

        return (int)(current * 100L / max);
    }

    /// <summary>
    /// The scale multiplier for a given mana percent.
    ///
    /// Normally full mana gives <paramref name="atFull"/> and empty gives
    /// <paramref name="atEmpty"/>, interpolated linearly between. Inverted
    /// swaps which end is which, so the two values keep their names whichever
    /// way round the mapping runs.
    /// </summary>
    public static float Factor(int percent, float atEmpty, float atFull, bool invert)
    {
        var t = Math.Clamp(percent, 0, 100) / 100f;
        if (invert)
            t = 1f - t;

        var from = Clamp(atEmpty);
        var to = Clamp(atFull);
        return from + ((to - from) * t);
    }

    /// <summary>Pull a scale into the range Customize+ will accept.</summary>
    public static float Clamp(float scale)
    {
        if (float.IsNaN(scale))
            return 1f;

        return Math.Clamp(scale, MinAllowed, MaxAllowed);
    }
}
