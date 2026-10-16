using System;
using System.Collections.Generic;
using System.Linq;
using GameCore;
using InventorySystem;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features.Extensions;
using LabApi.Features.Wrappers;
using MapGeneration;
using Mirror;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.Ragdolls;
using PlayerRoles.RoleAssign;
using PlayerStatsSystem;
using UnityEngine;

namespace Better3114Spawn;

public class SpawnHandler
{
    private List<Player> _scp3114ToMove = new List<Player>();
    
    
    public void OnRoundStarted()
    {
        _scp3114ToMove.Clear();
        
        if(Core.Instance.Config.RemoveScp3114Spawning)
        {
            // Remove Scp3114 from the spawn pool

            var originalPool = ScpSpawner.SpawnableScps.ToArray();

            var allowed = originalPool
                .Where(role => role.RoleTypeId != RoleTypeId.Scp3114)
                .ToArray();

            ScpSpawner._cachedSpawnableScps = allowed;
            ScpSpawner._chancesArray = new float[allowed.Length];
            ScpSpawner._cacheSet = true;
            ScpSpawner.BackupScps.Clear();
        }
    }
    
    
    public void OnChangingRole(PlayerChangingRoleEventArgs ev)
    {
        if(ev.ChangeReason != RoleChangeReason.RoundStart) return;
        if(_scp3114ToMove.Count > 0 && ev.NewRole.IsHuman())
        {
            Player scp3114Player = _scp3114ToMove.FirstOrDefault();
            if(scp3114Player == null) return;
            _scp3114ToMove.Remove(scp3114Player);
            ev.NewRole.TryGetRandomSpawnPoint(out Vector3 spawnPoint, out float spawnRotation);
            scp3114Player.Position = spawnPoint;
            scp3114Player.LookRotation = new Vector2(0f, spawnRotation);
            if (ev.SpawnFlags.HasFlag(RoleSpawnFlags.AssignInventory))
            {
                if (!ev.NewRole.TryGetRoleTemplate(out PlayerRoleBase roleTemplate)) return;
                InventoryItemProvider.ServerGrantLoadout(scp3114Player.ReferenceHub, roleTemplate);
            }
            
            
            Scp3114Role role = scp3114Player.RoleBase as Scp3114Role;
            if(role == null) return;
            Ragdoll ragdoll = Ragdoll.SpawnRagdoll(ev.NewRole, Vector3.zero, Quaternion.identity, (DamageHandlerBase) new Scp3114DamageHandler(), scp3114Player.Nickname);
            if (ragdoll == null) return;
            ragdoll.Freeze();
            role.CurIdentity.Ragdoll = ragdoll.Base;
            role.Disguised = true;

            role._identity.RemainingDuration.Trigger(Math.Max(150, Core.Instance.Config.Scp3114PreventRevealTime));
            scp3114Player.SendHint($"Blend in for {TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime):mm\\:ss} before you can attack.", 5f);

            ev.NewRole = RoleTypeId.ClassD;
        }
        
    }

    
    public void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev.ChangeReason != RoleChangeReason.RoundStart) return;
        if (ev.OldRole == RoleTypeId.Scp3114 && _scp3114ToMove.Contains(ev.Player))
        {
            _scp3114ToMove.Remove(ev.Player);
        }
        if(ev.NewRole.RoleTypeId == RoleTypeId.Scp3114)
        {
            _scp3114ToMove.Add(ev.Player);
        }
    }
    
    
    private void OnRagdollSpawned(BasicRagdoll obj)
    {
        if(obj.NetworkInfo.Handler is Scp3114DamageHandler)
        {
            if ((obj.NetworkInfo.Handler as Scp3114DamageHandler).StartingRagdoll)
            {
                NetworkServer.Destroy(obj.gameObject);
            }
        }
    }

    public void RegisterEvents()
    {
        // Register events here
        LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole += OnChangingRole;
        LabApi.Events.Handlers.PlayerEvents.ChangedRole += OnChangedRole;
        RagdollManager.OnRagdollSpawned += OnRagdollSpawned;
    }

    
    public void UnregisterEvents()
    {
        // Unregister events here
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole -= OnChangingRole;
        LabApi.Events.Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        RagdollManager.OnRagdollSpawned -= OnRagdollSpawned;
    }
}