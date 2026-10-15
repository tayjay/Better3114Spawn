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
    private Player _currentlyMovingScp, _currentlyReplacingHuman;
    private RoleTypeId _currentNewRole;
    
    
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
        _currentlyMovingScp = null;
        _currentlyReplacingHuman = null;
        if(ev.ChangeReason != RoleChangeReason.RoundStart) return;
        if(_scp3114ToMove.Count > 0 && ev.NewRole.IsHuman())
        {
            if(!ev.NewRole.TryGetRandomSpawnPoint(out Vector3 _, out float _)) return;
            Player scp3114Player = _scp3114ToMove.FirstOrDefault();
            if(scp3114Player == null) return;
            _currentlyMovingScp = scp3114Player;
            _currentlyReplacingHuman = ev.Player;
            _currentNewRole = ev.NewRole;
            
            ev.NewRole = RoleTypeId.ClassD;
        }
        
    }

    
    public void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev.OldRole == RoleTypeId.Scp3114 && _scp3114ToMove.Contains(ev.Player))
        {
            _scp3114ToMove.Remove(ev.Player);
        }
        if(ev.NewRole.RoleTypeId == RoleTypeId.Scp3114)
        {
            _scp3114ToMove.Add(ev.Player);
        }

        if (_currentlyMovingScp != null && _currentlyReplacingHuman != null && ev.Player == _currentlyReplacingHuman)
        {
            if (_currentlyMovingScp.Role != RoleTypeId.Scp3114 || _currentlyReplacingHuman.Role != RoleTypeId.ClassD)
            {
                _currentlyMovingScp = null;
                _currentlyReplacingHuman = null;
                return;
            }
            if(!_currentNewRole.TryGetRandomSpawnPoint(out Vector3 spawnPoint, out float spawnRotation)) return;
            _currentlyMovingScp.Position = spawnPoint;
            _currentlyMovingScp.LookRotation = new Vector2(0f, spawnRotation);
            
            
            Scp3114Role role = _currentlyMovingScp.RoleBase as Scp3114Role;
            if(role == null) return;
            Ragdoll ragdoll = Ragdoll.SpawnRagdoll(_currentNewRole, Vector3.zero, Quaternion.identity, (DamageHandlerBase) new Scp3114DamageHandler(), _currentlyMovingScp.Nickname);
            if (ragdoll == null) return;
            ragdoll.Freeze();
            role.CurIdentity.Ragdoll = ragdoll.Base;
            role.Disguised = true;
            role._identity.RemainingDuration.Trigger(Math.Max(2.5f, Core.Instance.Config.Scp3114PreventRevealTime) * 60f);
            role._identity.ServerResendIdentity();
            _currentlyMovingScp.SendHint($"Blend in for {TimeSpan.FromMinutes(Core.Instance.Config.Scp3114PreventRevealTime):mm\\:ss} before you can attack.", 5f);
            
            
            if (ev.SpawnFlags.HasFlag(RoleSpawnFlags.AssignInventory))
            {
                if (!_currentNewRole.TryGetRoleTemplate(out PlayerRoleBase roleTemplate)) return;
                InventoryItemProvider.ServerGrantLoadout(_currentlyMovingScp.ReferenceHub, roleTemplate);
            }
            
            _scp3114ToMove.Remove(_currentlyMovingScp);
            _currentlyMovingScp = null;
            _currentlyReplacingHuman = null;
        }
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
        
        
        Timing.CallDelayed(2f, () =>
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
        if(Core.Instance.Config.RemoveScp3114Spawning && _originalPool != null)
        {
            ScpSpawner._cachedSpawnableScps = _originalPool;
            ScpSpawner._chancesArray = new float[_originalPool.Length];
            ScpSpawner._cacheSet = true;
            ScpSpawner.BackupScps.Clear();
        }
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
    }
}