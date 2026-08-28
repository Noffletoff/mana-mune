using ManaMune;
using Xunit;

namespace ManaMune.Tests;

public class ApplicabilityTests
{
    private const uint Blm = 25;
    private const uint Samurai = 34;

    [Fact]
    public void BothFiltersOffMeansAlwaysOn()
    {
        Assert.True(Applicability.ShouldApply(Samurai, onlyMpJobs: false,
                                              inCombat: false, inCombatOnly: false));
    }

    [Fact]
    public void TheJobFilterStillWorksOnItsOwn()
    {
        Assert.True(Applicability.ShouldApply(Blm, true, false, false));
        Assert.False(Applicability.ShouldApply(Samurai, true, false, false));
    }

    [Fact]
    public void TheCombatFilterWorksOnItsOwn()
    {
        Assert.True(Applicability.ShouldApply(Samurai, false, inCombat: true, inCombatOnly: true));
        Assert.False(Applicability.ShouldApply(Samurai, false, inCombat: false, inCombatOnly: true));
    }

    [Fact]
    public void CombatDoesNotMatterWhenTheFilterIsOff()
    {
        Assert.True(Applicability.ShouldApply(Blm, true, inCombat: false, inCombatOnly: false));
        Assert.True(Applicability.ShouldApply(Blm, true, inCombat: true, inCombatOnly: false));
    }

    [Fact]
    public void EitherFilterAloneIsEnoughToRuleItOut()
    {
        // A caster out of combat, and a Samurai in combat: both filters on,
        // each blocked by a different one.
        Assert.False(Applicability.ShouldApply(Blm, true, inCombat: false, inCombatOnly: true));
        Assert.False(Applicability.ShouldApply(Samurai, true, inCombat: true, inCombatOnly: true));
    }

    [Fact]
    public void BothSatisfiedMeansOn()
        => Assert.True(Applicability.ShouldApply(Blm, true, inCombat: true, inCombatOnly: true));

    [Fact]
    public void NoJobLoadedIsNotAnMpJob()
    {
        // Between logins ClassJob reads 0.
        Assert.False(Applicability.ShouldApply(0, true, true, false));
        Assert.True(Applicability.ShouldApply(0, false, true, false));
    }
}
