using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManaMune;

/// <summary>
/// The JSON a Customize+ temporary profile is made of.
///
/// Mirrors CustomizePlus.Api.Data.IPCCharacterProfile / IPCBoneTransform - the
/// property names here are the contract, so they are spelled exactly as that
/// assembly spells them (note ChildScaleIndependent, which differs from the
/// ChildScalingIndependent found in Customize+'s own template files).
/// </summary>
public sealed class IpcProfile
{
    public Dictionary<string, IpcBone> Bones { get; set; } = new();
}

public sealed class IpcVector
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public IpcVector() { }

    public IpcVector(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public IpcVector Copy() => new(X, Y, Z);
}

public sealed class IpcBone
{
    public IpcVector Translation { get; set; } = new(0, 0, 0);
    public IpcVector Rotation { get; set; } = new(0, 0, 0);
    public IpcVector Scaling { get; set; } = new(1, 1, 1);
    public bool PropagateTranslation { get; set; }
    public bool PropagateRotation { get; set; }
    public bool PropagateScale { get; set; }
    public bool ChildScaleIndependent { get; set; }

    public IpcBone Copy() => new()
    {
        Translation = Translation.Copy(),
        Rotation = Rotation.Copy(),
        Scaling = Scaling.Copy(),
        PropagateTranslation = PropagateTranslation,
        PropagateRotation = PropagateRotation,
        PropagateScale = PropagateScale,
        ChildScaleIndependent = ChildScaleIndependent,
    };
}

/// <summary>
/// Builds the temporary profile that gets pushed to Customize+.
///
/// A temporary profile REPLACES whatever profile the character is wearing -
/// Customize+ resolves exactly one active profile per actor and a temporary one
/// outranks the rest. Sending only the breast bones would therefore silently
/// drop every other edit the player has made. So the player's own profile is
/// read first and used as the base, and the mana factor is MULTIPLIED into
/// whatever scale those bones already carry rather than replacing it. Their
/// shape is preserved; it just breathes with the mana bar.
/// </summary>
public static class ProfileMerge
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    /// <summary>
    /// Parse a profile as handed over by Customize+. Returns an empty profile
    /// for null, blank or unparseable input - a base we cannot read is not a
    /// reason to stop scaling, it just means there is nothing to preserve.
    /// </summary>
    public static IpcProfile Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new IpcProfile();

        try
        {
            var parsed = JsonSerializer.Deserialize<IpcProfile>(json, ReadOptions);
            if (parsed == null)
                return new IpcProfile();

            parsed.Bones ??= new Dictionary<string, IpcBone>();
            return parsed;
        }
        catch (JsonException)
        {
            return new IpcProfile();
        }
    }

    /// <summary>
    /// The base profile with <paramref name="factor"/> multiplied into the
    /// scale of each named bone. The base is never mutated, so the same base
    /// can be reused for every subsequent mana change.
    /// </summary>
    public static IpcProfile Apply(IpcProfile baseProfile, IEnumerable<string> bones, float factor)
    {
        var result = new IpcProfile();
        foreach (var (name, bone) in baseProfile.Bones)
            result.Bones[name] = bone.Copy();

        // Deduplicated: the same bone can arrive from both a preset tick box
        // and the free-text field, and scaling it twice squares the factor.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in bones)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var key = name.Trim();
            if (!seen.Add(key))
                continue;

            if (!result.Bones.TryGetValue(key, out var bone))
            {
                bone = new IpcBone();
                result.Bones[key] = bone;
            }

            bone.Scaling = new IpcVector(
                ManaScaler.Clamp(bone.Scaling.X * factor),
                ManaScaler.Clamp(bone.Scaling.Y * factor),
                ManaScaler.Clamp(bone.Scaling.Z * factor));
        }

        return result;
    }

    public static string Serialise(IpcProfile profile) =>
        JsonSerializer.Serialize(profile, WriteOptions);

    /// <summary>Parse, apply and serialise in one step.</summary>
    public static string Build(string? baseJson, IEnumerable<string> bones, float factor) =>
        Serialise(Apply(Parse(baseJson), bones, factor));

    /// <summary>
    /// Split the free-text "extra bones" field into names. Commas, spaces and
    /// newlines all separate, because all three are what people actually type.
    /// </summary>
    public static List<string> SplitBoneList(string? text)
    {
        var outp = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return outp;

        foreach (var part in text.Split(new[] { ',', ' ', '\t', '\r', '\n', ';' },
                                        StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0 && !outp.Contains(trimmed, StringComparer.Ordinal))
                outp.Add(trimmed);
        }

        return outp;
    }
}
