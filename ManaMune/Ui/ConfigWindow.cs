using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ManaMune.Ui;

/// <summary>
/// One window: the mapping, which bones it drives, and a readout that shows
/// whether it is actually reaching Customize+.
/// </summary>
public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly Vector4 Good = new(0.45f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 Bad = new(1f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 Warn = new(1f, 0.75f, 0.3f, 1f);
    private static readonly Vector4 Dim = new(0.65f, 0.65f, 0.65f, 1f);

    private readonly Config _config;
    private readonly ManaWatcher _watcher;

    public ConfigWindow(Config config, ManaWatcher watcher)
        : base("ManaMune###ManaMuneMain")
    {
        _config = config;
        _watcher = watcher;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 320),
            MaximumSize = new Vector2(900, 1200),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawStatus();
        ImGui.Separator();
        DrawMapping();
        ImGui.Separator();
        DrawBones();
    }

    private void DrawStatus()
    {
        if (!_watcher.CustomizePlusAvailable)
        {
            ImGui.TextColored(Bad, "Customize+ is not responding.");
            ImGui.TextWrapped("ManaMune drives Customize+ rather than touching the "
                            + "skeleton itself, so nothing happens until it is installed "
                            + "and enabled.");
            return;
        }

        var enabled = _config.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            _config.Enabled = enabled;
            _config.Save();
            _watcher.SettingsChanged();
        }

        if (!enabled)
        {
            ImGui.TextColored(Dim, "Off - your own Customize+ profile is untouched.");
            return;
        }

        var pct = _watcher.LastPercent;
        if (pct < 0)
        {
            ImGui.TextColored(Warn, _config.OnlyMpJobs
                ? "Waiting - not logged in, or on a job that does not spend mana."
                : "Waiting for a character.");
        }
        else
        {
            // Not a printf format string in these bindings, so a lone % is fine.
            ImGui.TextColored(Good, $"Mana {pct}%  ->  scale x{_watcher.LastFactor:0.000}");
        }

        // Which profile is being carried underneath. Getting this wrong is the
        // one failure that silently drops the player's other bone edits, so it
        // is stated outright rather than left to be discovered in game.
        var baseId = _watcher.BaseProfileId;
        if (baseId == null)
        {
            ImGui.TextColored(Dim, "No Customize+ profile underneath - scaling from 1.0.");
        }
        else
        {
            var name = _watcher.BaseProfileName ?? baseId.Value.ToString()[..8];
            ImGui.TextColored(Dim,
                $"On top of: {name} ({_watcher.BaseBoneCount} bones preserved)");
        }

        if (ImGui.Button("Re-detect profile"))
            _watcher.Redetect();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Press this after switching Customize+ profiles.\n"
                           + "While ManaMune is applying, Customize+ reports ManaMune's own\n"
                           + "profile as the active one, so a switch cannot be spotted\n"
                           + "automatically. Zoning and changing job also re-detect.");

        var error = _watcher.LastError;
        if (!string.IsNullOrEmpty(error))
        {
            ImGui.TextColored(Bad, error);
        }
    }

    private void DrawMapping()
    {
        ImGui.TextColored(Dim, "Mapping");

        var atFull = _config.ScaleAtFull;
        if (ImGui.SliderFloat("At full mana", ref atFull, ManaScaler.MinAllowed, 3f, "x%.2f"))
        {
            _config.ScaleAtFull = ManaScaler.Clamp(atFull);
            _config.Save();
            _watcher.SettingsChanged();
        }

        var atEmpty = _config.ScaleAtEmpty;
        if (ImGui.SliderFloat("At empty mana", ref atEmpty, ManaScaler.MinAllowed, 3f, "x%.2f"))
        {
            _config.ScaleAtEmpty = ManaScaler.Clamp(atEmpty);
            _config.Save();
            _watcher.SettingsChanged();
        }

        ImGui.TextColored(Dim, "Multipliers on whatever your own profile already sets.");

        var smooth = _config.Smooth;
        if (ImGui.Checkbox("Smooth", ref smooth))
        {
            _config.Smooth = smooth;
            _config.Save();
            _watcher.SettingsChanged();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Slide to each new size instead of snapping to it.\n\n"
                           + "Mana arrives in lumps - a Red Mage cast is about a sixth\n"
                           + "of the bar in one go - so without this the size steps.\n\n"
                           + "Off is exactly the old behaviour.");

        var invert = _config.Invert;
        if (ImGui.Checkbox("Invert", ref invert))
        {
            _config.Invert = invert;
            _config.Save();
            _watcher.SettingsChanged();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Swap the ends: the 'at empty' value is used at full mana\n"
                           + "and the 'at full' value at empty.");

        var onlyMp = _config.OnlyMpJobs;
        if (ImGui.Checkbox("Only on jobs that spend mana", ref onlyMp))
        {
            _config.OnlyMpJobs = onlyMp;
            _config.Save();
            _watcher.SettingsChanged();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Every job reports 10000 MP these days, so on a Samurai this\n"
                           + "would otherwise sit pinned at the full-mana scale forever.\n"
                           + "With this on, ManaMune gets out of the way instead.");

        var death = (int)_config.DeathBehaviour;
        if (ImGui.Combo("While dead", ref death, (IReadOnlyList<string>)DeathLabels, 3))
        {
            _config.DeathBehaviour = (OnDeath)death;
            _config.Save();
            _watcher.SettingsChanged();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Dying empties the mana bar, so 'Keep following mana' pins\n"
                           + "every corpse at the minimum size.\n\n"
                           + "Turn off - your own Customize+ profile shows instead.\n"
                           + "Freeze - whatever size you died at is the size that stays.\n\n"
                           + "Either way it picks up again the moment you are raised,\n"
                           + "at whatever mana you come back with.");
    }

    private static readonly string[] DeathLabels =
    {
        "Keep following mana",
        "Turn off",
        "Freeze at last size",
    };

    private void DrawBones()
    {
        ImGui.TextColored(Dim, "Bones");

        var vanilla = _config.VanillaBones;
        if (ImGui.Checkbox("Vanilla (j_mune_l / j_mune_r)", ref vanilla))
        {
            _config.VanillaBones = vanilla;
            _config.Save();
            _watcher.SettingsChanged();
        }

        var ivcs = _config.IvcsBones;
        if (ImGui.Checkbox("IVCS (iv_c_mune_l / iv_c_mune_r)", ref ivcs))
        {
            _config.IvcsBones = ivcs;
            _config.Save();
            _watcher.SettingsChanged();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("These sit UNDER the vanilla bones in the skeleton, so they\n"
                           + "already inherit the vanilla scale. Ticking both multiplies\n"
                           + "the effect rather than widening it.");

        var pec = _config.PectoralBones;
        if (ImGui.Checkbox("IVCS pectorals (iv_kyokin_phys_l / _r)", ref pec))
        {
            _config.PectoralBones = pec;
            _config.Save();
            _watcher.SettingsChanged();
        }

        var extra = _config.ExtraBones;
        if (ImGui.InputTextWithHint("##extrabones", "Other bones, comma separated",
                                    ref extra, 512))
        {
            _config.ExtraBones = extra;
            _config.Save();
            _watcher.SettingsChanged();
        }

        var count = _config.BoneNames().Count;
        ImGui.TextColored(count == 0 ? Warn : Dim,
            count == 0 ? "No bones selected - nothing will move."
                       : $"{count} bones driven.");
    }
}
