using System.Collections.Generic;
using System.Text.Json;
using ManaMune;
using Xunit;

namespace ManaMune.Tests;

public class ParseTests
{
    /// <summary>A profile in the shape Customize+ actually hands back.</summary>
    private const string Sample = """
    {
      "Bones": {
        "j_mune_l": {
          "PropagateTranslation": false,
          "PropagateRotation": false,
          "PropagateScale": false,
          "ChildScaleIndependent": false,
          "Translation": { "X": 0.0, "Y": 0.0, "Z": 0.0 },
          "Rotation": { "X": 0.0, "Y": 0.0, "Z": -18.9 },
          "Scaling": { "X": 1.4, "Y": 1.4, "Z": 1.4 }
        },
        "j_kosi": {
          "Translation": { "X": 0.0, "Y": 0.1, "Z": 0.0 },
          "Rotation": { "X": 0.0, "Y": 0.0, "Z": 0.0 },
          "Scaling": { "X": 1.0, "Y": 1.05, "Z": 1.0 }
        }
      }
    }
    """;

    [Fact]
    public void ReadsBonesAndValues()
    {
        var p = ProfileMerge.Parse(Sample);
        Assert.Equal(2, p.Bones.Count);
        Assert.Equal(1.4f, p.Bones["j_mune_l"].Scaling.X, 4);
        Assert.Equal(-18.9f, p.Bones["j_mune_l"].Rotation.Z, 4);
        Assert.Equal(0.1f, p.Bones["j_kosi"].Translation.Y, 4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"Bones\": ")]
    public void UnusableInputGivesAnEmptyProfileRatherThanThrowing(string? json)
    {
        var p = ProfileMerge.Parse(json);
        Assert.Empty(p.Bones);
    }

    [Fact]
    public void MissingBonesKeyGivesAnEmptyDictionaryNotNull()
    {
        var p = ProfileMerge.Parse("{}");
        Assert.NotNull(p.Bones);
        Assert.Empty(p.Bones);
    }

    [Fact]
    public void AnExplicitNullBonesKeyIsAlsoSafe()
    {
        var p = ProfileMerge.Parse("{ \"Bones\": null }");
        Assert.NotNull(p.Bones);
        Assert.Empty(p.Bones);
    }
}

public class ApplyTests
{
    private static IpcProfile Base(string bone, float scale)
    {
        var p = new IpcProfile();
        p.Bones[bone] = new IpcBone { Scaling = new IpcVector(scale, scale, scale) };
        return p;
    }

    [Fact]
    public void MultipliesIntoAnExistingScaleRatherThanReplacingIt()
    {
        // The whole point: a 1.4 shape at half mana is 1.4 * 0.8, not 0.8.
        var result = ProfileMerge.Apply(Base("j_mune_l", 1.4f), new[] { "j_mune_l" }, 0.8f);
        Assert.Equal(1.12f, result.Bones["j_mune_l"].Scaling.X, 4);
    }

    [Fact]
    public void AddsBonesTheBaseDoesNotHave()
    {
        var result = ProfileMerge.Apply(new IpcProfile(), new[] { "iv_c_mune_r" }, 0.75f);
        Assert.Equal(0.75f, result.Bones["iv_c_mune_r"].Scaling.X, 4);
        Assert.Equal(0.75f, result.Bones["iv_c_mune_r"].Scaling.Z, 4);
    }

    [Fact]
    public void KeepsEveryOtherBoneUntouched()
    {
        // A temporary profile replaces the active one, so anything dropped here
        // is an edit the player silently loses while the plugin runs.
        var b = Base("j_mune_l", 1.4f);
        b.Bones["j_kosi"] = new IpcBone
        {
            Scaling = new IpcVector(1f, 1.05f, 1f),
            Translation = new IpcVector(0f, 0.1f, 0f),
        };

        var result = ProfileMerge.Apply(b, new[] { "j_mune_l" }, 0.5f);

        Assert.Equal(2, result.Bones.Count);
        Assert.Equal(1.05f, result.Bones["j_kosi"].Scaling.Y, 4);
        Assert.Equal(0.1f, result.Bones["j_kosi"].Translation.Y, 4);
    }

    [Fact]
    public void DoesNotMutateTheBase()
    {
        // The base is captured once and reused for every mana change. If Apply
        // wrote through it, the scale would compound every single frame and
        // shrink the character to nothing within seconds.
        var b = Base("j_mune_l", 1.0f);

        for (var i = 0; i < 100; i++)
            ProfileMerge.Apply(b, new[] { "j_mune_l" }, 0.9f);

        Assert.Equal(1.0f, b.Bones["j_mune_l"].Scaling.X, 4);
    }

    [Fact]
    public void RepeatedApplicationIsStable()
    {
        var b = Base("j_mune_l", 1.0f);
        var first = ProfileMerge.Apply(b, new[] { "j_mune_l" }, 0.9f);
        var second = ProfileMerge.Apply(b, new[] { "j_mune_l" }, 0.9f);
        Assert.Equal(first.Bones["j_mune_l"].Scaling.X,
                     second.Bones["j_mune_l"].Scaling.X, 4);
    }

