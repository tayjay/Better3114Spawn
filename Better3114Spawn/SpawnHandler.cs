using System;
using System.Collections.Generic;
using System.Linq;
using GameCore;
using InventorySystem;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features.Extensions;
using LabApi.Features.Stores;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
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
    private PlayerRoleBase[] _originalPool;
    private CoroutineHandle _delayedRagdollCleanupHandle;
    
    
    public void OnRoundStarted()
    {
        _scp3114ToMove.Clear();
        
        if(Core.Instance.Config.RemoveScp3114Spawning)
        {
            // Remove Scp3114 from the spawn pool

            _originalPool = ScpSpawner.SpawnableScps.ToArray();

            var allowed = _originalPool
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
            if(!ev.NewRole.TryGetRandomSpawnPoint(out Vector3 _, out float _)) return;
            Player scp3114Player = _scp3114ToMove.FirstOrDefault();
            if(scp3114Player == null) return;
            var data = scp3114Player.GetDataStore<Scp3114Data>();
            if(data.PendingReplacement) return;
            data.ReplacingNetId = ev.Player.NetworkId;
            data.InitialDisguiseRole = ev.NewRole;
            data.PendingReplacement = true;
            
            ev.NewRole = RoleTypeId.ClassD;
        }
        
    }

    
    public void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev.OldRole == RoleTypeId.Scp3114 && _scp3114ToMove.Contains(ev.Player))
        {
            ev.Player.GetDataStore<Scp3114Data>().Reset();
            _scp3114ToMove.Remove(ev.Player);
        }
        if(ev.NewRole.RoleTypeId == RoleTypeId.Scp3114)
        {
            _scp3114ToMove.Add(ev.Player);
            ev.Player.GetDataStore<Scp3114Data>().Reset();
        }

        var nextMovingScp = _scp3114ToMove.FirstOrDefault(scp => scp.GetDataStore<Scp3114Data>().ReplacingNetId == ev.Player.NetworkId);
        if (nextMovingScp == null) return;
        var scp3114Data = nextMovingScp.GetDataStore<Scp3114Data>();
        if (nextMovingScp.Role != RoleTypeId.Scp3114 || ev.Player.Role != RoleTypeId.ClassD)
        {
            return;
        }
        if(!scp3114Data.InitialDisguiseRole.TryGetRandomSpawnPoint(out Vector3 spawnPoint, out float spawnRotation)) return;
        nextMovingScp.Position = spawnPoint;
        nextMovingScp.LookRotation = new Vector2(0f, spawnRotation);
            
            
        Scp3114Role role = nextMovingScp.RoleBase as Scp3114Role;
        if(role == null) return;
        Ragdoll ragdoll = Ragdoll.SpawnRagdoll(scp3114Data.InitialDisguiseRole, Vector3.zero, Quaternion.identity, (DamageHandlerBase) new Scp3114DamageHandler(), nextMovingScp.Nickname);
        if (ragdoll == null) return;
        ragdoll.Freeze();
        role.CurIdentity.Ragdoll = ragdoll.Base;
        role.Disguised = true;
        role._identity.RemainingDuration.Trigger(Math.Max(150f, Core.Instance.Config.Scp3114PreventRevealTime));
        role._identity.ServerResendIdentity();
        nextMovingScp.SendHint($"Blend in for {TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime):mm\\:ss} before you can attack.", 5f);
            
            
        if (ev.SpawnFlags.HasFlag(RoleSpawnFlags.AssignInventory))
        {
            if (!scp3114Data.InitialDisguiseRole.TryGetRoleTemplate(out PlayerRoleBase roleTemplate)) return;
            InventoryItemProvider.ServerGrantLoadout(nextMovingScp.ReferenceHub, roleTemplate);
        }
        scp3114Data.PendingReplacement = false;
        _scp3114ToMove.Remove(nextMovingScp);
    }

    public void OnPlayersSpawned()
    {
        if(Core.Instance.Config.RemoveScp3114Spawning)
        {
            ScpSpawner._cachedSpawnableScps = _originalPool;
            ScpSpawner._chancesArray = new float[_originalPool.Length];
            ScpSpawner._cacheSet = true;
            ScpSpawner.BackupScps.Clear();
        }
        
        
        _delayedRagdollCleanupHandle = Timing.CallDelayed(2f, () =>
        {
            if (_scp3114ToMove.Count > 0) return;
            foreach (var obj in RagdollManager.AllRagdolls)
            {
                if (obj.NetworkInfo.Handler is Scp3114DamageHandler)
                {
                    if ((obj.NetworkInfo.Handler as Scp3114DamageHandler).StartingRagdoll)
                    {
                        NetworkServer.Destroy(obj.gameObject);
                    }
                }
            }
        });
    }

    public void OnRoundRestarted()
    {
        
    }

    public void RegisterEvents()
    {
        // Register events here
        LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole += OnChangingRole;
        LabApi.Events.Handlers.PlayerEvents.ChangedRole += OnChangedRole;
        LabApi.Events.Handlers.ServerEvents.RoundRestarted += OnRoundRestarted;
        RoleAssigner.OnPlayersSpawned += OnPlayersSpawned;
    }

    
    public void UnregisterEvents()
    {
        // Unregister events here
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole -= OnChangingRole;
        LabApi.Events.Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        LabApi.Events.Handlers.ServerEvents.RoundRestarted -= OnRoundRestarted;
        RoleAssigner.OnPlayersSpawned -= OnPlayersSpawned;
        
        Timing.KillCoroutines(_delayedRagdollCleanupHandle);
    }
}