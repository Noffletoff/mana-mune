using ManaMune;
using Xunit;

namespace ManaMune.Tests;

public class DeathPolicyTests
{
    [Theory]
    [InlineData(OnDeath.Track)]
    [InlineData(OnDeath.Disable)]
    [InlineData(OnDeath.Freeze)]
    public void AliveAlwaysCarriesOnWhicheverSettingIsChosen(OnDeath behaviour)
        => Assert.Equal(DeathAction.Continue, DeathPolicy.Decide(false, behaviour));

    [Fact]
    public void DeadAndSetToDisableWithdraws()
        => Assert.Equal(DeathAction.Withdraw, DeathPolicy.Decide(true, OnDeath.Disable));

    [Fact]
    public void DeadAndSetToFreezeTouchesNothing()
        => Assert.Equal(DeathAction.LeaveAlone, DeathPolicy.Decide(true, OnDeath.Freeze));

    [Fact]
    public void DeadAndSetToTrackKeepsFollowingTheEmptyBar()
        => Assert.Equal(DeathAction.Continue, DeathPolicy.Decide(true, OnDeath.Track));

    [Fact]
    public void ResurrectionNeedsNoSpecialCase()
    {
        // The only thing that changes on being raised is IsDead going false,
        // and that alone must be enough to resume - there is no separate
        // "revived" branch anywhere for a bug to hide in.
        foreach (var behaviour in new[] { OnDeath.Track, OnDeath.Disable, OnDeath.Freeze })
        {
            var whileDead = DeathPolicy.Decide(true, behaviour);
            var afterRaise = DeathPolicy.Decide(false, behaviour);

            Assert.Equal(DeathAction.Continue, afterRaise);

            if (behaviour != OnDeath.Track)
                Assert.NotEqual(whileDead, afterRaise);
        }
    }

    [Fact]
    public void AnUnknownStoredValueFallsBackToCarryingOn()
    {
        // An old or hand-edited config could hold anything.
        Assert.Equal(DeathAction.Continue, DeathPolicy.Decide(true, (OnDeath)99));
    }
}
