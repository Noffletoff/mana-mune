using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace ManaMune;

/// <summary>
/// Watches the mana bar and keeps a temporary Customize+ profile in step with
/// it.
///
/// Two things shape this class. First, a temporary profile REPLACES the
/// character's normal profile, so the player's own profile has to be read and
/// carried along rather than overwritten - see <see cref="ProfileMerge"/>.
/// Second, Customize+ reports the temporary profile as the active one once it
/// is applied, so the base has to be captured while nothing of ours is applied
/// and remembered by id from then on.
/// </summary>
public sealed class ManaWatcher : IDisposable
{
    /// <summary>Frames between re-reads of the base profile's contents.</summary>
    private const int BaseRefreshFrames = 120;

    /// <summary>Frames between checks that Customize+ is still there.</summary>
    private const int AvailabilityFrames = 60;

    private readonly Config _config;
    private readonly CustomizePlusBridge _cp;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objects;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;

    private int _baseCountdown;
    private int _availabilityCountdown;

    private Guid? _baseProfileId;
    private IpcProfile _baseProfile = new();
    private Guid? _appliedTempId;

    private int _lastPercent = -1;
    private bool _settingsChanged;

    /// <summary>
    /// Rebuilt only when the settings change. Working it out per frame meant
    /// splitting the free-text bone field on the game thread sixty times a
    /// second to produce the same list every time.
    /// </summary>
    private List<string> _bones;

    // -- what the status line reads --------------------------------------
    public bool CustomizePlusAvailable { get; private set; }
    public Guid? BaseProfileId => _baseProfileId;
    public int LastPercent => _lastPercent;
    public float LastFactor { get; private set; } = 1f;
    public bool Applied => _appliedTempId != null;
    public string? LastError => _cp.LastError;
    public int BaseBoneCount => _baseProfile.Bones.Count;

    /// <summary>
    /// Display name of the profile being carried underneath. Resolved here, on
    /// the framework thread, rather than in the window: the window draws on the
    /// render thread and would otherwise call into Customize+ every frame.
    /// </summary>
    public string? BaseProfileName { get; private set; }

    public ManaWatcher(Config config, CustomizePlusBridge cp, IClientState clientState,
                       IObjectTable objects, IFramework framework, IPluginLog log)
    {
        _config = config;
        _cp = cp;
        _clientState = clientState;
        _objects = objects;
        _framework = framework;
        _log = log;
        _bones = config.BoneNames();

        _framework.Update += OnUpdate;
        _clientState.Login += Redetect;
        _clientState.Logout += OnLogout;
        _clientState.TerritoryChanged += OnTerritoryChanged;
        _clientState.ClassJobChanged += OnClassJobChanged;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        _clientState.Login -= Redetect;
        _clientState.Logout -= OnLogout;
        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _clientState.ClassJobChanged -= OnClassJobChanged;

        Withdraw();
    }

    /// <summary>
    /// Call after any settings edit so the next frame reapplies even though the
    /// mana percent has not moved.
    /// </summary>
    public void SettingsChanged()
    {
        _bones = _config.BoneNames();
        _settingsChanged = true;
    }

    /// <summary>
    /// Drop the temporary profile and forget which profile was underneath, so
    /// the next frame works it out again. This is what to do after switching
    /// Customize+ profiles while the plugin is running.
    /// </summary>
    public void Redetect()
    {
        Withdraw();
        _baseProfileId = null;
        _baseProfile = new IpcProfile();
        _baseCountdown = 0;
    }

    private void OnTerritoryChanged(uint _) => Redetect();

    /// <summary>
    /// A job change can move the player in or out of scope, and Customize+
    /// profiles can be bound to a job, so the base is worked out again.
    /// </summary>
    private void OnClassJobChanged(uint _) => Redetect();

    private void OnLogout(int type, int code)
    {
        _appliedTempId = null;
        _lastPercent = -1;
        _baseProfileId = null;
        _baseProfile = new IpcProfile();
    }

    private void OnUpdate(IFramework framework)
    {
        try
        {
            Tick();
        }
        catch (Exception e)
        {
            // A throw here would fire every single frame.
            _log.Error(e, "ManaMune: update failed");
        }
    }

