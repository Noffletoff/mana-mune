using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace ManaMune;

public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    /// <summary>Multiplier applied to the bones at 0% mana.</summary>
    public float ScaleAtEmpty { get; set; } = 0.6f;

    /// <summary>Multiplier applied to the bones at 100% mana.</summary>
    public float ScaleAtFull { get; set; } = 1.0f;

    /// <summary>Run the mapping the other way: small when full, large when empty.</summary>
    public bool Invert { get; set; }

    /// <summary>
    /// Leave the character alone on jobs that never spend mana. Every job
    /// reports 10000 MP these days, so without this a Samurai would sit pinned
    /// at the full-mana scale with a temporary profile applied for no reason.
    /// </summary>
    public bool OnlyMpJobs { get; set; } = true;

    /// <summary>The vanilla breast bones. On by default; everything else is opt-in.</summary>
    public bool VanillaBones { get; set; } = true;

    /// <summary>
    /// IVCS breast bones. These are CHILDREN of the vanilla ones, so ticking
    /// both compounds the effect rather than widening it.
    /// </summary>
    public bool IvcsBones { get; set; }

    /// <summary>IVCS pectoral physics bones.</summary>
    public bool PectoralBones { get; set; }

    /// <summary>Anything else, typed by hand.</summary>
    public string ExtraBones { get; set; } = string.Empty;

    public static readonly string[] Vanilla = { "j_mune_l", "j_mune_r" };
    public static readonly string[] Ivcs = { "iv_c_mune_l", "iv_c_mune_r" };
    public static readonly string[] Pectoral = { "iv_kyokin_phys_l", "iv_kyokin_phys_r" };

    [NonSerialized] private IDalamudPluginInterface? _pi;

    public void Initialise(IDalamudPluginInterface pi)
    {
        _pi = pi;
        ScaleAtEmpty = ManaScaler.Clamp(ScaleAtEmpty);
        ScaleAtFull = ManaScaler.Clamp(ScaleAtFull);
    }

    public void Save() => _pi?.SavePluginConfig(this);

    /// <summary>Every bone the current settings ask for, in a stable order.</summary>
    public List<string> BoneNames()
    {
        var names = new List<string>();

        if (VanillaBones)
            names.AddRange(Vanilla);
        if (IvcsBones)
            names.AddRange(Ivcs);
        if (PectoralBones)
            names.AddRange(Pectoral);

        names.AddRange(ProfileMerge.SplitBoneList(ExtraBones));
        return names;
    }
}
