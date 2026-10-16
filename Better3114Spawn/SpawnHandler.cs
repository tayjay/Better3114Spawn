using System;
using System.Collections.Generic;
using System.Linq;
using InventorySystem;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Extensions;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.Ragdolls;
using PlayerRoles.RoleAssign;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace Better3114Spawn;

public class SpawnHandler
{
    private sealed class Replacement
    {
        public Player Human;
        public uint HumanNetId;
        public PlayerRoleBase HumanBeforeRole;
        public int HumanBeforeLife;
        public RoleTypeId HumanBeforeRoleType;
        public PlayerRoleBase HumanRole;
        public int HumanLife;
        public RoleSpawnFlags HumanSpawnFlags;
        public Player Scp;
        public Scp3114Role ScpRole;
        public int ScpLife;
        public Scp3114Data Data;
        public PlayerChangingRoleEventArgs Proposal;
        public RoleTypeId OriginalRole;
        public PlayerRoleBase RoleTemplate;
        public Vector3 Position;
        public float Rotation;
        public double PreventRevealSeconds;
        public int Generation;
        public bool Preparing = true;
        public bool Cancelled;
        public Ragdoll Body;
    }

    private readonly List<Player> _scp3114ToMove = new List<Player>();
    private readonly Dictionary<Player, Replacement> _replacements = new Dictionary<Player, Replacement>();
    private readonly HashSet<Player> _ambiguousHumans = new HashSet<Player>();
    private PlayerRoleBase[] _originalPool;
    private PlayerRoleBase[] _filteredPool;
    private CoroutineHandle _delayedRagdollCleanupHandle;
    private int _generation;
    private bool _enabled;
    private bool _initialSpawning;

    public void OnRoundStarted()
    {
        if (!_enabled) return;
        ResetRoundState();
        _initialSpawning = true;
        if (!Core.Instance.Config.RemoveScp3114Spawning) return;

        _originalPool = ScpSpawner.SpawnableScps;
        _filteredPool = _originalPool.Where(role => role.RoleTypeId != RoleTypeId.Scp3114).ToArray();
        ScpSpawner._cachedSpawnableScps = _filteredPool;
        ScpSpawner._chancesArray = new float[_filteredPool.Length];
        ScpSpawner._cacheSet = true;
        ScpSpawner.BackupScps.Clear();
    }

    public void OnChangingRole(PlayerChangingRoleEventArgs ev)
    {
        if (!_enabled) return;

        // ChangedRole has no transaction ID. Do not pair recursive changes for the
        // same human with either proposal, including deeper nested callbacks.
        if (_replacements.TryGetValue(ev.Player, out Replacement previous))
        {
            _ambiguousHumans.Add(ev.Player);
            _replacements.Remove(ev.Player);
            ReleaseReservation(previous);
            return;
        }

        PruneReservations();
        if (!_initialSpawning || !ev.IsAllowed || _ambiguousHumans.Contains(ev.Player) ||
            ev.ChangeReason != RoleChangeReason.RoundStart || !ev.NewRole.IsHuman()) return;

        _scp3114ToMove.RemoveAll(player => !IsScp3114(player));
        Player scp = _scp3114ToMove.FirstOrDefault(player =>
            !_replacements.Values.Any(replacement => !replacement.Cancelled && replacement.Scp == player));
        if (scp == null || ev.Player.ReferenceHub == null) return;

        if (!ev.NewRole.TryGetRoleTemplate(out PlayerRoleBase template) ||
            !ev.NewRole.TryGetRandomSpawnPoint(out Vector3 position, out float rotation)) return;

        var replacement = new Replacement
        {
            Human = ev.Player,
            HumanNetId = ev.Player.NetworkId,
            HumanBeforeRole = ev.Player.RoleBase,
            HumanBeforeLife = ev.Player.RoleBase.UniqueLifeIdentifier,
            HumanBeforeRoleType = ev.Player.Role,
            Scp = scp,
            ScpRole = scp.RoleBase as Scp3114Role,
            ScpLife = scp.RoleBase.UniqueLifeIdentifier,
            Data = scp.GetDataStore<Scp3114Data>(),
            Proposal = ev,
            OriginalRole = ev.NewRole,
            RoleTemplate = template,
            Position = position,
            Rotation = rotation,
            PreventRevealSeconds = Core.Instance.Config.Scp3114PreventRevealTime,
            Generation = _generation,
        };

        _replacements.Add(ev.Player, replacement);
        replacement.Data.InitialDisguiseRole = replacement.OriginalRole;
        replacement.Data.ReplacingNetId = replacement.HumanNetId;
        replacement.Data.PendingReplacement = true;
        try
        {
            // Body creation invokes native callbacks. Reserve first, then verify
            // the proposal and both lives before changing the human's role.
            replacement.Body = Ragdoll.SpawnRagdoll(replacement.OriginalRole, Vector3.zero,
                Quaternion.identity, new Scp3114DamageHandler(), scp.Nickname);
            if (!IsRegistered(replacement) || !IsValidProposal(replacement) ||
                !HasOriginalHumanLife(replacement) || !HasPreparedBody(replacement))
            {
                ReleaseReservation(replacement);
                return;
            }

            replacement.Preparing = false;
            ev.NewRole = RoleTypeId.ClassD;
        }
        catch (Exception exception)
        {
            ReleaseReservation(replacement);
            Logger.Error($"Could not prepare a starting disguise: {exception}");
        }
    }

