using System;
using Hints;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp3114Events;
using LabApi.Features.Wrappers;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerStatsSystem;

namespace Better3114Spawn;

public class RoleHandler
{
    public void OnRevealing(Scp3114RevealingEventArgs ev)
    {
        if (ev.Forced) return;

        Scp3114Data data = ev.Player.GetDataStore<Scp3114Data>();
        if (data.CanRevealEarly && data.EarlyRevealLifeId == ev.Player.LifeId) return;
        if (Round.Duration >= TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime)) return;

        ev.IsAllowed = false;
        
        double remaining = Math.Max(0,
            Core.Instance.Config.Scp3114PreventRevealTime
            - Round.Duration.TotalSeconds);

        if (remaining > 0)
        {
            ev.Player.SendHint(
                Core.Instance.Config.Translate("scp3114_wait_hint", "{0}"),
                new HintParameter[]
                {
                    TimespanHintParameter.FromOffset(
                        remaining, @"mm\:ss", negate: false)
                },
                duration: 5f);
        }
    }

    public void OnHurt(PlayerHurtEventArgs ev)
    {
        if (ev.Player.IsDestroyed || ev.Player.RoleBase is not Scp3114Role role) return;
        if (role.CurIdentity.Status != Scp3114Identity.DisguiseStatus.Active) return;
        if (Round.Duration >= TimeSpan.FromSeconds(Core.Instance.Config.Scp3114PreventRevealTime)) return;
        if (ev.Attacker == null || ev.Attacker.IsDestroyed || ev.Attacker.IsHost || !ev.Attacker.IsPlayer) return;
        if (ev.Attacker == ev.Player || ev.Attacker.Faction == ev.Player.Faction) return;

        // Hurt also fires when ApplyDamage does nothing. Require damage that was actually applied.
        if (ev.DamageHandler is not StandardDamageHandler damage || !(damage.Damage > 0f) || !(damage.TotalDamageDealt > 0f)) return;
        Scp3114Data data = ev.Player.GetDataStore<Scp3114Data>();

        if (damage.AbsorbedHumeDamage > 0f && ev.Player.HumeShield <= 0f)
        {
            // Let the native cooldown raise the cancellable forced-reveal event on its next update.
            role._identity.RemainingDuration.Remaining = 0;
            data.CanRevealEarly = true;
            data.EarlyRevealLifeId = role.UniqueLifeIdentifier;
            ev.Player.SendHint(Core.Instance.Config.Translate("scp3114_force_reveal_hint"), 5f);
            return;
        }

        if (Core.Instance.Config.Scp3114AllowEarlyRevealOnDamage && (damage.TotalDamageDealt > 5f) && !data.CanRevealEarly)
        {
            data.CanRevealEarly = true;
            data.EarlyRevealLifeId = role.UniqueLifeIdentifier;
            ev.Player.SendHint(Core.Instance.Config.Translate("scp3114_allow_reveal_hint"), 5f);
        }
    }

    public void RegisterEvents()
    {
        LabApi.Events.Handlers.Scp3114Events.Revealing += OnRevealing;
        LabApi.Events.Handlers.PlayerEvents.Hurt += OnHurt;
    }

    public void UnregisterEvents()
    {
        LabApi.Events.Handlers.Scp3114Events.Revealing -= OnRevealing;
        LabApi.Events.Handlers.PlayerEvents.Hurt -= OnHurt;
    }
}
