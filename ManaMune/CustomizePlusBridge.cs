using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace ManaMune;

/// <summary>
/// Everything this plugin says to Customize+.
///
/// The gate names and signatures below were read off
/// CustomizePlus.Api.CustomizePlusIpc in the installed assembly rather than
/// copied from documentation - a wrong parameter type on a Dalamud call gate
/// does not fail loudly, it just never connects. Error codes are
/// CustomizePlus.Api.Enums.ErrorCode, where 0 is Success.
/// </summary>
public sealed class CustomizePlusBridge
{
    public const int Success = 0;
    public const int ProfileNotFound = 3;

    /// <summary>The local player is always object index 0.</summary>
    public const ushort LocalPlayerIndex = 0;

    private readonly IPluginLog _log;

    private readonly ICallGateSubscriber<(int, int)> _apiVersion;
    private readonly ICallGateSubscriber<bool> _isValid;
    private readonly ICallGateSubscriber<ushort, (int, Guid?)> _activeProfileId;
    private readonly ICallGateSubscriber<Guid, (int, string)> _profileById;
    private readonly ICallGateSubscriber<ushort, string, (int, Guid?)> _setTemporary;
    private readonly ICallGateSubscriber<ushort, int> _deleteTemporary;
    private readonly ICallGateSubscriber<
        IList<(Guid, string, string, List<(string, ushort, byte, ushort)>, int, bool)>> _profileList;

    /// <summary>Why the last call failed, for the status line. Null when fine.</summary>
    public string? LastError { get; private set; }

    public CustomizePlusBridge(IDalamudPluginInterface pi, IPluginLog log)
    {
        _log = log;

        _apiVersion      = pi.GetIpcSubscriber<(int, int)>("CustomizePlus.General.GetApiVersion");
        _isValid         = pi.GetIpcSubscriber<bool>("CustomizePlus.General.IsValid");
        _activeProfileId = pi.GetIpcSubscriber<ushort, (int, Guid?)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
        _profileById     = pi.GetIpcSubscriber<Guid, (int, string)>("CustomizePlus.Profile.GetByUniqueId");
        _setTemporary    = pi.GetIpcSubscriber<ushort, string, (int, Guid?)>("CustomizePlus.Profile.SetTemporaryProfileOnCharacter");
        _deleteTemporary = pi.GetIpcSubscriber<ushort, int>("CustomizePlus.Profile.DeleteTemporaryProfileOnCharacter");
        _profileList     = pi.GetIpcSubscriber<
            IList<(Guid, string, string, List<(string, ushort, byte, ushort)>, int, bool)>>("CustomizePlus.Profile.GetList");
    }

    /// <summary>
    /// Whether Customize+ is loaded and willing to talk. Cheap enough to poll,
    /// but the plugin only asks about once a second.
    /// </summary>
    public bool Available
    {
        get
        {
            try
            {
                return _isValid.InvokeFunc();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public (int Breaking, int Feature)? ApiVersion()
    {
        try
        {
            return _apiVersion.InvokeFunc();
        }
        catch (Exception e)
        {
            Fail("reading the Customize+ API version", e);
            return null;
        }
    }

    /// <summary>The profile Customize+ is currently applying to the player.</summary>
    public Guid? ActiveProfileId()
    {
        try
        {
            var (code, id) = _activeProfileId.InvokeFunc(LocalPlayerIndex);
            if (code == Success)
                return id;

            // No profile at all is an ordinary state, not a failure.
            if (code != ProfileNotFound)
                LastError = $"Customize+ returned error {code} asking for the active profile";

            return null;
        }
        catch (Exception e)
        {
            Fail("asking Customize+ for the active profile", e);
            return null;
        }
    }

    /// <summary>The bone data of a profile, as JSON, or null if it is gone.</summary>
    public string? ProfileJson(Guid id)
    {
        try
        {
            var (code, json) = _profileById.InvokeFunc(id);
            if (code == Success)
                return json;

            if (code != ProfileNotFound)
                LastError = $"Customize+ returned error {code} reading a profile";

            return null;
        }
        catch (Exception e)
        {
            Fail("reading a Customize+ profile", e);
            return null;
        }
    }

    /// <summary>Apply a temporary profile. Returns its id, or null on failure.</summary>
    public Guid? SetTemporary(string profileJson)
    {
        try
        {
            var (code, id) = _setTemporary.InvokeFunc(LocalPlayerIndex, profileJson);
            if (code != Success)
            {
                LastError = $"Customize+ refused the profile (error {code})";
                return null;
            }

            LastError = null;
            return id;
        }
        catch (Exception e)
        {
            Fail("applying a temporary profile", e);
            return null;
        }
    }

    public void DeleteTemporary()
    {
        try
        {
            _deleteTemporary.InvokeFunc(LocalPlayerIndex);
        }
        catch (Exception e)
        {
            // Nothing to be done about it, and it happens naturally when
            // Customize+ unloads first at shutdown.
            _log.Debug(e, "ManaMune: removing the temporary profile failed");
        }
    }

    /// <summary>Profile id to display name, for the status line.</summary>
    public string? ProfileName(Guid id)
    {
        foreach (var entry in ProfileList())
        {
            if (entry.Item1 != id)
                continue;

            return !string.IsNullOrWhiteSpace(entry.Item2) ? entry.Item2
                 : !string.IsNullOrWhiteSpace(entry.Item3) ? entry.Item3
                 : null;
        }

        return null;
    }

    public IList<(Guid, string, string, List<(string, ushort, byte, ushort)>, int, bool)> ProfileList()
    {
        try
        {
            return _profileList.InvokeFunc();
        }
        catch (Exception e)
        {
            Fail("listing Customize+ profiles", e);
            return new List<(Guid, string, string, List<(string, ushort, byte, ushort)>, int, bool)>();
        }
    }

    private void Fail(string what, Exception e)
    {
        LastError = $"Failed {what}: {e.Message}";
        _log.Debug(e, $"ManaMune: failed {what}");
    }
}