    [Fact]
    public void RotationAndTranslationOnTheScaledBoneSurvive()
    {
        var b = new IpcProfile();
        b.Bones["j_mune_l"] = new IpcBone
        {
            Scaling = new IpcVector(1.2f, 1.2f, 1.2f),
            Rotation = new IpcVector(0f, 0f, -18.9f),
            PropagateScale = true,
        };

        var result = ProfileMerge.Apply(b, new[] { "j_mune_l" }, 0.5f);

        Assert.Equal(-18.9f, result.Bones["j_mune_l"].Rotation.Z, 4);
        Assert.True(result.Bones["j_mune_l"].PropagateScale);
    }

    [Fact]
    public void ResultIsNeverCollapsedToZero()
    {
        var result = ProfileMerge.Apply(Base("j_mune_l", 0.0f), new[] { "j_mune_l" }, 0.5f);
        Assert.True(result.Bones["j_mune_l"].Scaling.X >= ManaScaler.MinAllowed);
    }

    [Fact]
    public void BlankBoneNamesAreIgnored()
    {
        var result = ProfileMerge.Apply(new IpcProfile(), new[] { "", "   ", "j_mune_r" }, 0.5f);
        Assert.Single(result.Bones);
        Assert.True(result.Bones.ContainsKey("j_mune_r"));
    }

    [Fact]
    public void BoneNamesAreTrimmed()
    {
        var result = ProfileMerge.Apply(new IpcProfile(), new[] { "  j_mune_l  " }, 0.5f);
        Assert.True(result.Bones.ContainsKey("j_mune_l"));
    }

    [Fact]
    public void ABoneNamedTwiceIsOnlyScaledOnce()
    {
        var result = ProfileMerge.Apply(Base("j_mune_l", 1.0f),
                                        new[] { "j_mune_l", "j_mune_l" }, 0.5f);
        Assert.Equal(0.5f, result.Bones["j_mune_l"].Scaling.X, 4);
    }
}

public class SerialiseTests
{
    [Fact]
    public void WritesThePropertyNamesCustomizePlusExpects()
    {
        var json = ProfileMerge.Build(null, new[] { "j_mune_l" }, 0.8f);

        using var doc = JsonDocument.Parse(json);
        var bone = doc.RootElement.GetProperty("Bones").GetProperty("j_mune_l");

        // Spelled as CustomizePlus.Api.Data.IPCBoneTransform spells them.
        Assert.True(bone.TryGetProperty("Translation", out _));
        Assert.True(bone.TryGetProperty("Rotation", out _));
        Assert.True(bone.TryGetProperty("Scaling", out _));
        Assert.True(bone.TryGetProperty("PropagateTranslation", out _));
        Assert.True(bone.TryGetProperty("PropagateRotation", out _));
        Assert.True(bone.TryGetProperty("PropagateScale", out _));
        Assert.True(bone.TryGetProperty("ChildScaleIndependent", out _));

        var scaling = bone.GetProperty("Scaling");
        Assert.Equal(0.8f, scaling.GetProperty("X").GetSingle(), 4);
        Assert.Equal(0.8f, scaling.GetProperty("Y").GetSingle(), 4);
        Assert.Equal(0.8f, scaling.GetProperty("Z").GetSingle(), 4);
    }

    [Fact]
    public void RoundTripsThroughItsOwnFormat()
    {
        var once = ProfileMerge.Build(null, new[] { "j_mune_l" }, 1.4f);
        var twice = ProfileMerge.Parse(once);
        Assert.Equal(1.4f, twice.Bones["j_mune_l"].Scaling.X, 4);
    }

    [Fact]
    public void UnusedBonesAreNotEmitted()
    {
        var json = ProfileMerge.Build(null, new List<string>(), 0.8f);
        using var doc = JsonDocument.Parse(json);
        Assert.Empty(doc.RootElement.GetProperty("Bones").EnumerateObject());
    }
}

public class SplitBoneListTests
{
    [Theory]
    [InlineData("a,b", new[] { "a", "b" })]
    [InlineData("a, b", new[] { "a", "b" })]
    [InlineData("a b", new[] { "a", "b" })]
    [InlineData("a\nb", new[] { "a", "b" })]
    [InlineData("a;b", new[] { "a", "b" })]
    [InlineData(" a ,, b ", new[] { "a", "b" })]
    public void SeparatesOnAnythingReasonable(string input, string[] expected)
        => Assert.Equal(expected, ProfileMerge.SplitBoneList(input));

    [Fact]
    public void DropsDuplicates()
        => Assert.Equal(new[] { "a", "b" }, ProfileMerge.SplitBoneList("a,b,a"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyInputGivesNothing(string? input)
        => Assert.Empty(ProfileMerge.SplitBoneList(input));
}
