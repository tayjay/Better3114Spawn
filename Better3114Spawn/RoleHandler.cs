using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp3114Events;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerStatsSystem;
using UnityEngine;

namespace Better3114Spawn;

public class RoleHandler
{

    public void OnRevealing(Scp3114RevealingEventArgs ev)
    {
        if (ev.Forced) return;
        if (Round.Duration >= TimeSpan.FromMinutes(Core.Instance.Config.Scp3114PreventRevealTime)) return;
        ev.IsAllowed = false;
        ev.Player.SendHint($"You must wait {(TimeSpan.FromMinutes(Core.Instance.Config.Scp3114PreventRevealTime) - Round.Duration):mm\\:ss} before revealing.", 5f);
    }
    
    public void RegisterEvents()
    {
        // Register events here
        LabApi.Events.Handlers.Scp3114Events.Revealing += OnRevealing;
        
    }
    
    public void UnregisterEvents()
    {
        // Unregister events here
        LabApi.Events.Handlers.Scp3114Events.Revealing -= OnRevealing;
        
    }
}