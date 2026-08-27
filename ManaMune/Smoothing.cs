using System;

namespace ManaMune;

/// <summary>
/// Eases the displayed scale toward the one the mana bar is asking for.
///
/// Exponential smoothing rather than a fixed-duration tween: mana keeps moving
/// while the ease is still running - regen ticks every three seconds, casts
/// back to back - and a tween would restart from the top each time and stutter.
/// This just re-aims at the new target without a discontinuity.
///
/// Framerate independent by construction. The per-frame factor is derived from
/// the elapsed time rather than assumed, so the same ease takes the same wall
/// time at 30fps and at 144fps.
/// </summary>
public static class Smoothing
{
    /// <summary>
    /// Below this, two scales are the same as far as anyone can see. Used both
    /// to stop easing and to decide a new value is not worth sending.
    /// </summary>
    public const float Epsilon = 0.0005f;

    /// <summary>
    /// How long the ease takes when it is switched on. Short enough to keep up
    /// with a cast, long enough that a Red Mage's sixteen-percent chunk arrives
    /// as a slide rather than a step.
    /// </summary>
    public const float DefaultSeconds = 0.4f;

    /// <summary>
    /// How much of the remaining distance is closed per unit of the time
    /// constant. Dividing the user-facing "arrive in N seconds" by this gives
    /// the exponential's tau, so that after N seconds about 95% of the distance
    /// is gone - close enough that Epsilon has usually already snapped it.
    /// </summary>
    private const float ArrivalConstant = 3f;

    /// <summary>One frame of easing.</summary>
    public static float Step(float current, float target, float deltaSeconds, float arriveSeconds)
    {
        // Smoothing off: the slider's zero is exactly the old behaviour, not an
        // almost-instant approximation of it.
        if (arriveSeconds <= 0f)
            return target;

        // A stalled or nonsense frame time must not move anything. UpdateDelta
        // can be zero on the first frame, and negative would run the ease
        // backwards.
        if (!(deltaSeconds > 0f))
            return current;

        if (float.IsNaN(current))
            return target;

        var tau = arriveSeconds / ArrivalConstant;
        var alpha = 1f - MathF.Exp(-deltaSeconds / tau);

        // A very long frame - alt-tab, a loading screen - closes the whole
        // distance rather than overshooting it.
        if (alpha >= 1f || float.IsNaN(alpha))
            return target;

        var next = current + ((target - current) * alpha);

        // Land exactly rather than approaching forever, so the resting state
        // sends nothing at all.
        return Differs(next, target) ? next : target;
    }

    /// <summary>Whether two scales are far enough apart to be worth acting on.</summary>
    public static bool Differs(float a, float b) => MathF.Abs(a - b) >= Epsilon;

    /// <summary>The ease duration for a given setting of the toggle.</summary>
    public static float SecondsFor(bool on) => on ? DefaultSeconds : 0f;
}