    public void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (!_enabled) return;

        if (IsScp3114(ev.Player))
        {
            var data = ev.Player.GetDataStore<Scp3114Data>();
            int life = ev.Player.RoleBase.UniqueLifeIdentifier;
            if (data.RoleLifeId != life)
            {
                _scp3114ToMove.Remove(ev.Player);
                _scp3114ToMove.Add(ev.Player);
                data.Reset();
                data.RoleLifeId = life;
            }
        }
        else _scp3114ToMove.Remove(ev.Player);

        PruneReservations();
        if (!_replacements.TryGetValue(ev.Player, out Replacement replacement)) return;
        if (replacement.Preparing ||
            ev.ChangeReason != RoleChangeReason.RoundStart || ev.OldRole != replacement.HumanBeforeRoleType ||
            ev.NewRole.RoleTypeId != RoleTypeId.ClassD || ev.Player.Role != RoleTypeId.ClassD ||
            !ReferenceEquals(ev.NewRole, ev.Player.RoleBase) ||
            ev.Player.RoleBase.UniqueLifeIdentifier == replacement.HumanBeforeLife)
        {
            _replacements.Remove(ev.Player);
            ReleaseReservation(replacement);
            return;
        }

        replacement.HumanRole = ev.Player.RoleBase;
        replacement.HumanLife = replacement.HumanRole.UniqueLifeIdentifier;
        replacement.HumanSpawnFlags = ev.SpawnFlags;

