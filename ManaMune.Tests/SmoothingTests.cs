using ManaMune;
using Xunit;

namespace ManaMune.Tests;

public class SmoothingTests
{
    private const float Ease = 0.4f;

    [Fact]
    public void OffIsExactlyInstant()
    {
        // The toggle's "off" has to be the old behaviour precisely, not a very
        // fast ease that still costs extra profile pushes.
        Assert.Equal(1.4f, Smoothing.Step(0.6f, 1.4f, 0.016f, 0f));
        Assert.Equal(0f, Smoothing.SecondsFor(false));
        Assert.Equal(Smoothing.DefaultSeconds, Smoothing.SecondsFor(true));
    }

    [Fact]
    public void MovesTowardTheTarget()
    {
        var next = Smoothing.Step(0.6f, 1.0f, 0.016f, Ease);
        Assert.True(next > 0.6f, $"did not move: {next}");
        Assert.True(next < 1.0f, $"arrived instantly: {next}");
    }

    [Fact]
    public void MovesDownwardToo()
    {
        var next = Smoothing.Step(1.0f, 0.6f, 0.016f, Ease);
        Assert.True(next < 1.0f && next > 0.6f, $"{next}");
    }

    [Fact]
    public void NeverOvershoots()
    {
        var v = 0.6f;
        for (var i = 0; i < 500; i++)
        {
            v = Smoothing.Step(v, 1.0f, 0.05f, Ease);
            Assert.InRange(v, 0.6f, 1.0f);
        }
    }

    [Fact]
    public void ArrivesAndStops()
    {
        var v = 0.6f;
        for (var i = 0; i < 200; i++)
            v = Smoothing.Step(v, 1.0f, 0.016f, Ease);

        // Landing exactly matters: an asymptotic approach would keep sending
        // new profiles to Customize+ forever.
        Assert.Equal(1.0f, v);
        Assert.False(Smoothing.Differs(v, 1.0f));
    }

    [Fact]
    public void ArrivesInRoughlyTheStatedTime()
    {
        var v = 0f;
        var frames = (int)(Ease / 0.016f);
        for (var i = 0; i < frames; i++)
            v = Smoothing.Step(v, 1f, 0.016f, Ease);

        // ~95% of the way after the stated duration.
        Assert.InRange(v, 0.9f, 1.0f);
    }

    [Fact]
    public void IsFramerateIndependent()
    {
        // The whole point. One long frame and many short frames covering the
        // same wall time must land in the same place, or the effect runs at a
        // different speed on a different machine.
        var slow = 0.6f;
        slow = Smoothing.Step(slow, 1.4f, 0.1f, Ease);

        var fast = 0.6f;
        for (var i = 0; i < 10; i++)
            fast = Smoothing.Step(fast, 1.4f, 0.01f, Ease);

        Assert.Equal(slow, fast, 4);
    }

    [Fact]
    public void IsFramerateIndependentOverAWholeEase()
    {
        var at30 = 0.6f;
        for (var i = 0; i < 12; i++)
            at30 = Smoothing.Step(at30, 1.4f, 1f / 30f, Ease);

        var at144 = 0.6f;
        for (var i = 0; i < 58; i++)
            at144 = Smoothing.Step(at144, 1.4f, 1f / 144f, Ease);

        // 12/30s and 58/144s are both ~0.4s of wall time.
        Assert.Equal(at30, at144, 2);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.016f)]
    [InlineData(float.NaN)]
    public void AStalledOrNonsenseFrameChangesNothing(float delta)
        => Assert.Equal(0.6f, Smoothing.Step(0.6f, 1.4f, delta, Ease));

    [Fact]
    public void AVeryLongFrameClosesTheDistanceWithoutOvershooting()
    {
        // Alt-tab, a loading screen: the delta can be enormous.
        Assert.Equal(1.4f, Smoothing.Step(0.6f, 1.4f, 600f, Ease));
    }

    [Fact]
    public void RetargetingMidEaseIsContinuous()
    {
        // Mana moves again while the ease is running. The value must carry on
        // from where it is, not jump.
        var v = 0.6f;
        for (var i = 0; i < 5; i++)
            v = Smoothing.Step(v, 1.4f, 0.016f, Ease);

        var before = v;
        var after = Smoothing.Step(v, 0.8f, 0.016f, Ease);

        Assert.True(Smoothing.Differs(after, before) is false || after < before,
            "retargeting downward should not jump upward");
        Assert.InRange(after, 0.8f, before);
    }

    [Fact]
    public void ACorruptCurrentValueRecovers()
        => Assert.Equal(1.0f, Smoothing.Step(float.NaN, 1.0f, 0.016f, Ease));

    [Fact]
    public void DiffersUsesTheSharedEpsilon()
    {
        Assert.False(Smoothing.Differs(1.0f, 1.0f + (Smoothing.Epsilon / 2f)));
        Assert.True(Smoothing.Differs(1.0f, 1.0f + (Smoothing.Epsilon * 2f)));
    }

    [Fact]
    public void DiffersIsSymmetric()
    {
        Assert.Equal(Smoothing.Differs(1.0f, 1.2f), Smoothing.Differs(1.2f, 1.0f));
        Assert.Equal(Smoothing.Differs(1.0f, 1.0f), Smoothing.Differs(1.0f, 1.0f));
    }
}
