using ManaMune;
using Xunit;

namespace ManaMune.Tests;

public class BucketTests
{
    [Theory]
    [InlineData(10000u, 10000u, 100)]
    [InlineData(0u, 10000u, 0)]
    [InlineData(5000u, 10000u, 50)]
    [InlineData(9999u, 10000u, 99)]
    [InlineData(1u, 10000u, 0)]
    public void MapsManaToWholePercent(uint current, uint max, int expected)
        => Assert.Equal(expected, ManaScaler.Bucket(current, max));

    [Fact]
    public void ZeroMaximumReadsAsFull()
    {
        // Happens while zoning and between logins. Reading it as empty would
        // collapse the character to the minimum scale on every loading screen.
        Assert.Equal(100, ManaScaler.Bucket(0, 0));
    }

    [Fact]
    public void CurrentAboveMaximumIsStillFull()
        => Assert.Equal(100, ManaScaler.Bucket(12000, 10000));

    [Fact]
    public void NeverExceedsPercentRange()
    {
        for (uint mp = 0; mp <= 10000; mp += 137)
        {
            var pct = ManaScaler.Bucket(mp, 10000);
            Assert.InRange(pct, 0, 100);
        }
    }

    [Fact]
    public void LargeValuesDoNotOverflow()
    {
        // current * 100 overflows a uint above ~42.9 million; the arithmetic
        // must widen before multiplying.
        Assert.Equal(50, ManaScaler.Bucket(2_000_000_000u, 4_000_000_000u));
    }
}

public class FactorTests
{
    [Fact]
    public void FullManaGivesTheFullValue()
        => Assert.Equal(1.0f, ManaScaler.Factor(100, 0.6f, 1.0f, invert: false), 4);

    [Fact]
    public void EmptyManaGivesTheEmptyValue()
        => Assert.Equal(0.6f, ManaScaler.Factor(0, 0.6f, 1.0f, invert: false), 4);

    [Fact]
    public void HalfManaSitsHalfway()
        => Assert.Equal(0.8f, ManaScaler.Factor(50, 0.6f, 1.0f, invert: false), 4);

    [Fact]
    public void InvertSwapsTheEnds()
    {
        Assert.Equal(0.6f, ManaScaler.Factor(100, 0.6f, 1.0f, invert: true), 4);
        Assert.Equal(1.0f, ManaScaler.Factor(0, 0.6f, 1.0f, invert: true), 4);
    }

    [Fact]
    public void InvertLeavesTheMidpointAlone()
        => Assert.Equal(ManaScaler.Factor(50, 0.6f, 1.0f, false),
                        ManaScaler.Factor(50, 0.6f, 1.0f, true), 4);

    [Fact]
    public void PercentOutsideRangeIsClamped()
    {
        Assert.Equal(1.0f, ManaScaler.Factor(150, 0.6f, 1.0f, false), 4);
        Assert.Equal(0.6f, ManaScaler.Factor(-20, 0.6f, 1.0f, false), 4);
    }

    [Fact]
    public void AnEmptyValueAboveTheFullValueStillInterpolates()
    {
        // Nothing stops the two sliders crossing over; the result should just
        // run the other way rather than misbehave.
        Assert.Equal(1.4f, ManaScaler.Factor(0, 1.4f, 0.8f, false), 4);
        Assert.Equal(0.8f, ManaScaler.Factor(100, 1.4f, 0.8f, false), 4);
    }

    [Fact]
    public void ResultIsNeverZeroEvenWhenAskedFor()
    {
        // A zero bone scale collapses everything parented to it.
        var f = ManaScaler.Factor(0, 0f, 1f, false);
        Assert.True(f >= ManaScaler.MinAllowed, $"factor was {f}");
    }
}

public class ClampTests
{
    [Fact]
    public void PullsIntoRange()
    {
        Assert.Equal(ManaScaler.MinAllowed, ManaScaler.Clamp(0f));
        Assert.Equal(ManaScaler.MaxAllowed, ManaScaler.Clamp(500f));
        Assert.Equal(1.25f, ManaScaler.Clamp(1.25f));
    }

    [Fact]
    public void NotANumberBecomesUnscaled()
        => Assert.Equal(1f, ManaScaler.Clamp(float.NaN));
}

public class MpJobTests
{
    [Theory]
    [InlineData(24u)] // White Mage
    [InlineData(25u)] // Black Mage
    [InlineData(35u)] // Red Mage
    [InlineData(40u)] // Sage
    [InlineData(42u)] // Pictomancer
    [InlineData(19u)] // Paladin
    [InlineData(32u)] // Dark Knight
    public void CastersAndMpTanksCount(uint job) => Assert.True(MpJobs.UsesMp(job));

    [Theory]
    [InlineData(34u)] // Samurai
    [InlineData(21u)] // Warrior
    [InlineData(37u)] // Gunbreaker
    [InlineData(31u)] // Machinist
    [InlineData(41u)] // Viper
    [InlineData(0u)]  // no job / not loaded
    [InlineData(16u)] // Miner
    public void EverythingElseDoesNot(uint job) => Assert.False(MpJobs.UsesMp(job));
}