        // Consume before any identity, inventory, or ragdoll callbacks can reenter.
        _replacements.Remove(ev.Player);
        if (!replacement.Cancelled) _scp3114ToMove.Remove(replacement.Scp);
        ClearPendingMetadata(replacement);
        bool applied = false;
        try
        {
            if (replacement.Cancelled || !IsValidProposal(replacement) ||
                !IsCurrentAssignment(replacement) || !HasPreparedBody(replacement)) return;
            replacement.Body.Freeze();
            if (!IsCurrentAssignment(replacement) || !HasPreparedBody(replacement)) return;
            replacement.Scp.Position = replacement.Position;
            if (!IsCurrentAssignment(replacement)) return;
            replacement.Scp.LookRotation = new Vector2(0f, replacement.Rotation);
            if (!IsCurrentAssignment(replacement) || !HasPreparedBody(replacement)) return;
            replacement.ScpRole.CurIdentity.Ragdoll = replacement.Body.Base;
            replacement.ScpRole.Disguised = true;
            if (!HasPreparedDisguise(replacement)) return;

            replacement.ScpRole._identity.RemainingDuration.Trigger(Math.Max(150, replacement.PreventRevealSeconds));
            replacement.ScpRole._identity.ServerResendIdentity();
            if (!HasPreparedDisguise(replacement)) return;
            applied = true;
            replacement.Scp.SendHint(Core.Instance.Config.Translate("scp3114_blend_in_hint",$"{TimeSpan.FromSeconds(replacement.PreventRevealSeconds):mm\\:ss}"), 5f);
            if (HasPreparedDisguise(replacement) && ev.SpawnFlags.HasFlag(RoleSpawnFlags.AssignInventory))
                InventoryItemProvider.ServerGrantLoadout(replacement.Scp.ReferenceHub, replacement.RoleTemplate);
            // Native loadout callbacks can change roles. Do not write to either
            // player after calling ServerGrantLoadout.
        }
        catch (Exception exception)
        {
            Logger.Error($"Could not finish a starting disguise: {exception}");
        }
        finally
        {
            if (!applied)
            {
                TryRestoreHuman(replacement);
                if (!replacement.Cancelled && _enabled && replacement.Generation == _generation && IsCurrentScp(replacement) &&
                    !_scp3114ToMove.Contains(replacement.Scp)) _scp3114ToMove.Add(replacement.Scp);
                ClearFailedDisguise(replacement);
                DestroyPreparedBody(replacement);
            }
        }
    }

    public void OnPlayersSpawned()
    {
        if (!_enabled || !_initialSpawning) return;
        _initialSpawning = false;
        ClearReservations();
        RestoreSpawnPool();
        CancelCleanup();

        int generation = _generation;
        _delayedRagdollCleanupHandle = Timing.CallDelayed(2f, () =>
        {
            if (!_enabled || generation != _generation) return;
            _delayedRagdollCleanupHandle = default;
            if (!NetworkServer.active) return;
            _scp3114ToMove.RemoveAll(player => !IsScp3114(player));
            if (_scp3114ToMove.Count > 0) return;

            foreach (BasicRagdoll body in RagdollManager.AllRagdolls.ToArray())
            {
                if (body != null && body.NetworkInfo.Handler is Scp3114DamageHandler handler && handler.StartingRagdoll)
                    NetworkServer.Destroy(body.gameObject);
            }
        });
    }

    public void OnRoundRestarted() => ResetRoundState();

    private static bool IsScp3114(Player player) =>
        player != null && player.ReferenceHub != null && player.Role == RoleTypeId.Scp3114;

    private static bool IsCurrentScp(Replacement replacement) =>
        IsScp3114(replacement.Scp) && ReferenceEquals(replacement.Scp.RoleBase, replacement.ScpRole) &&
        replacement.ScpRole.UniqueLifeIdentifier == replacement.ScpLife;

    private static bool HasOriginalHumanLife(Replacement replacement) =>
        replacement.Human.ReferenceHub != null && ReferenceEquals(replacement.Human.RoleBase, replacement.HumanBeforeRole) &&
        replacement.Human.RoleBase.UniqueLifeIdentifier == replacement.HumanBeforeLife;

    private static bool HasPreparedBody(Replacement replacement) =>
        replacement.Body != null && replacement.Body.Base != null && replacement.Body.Role == replacement.OriginalRole;

    private bool IsRegistered(Replacement replacement) =>
        _replacements.TryGetValue(replacement.Human, out Replacement current) && ReferenceEquals(current, replacement);

    private bool IsValidProposal(Replacement replacement) =>
        !replacement.Cancelled && _enabled && _initialSpawning && replacement.Generation == _generation && IsCurrentScp(replacement) &&
        replacement.Human.ReferenceHub != null && replacement.Proposal.IsAllowed &&
        replacement.Proposal.ChangeReason == RoleChangeReason.RoundStart &&
        replacement.Proposal.NewRole == (replacement.Preparing ? replacement.OriginalRole : RoleTypeId.ClassD);

    private bool IsCurrentAssignment(Replacement replacement) =>
        _enabled && _initialSpawning && replacement.Generation == _generation && IsCurrentScp(replacement) &&
        HasCommittedHumanLife(replacement);

    private static bool HasCommittedHumanLife(Replacement replacement) =>
        replacement.Human.ReferenceHub != null && replacement.Human.Role == RoleTypeId.ClassD &&
        ReferenceEquals(replacement.Human.RoleBase, replacement.HumanRole) &&
        replacement.Human.RoleBase.UniqueLifeIdentifier == replacement.HumanLife;

    private bool HasPreparedDisguise(Replacement replacement) =>
        IsCurrentAssignment(replacement) && HasPreparedBody(replacement) && replacement.ScpRole.Disguised &&
        ReferenceEquals(replacement.ScpRole.CurIdentity.Ragdoll, replacement.Body.Base);

    private void PruneReservations()
    {
        // A veto has no post-event; release its final proposal before choosing the next SCP.
        foreach (Replacement replacement in _replacements.Values.ToArray())
            if (!replacement.Cancelled && !IsValidProposal(replacement)) ReleaseReservation(replacement);
    }

    private static void ClearPendingMetadata(Replacement replacement)
    {
        if (!IsCurrentScp(replacement) || replacement.Data.ReplacingNetId != replacement.HumanNetId) return;
        replacement.Data.PendingReplacement = false;
        replacement.Data.ReplacingNetId = 0;
    }

    private void ReleaseReservation(Replacement replacement)
    {
        // Keep a cancelled proposal until its human post-event or the next
        // pre-event: cancellation during native initialization still needs rollback.
        replacement.Cancelled = true;
        ClearPendingMetadata(replacement);
        if (!replacement.Preparing && replacement.Proposal.NewRole == RoleTypeId.ClassD)
            replacement.Proposal.NewRole = replacement.OriginalRole;
        DestroyPreparedBody(replacement);
    }

    private void TryRestoreHuman(Replacement replacement)
    {
        if (!_enabled || !_initialSpawning || replacement.Generation != _generation ||
            replacement.OriginalRole == RoleTypeId.ClassD || !HasCommittedHumanLife(replacement)) return;
        _ambiguousHumans.Add(replacement.Human);
        try
        {
            replacement.Human.ReferenceHub.roleManager.ServerSetRole(replacement.OriginalRole,
                RoleChangeReason.RoundStart, replacement.HumanSpawnFlags);
        }
        catch (Exception exception)
        {
            Logger.Error($"Could not restore a displaced human: {exception}");
        }
    }

    private static void ClearFailedDisguise(Replacement replacement)
    {
        BasicRagdoll ownedBody = replacement.Body?.Base;
        // Unity's destroyed-object equality can report null while this managed
        // reference is still the identity we need to clear.
        if (!IsCurrentScp(replacement) || ReferenceEquals(ownedBody, null) ||
            !ReferenceEquals(replacement.ScpRole.CurIdentity.Ragdoll, ownedBody)) return;
        try
        {
            replacement.ScpRole.Disguised = false;
            if (!IsCurrentScp(replacement) || !ReferenceEquals(replacement.ScpRole.CurIdentity.Ragdoll, ownedBody)) return;
            replacement.ScpRole.CurIdentity.Ragdoll = null;
            replacement.ScpRole._identity.ServerResendIdentity();
        }
        catch (Exception exception)
        {
            Logger.Error($"Could not clear an incomplete starting disguise: {exception}");
        }
    }

    private static void DestroyPreparedBody(Replacement replacement)
    {
        Ragdoll body = replacement.Body;
        replacement.Body = null;
        if (body == null || body.Base == null) return;
        try { NetworkServer.Destroy(body.Base.gameObject); }
        catch (Exception exception) { Logger.Error($"Could not remove an unused disguise body: {exception}"); }
    }

    private void ClearReservations()
    {
        Replacement[] replacements = _replacements.Values.ToArray();
        _replacements.Clear();
        foreach (Replacement replacement in replacements) ReleaseReservation(replacement);
    }

    private void RestoreSpawnPool()
    {
        PlayerRoleBase[] original = _originalPool;
        PlayerRoleBase[] filtered = _filteredPool;
        _originalPool = null;
        _filteredPool = null;
        if (original == null || !ReferenceEquals(ScpSpawner._cachedSpawnableScps, filtered)) return;
        ScpSpawner._cachedSpawnableScps = original;
        ScpSpawner._chancesArray = new float[original.Length];
        ScpSpawner._cacheSet = true;
        ScpSpawner.BackupScps.Clear();
    }

    private void CancelCleanup()
    {
        if (_delayedRagdollCleanupHandle.IsValid) Timing.KillCoroutines(_delayedRagdollCleanupHandle);
        _delayedRagdollCleanupHandle = default;
    }

    private void ResetRoundState()
    {
        ++_generation;
        _initialSpawning = false;
        CancelCleanup();
        ClearReservations();
        _scp3114ToMove.Clear();
        _ambiguousHumans.Clear();
        RestoreSpawnPool();
    }

    public void RegisterEvents()
    {
        if (_enabled) return;
        _enabled = true;
        LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole += OnChangingRole;
        LabApi.Events.Handlers.PlayerEvents.ChangedRole += OnChangedRole;
        LabApi.Events.Handlers.ServerEvents.RoundRestarted += OnRoundRestarted;
        RoleAssigner.OnPlayersSpawned += OnPlayersSpawned;
    }

    public void UnregisterEvents()
    {
        _enabled = false;
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole -= OnChangingRole;
        LabApi.Events.Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        LabApi.Events.Handlers.ServerEvents.RoundRestarted -= OnRoundRestarted;
        RoleAssigner.OnPlayersSpawned -= OnPlayersSpawned;
        ResetRoundState();
    }
}