    private void Tick()
    {
        if (--_availabilityCountdown <= 0)
        {
            _availabilityCountdown = AvailabilityFrames;

            var available = _cp.Available;
            var returned = available && !CustomizePlusAvailable;
            CustomizePlusAvailable = available;

            if (returned)
            {
                // Customize+ was reloaded or updated. Whatever we had applied
                // went with it, so believing it is still there would leave the
                // character unscaled until the mana happened to move.
                Redetect();
            }
        }

        if (!CustomizePlusAvailable)
        {
            // Customize+ has gone; our profile went with it.
            _appliedTempId = null;
            _lastPercent = -1;
            return;
        }

        if (!_config.Enabled)
        {
            Withdraw();
            return;
        }

        var player = _objects.LocalPlayer;
        if (player == null)
        {
            _appliedTempId = null;
            _lastPercent = -1;
            return;
        }

        if (_config.OnlyMpJobs && !MpJobs.UsesMp(player.ClassJob.RowId))
        {
            Withdraw();
            return;
        }

        if (_bones.Count == 0)
        {
            // With nothing to drive, applying would still put a temporary
            // profile in front of the player's own one for no benefit.
            Withdraw();
            return;
        }

        // Checked after the job and bone tests so that "does not apply at all"
        // still wins over "applies, but they are dead".
        switch (DeathPolicy.Decide(player.IsDead, _config.DeathBehaviour))
        {
            case DeathAction.Withdraw:
                Withdraw();
                return;

            case DeathAction.LeaveAlone:
                // Deliberately before the base refresh: a refresh that spotted
                // a change would push a new profile and unfreeze the size.
                return;
        }

        var refreshed = false;
        if (--_baseCountdown <= 0)
        {
            _baseCountdown = BaseRefreshFrames;
            refreshed = RefreshBase();
        }

        var percent = ManaScaler.Bucket(player.CurrentMp, player.MaxMp);
        if (percent == _lastPercent && Applied && !refreshed && !_settingsChanged)
            return;

        _settingsChanged = false;

        var factor = ManaScaler.Factor(percent, _config.ScaleAtEmpty, _config.ScaleAtFull,
                                       _config.Invert);
        var json = ProfileMerge.Serialise(ProfileMerge.Apply(_baseProfile, _bones, factor));

        var id = _cp.SetTemporary(json);
        if (id == null)
            return;

        _appliedTempId = id;
        _lastPercent = percent;
        LastFactor = factor;
    }

    /// <summary>
    /// Make sure we know which profile the player is really wearing, and what
    /// is in it. Returns true when the contents changed and the profile should
    /// be pushed again even though mana has not moved.
    /// </summary>
    private bool RefreshBase()
    {
        if (_baseProfileId == null)
        {
            var active = _cp.ActiveProfileId();

            // Once ours is applied, Customize+ reports OUR profile as active.
            // Adopting it as the base would fold the mana scale into the base
            // and compound it on every refresh.
            if (active != null && active != _appliedTempId)
            {
                _baseProfileId = active;
                BaseProfileName = _cp.ProfileName(active.Value);
            }
        }

        if (_baseProfileId == null)
        {
            // No profile underneath is a perfectly ordinary state.
            var wasEmpty = _baseProfile.Bones.Count == 0;
            _baseProfile = new IpcProfile();
            BaseProfileName = null;
            return !wasEmpty;
        }

        var json = _cp.ProfileJson(_baseProfileId.Value);
        if (json == null)
        {
            // Deleted or renamed out from under us; work it out again.
            _baseProfileId = null;
            _baseProfile = new IpcProfile();
            BaseProfileName = null;
            return true;
        }

        var parsed = ProfileMerge.Parse(json);
        var changed = !SameBones(parsed, _baseProfile);
        _baseProfile = parsed;
        return changed;
    }

    private static bool SameBones(IpcProfile a, IpcProfile b)
    {
        if (a.Bones.Count != b.Bones.Count)
            return false;

        foreach (var (name, bone) in a.Bones)
        {
            if (!b.Bones.TryGetValue(name, out var other))
                return false;

            if (Math.Abs(bone.Scaling.X - other.Scaling.X) > 0.0001f ||
                Math.Abs(bone.Scaling.Y - other.Scaling.Y) > 0.0001f ||
                Math.Abs(bone.Scaling.Z - other.Scaling.Z) > 0.0001f)
                return false;
        }

        return true;
    }

    /// <summary>Remove our temporary profile, leaving the player's own in place.</summary>
    private void Withdraw()
    {
        if (_appliedTempId == null)
        {
            _lastPercent = -1;
            return;
        }

        _cp.DeleteTemporary();
        _appliedTempId = null;
        _lastPercent = -1;

        // The player's own profile becomes visible to Customize+ again, so the
        // next refresh can re-detect it.
        _baseProfileId = null;
        _baseCountdown = 0;
    }
}
