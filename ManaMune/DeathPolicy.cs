namespace ManaMune;

/// <summary>What ManaMune should do while the character is dead.</summary>
public enum OnDeath
{
    /// <summary>
    /// Carry on following the mana bar. Death empties it, so this shrinks to
    /// the minimum and stays there until you are up again.
    /// </summary>
    Track = 0,

    /// <summary>
    /// Withdraw entirely, so the corpse wears the player's own Customize+
    /// profile exactly as if ManaMune were not installed.
    /// </summary>
    Disable = 1,

    /// <summary>
    /// Leave whatever is already applied alone until they get up. The size at
    /// the moment of death is the size that stays.
    /// </summary>
    Freeze = 2,
}

/// <summary>What the update loop should do this frame.</summary>
public enum DeathAction
{
    /// <summary>Proceed normally.</summary>
    Continue,

    /// <summary>Take the temporary profile off.</summary>
    Withdraw,

    /// <summary>Touch nothing at all - not even a base refresh.</summary>
    LeaveAlone,
}

public static class DeathPolicy
{
    /// <summary>
    /// Resurrection needs no handling of its own: the moment the character
    /// stops being dead this returns Continue again, and the ordinary path
    /// re-applies at whatever mana they came back with.
    /// </summary>
    public static DeathAction Decide(bool isDead, OnDeath behaviour)
    {
        if (!isDead)
            return DeathAction.Continue;

        return behaviour switch
        {
            OnDeath.Disable => DeathAction.Withdraw,
            OnDeath.Freeze => DeathAction.LeaveAlone,
            _ => DeathAction.Continue,
        };
    }
}
