using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp3114Events;
using LabApi.Events.Arguments.ScpEvents;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerStatsSystem;
using UnityEngine;

namespace Better3114Spawn;

public class RoleHandler
{
    private bool _earlyRevealing = false;

    public void OnWaitingForPlayers()
    {
        _earlyRevealing = false;
    }
    
    public void OnRevealing(Scp3114RevealingEventArgs ev)
    {
        if (ev.Forced) return;
        if(_earlyRevealing) return;
        if (Round.Duration >= TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime)) return;
        ev.IsAllowed = false;
        ev.Player.SendHint($"You must wait {(TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime) - Round.Duration):mm\\:ss} before revealing.", 5f);
    }

    public void OnHurt(PlayerHurtEventArgs ev)
    {
        if(ev.Player.Role != RoleTypeId.Scp3114) return;
        if(Round.Duration >= TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime)) return;
        if(ev.Attacker == null) return;
        if(ev.Attacker.IsHost) return;
        if(ev.Attacker == ev.Player) return;
        if(ev.Attacker.Faction == ev.Player.Faction) return;
        if((ev.DamageHandler as StandardDamageHandler)?.Damage <= 5) return;
        _earlyRevealing = true;
    }

    public void OnHumeBreak(ScpHumeShieldBrokenEventArgs ev)
    {
        if(ev.Player.Role != RoleTypeId.Scp3114) return;
        if(Round.Duration >= TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime)) return;
        Scp3114Role scp3114Role = ev.Player.RoleBase as Scp3114Role;
        if(scp3114Role == null) return;
        if(scp3114Role.CurIdentity.Status != Scp3114Identity.DisguiseStatus.Active) return;
        scp3114Role._identity.RemainingDuration.Remaining = 0; // Force the reveal
    }
    
    public void RegisterEvents()
    {
        // Register events here
        LabApi.Events.Handlers.Scp3114Events.Revealing += OnRevealing;
        LabApi.Events.Handlers.PlayerEvents.Hurt += OnHurt;
        LabApi.Events.Handlers.ScpEvents.HumeShieldBroken += OnHumeBreak;
        LabApi.Events.Handlers.ServerEvents.WaitingForPlayers += OnWaitingForPlayers;
    }
    
    public void UnregisterEvents()
    {
        // Unregister events here
        LabApi.Events.Handlers.Scp3114Events.Revealing -= OnRevealing;
        LabApi.Events.Handlers.PlayerEvents.Hurt -= OnHurt;
        LabApi.Events.Handlers.ScpEvents.HumeShieldBroken -= OnHumeBreak;
        LabApi.Events.Handlers.ServerEvents.WaitingForPlayers -= OnWaitingForPlayers;
    }
}