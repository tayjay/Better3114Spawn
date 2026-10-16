# Better SCP-3114 Spawning (Halloween 2026)

A LabAPI plugin for SCP: Secret Laboratory that starts SCP-3114 disguised among the human players.

Consider it practice for SCP-953.

**Requires LabAPI `1.1.7` and SCP:SL `14.2.8`.**

## How it works

- **Blend in from the start.** During initial role assignment, SCP-3114 takes the appearance, spawn point, and starting loadout of a Class-D, Scientist, or Facility Guard.
- **Keep the displaced player in the round.** The human player whose role was replaced spawns as a Class-D.
- **Give the disguise time to work.** Voluntary revealing is initially blocked, and SCP-3114 must reveal before using its attacks. The default reveal delay is **150 seconds (2 minutes, 30 seconds)**.
- **Follow the game's spawn conditions.** The plugin does not force SCP-3114 to spawn. It relies on the holiday 14.2.8 patch to make the role available, unless spawning is disabled in the plugin config.

## Why do I need this?

SCP-3114 has always felt awkward to play as, having to join a group from an odd location making them very suspicious. This should make the early game more fun for them and the humans.

The role is also very likely to spawn so you'll see a lot of them:
- 1 SCP: 20% chance
- 2 SCPs: 33.3% chance
- 3 SCPs: 46.7% chance
- 4 SCPs: 62.2% chance
- 5 SCPs: 80.4% chance

## Revealing early

During the opening reveal delay, an actively disguised SCP-3114 has two ways to reveal early:

- **Taking Hostile Damage.** Allows SCP-3114 to reveal voluntarily to get their revenge for RDM. (Configurable)
- **Hume Shield breaks.** If SCP-3114's Hume Shield is depleted by hostile damage, it is **forced** to reveal immediately.

## Role counting

SCP-3114 can replace any of the three starting human roles, making role counts less useful for identifying it. An extra Class-D may still suggest that SCP-3114 is present, but the count alone does not identify the disguised player.

## Configuration

| Option | Default | Description |
| --- | --- | --- |
| `remove_scp3114_spawning` | `false` | Set to `true` to remove SCP-3114 from the round-start SCP spawn pool. This does not block administrative role changes. |
| `scp3114_prevent_reveal_time` | `150` | Seconds from the start of the round before voluntary revealing is allowed, unless an early-reveal condition applies. |
| `scp3114_allow_early_reveal_on_damage` | `true` | Allow qualifying hostile damage to unlock voluntary revealing before the delay ends. |
| `translations` | `key: <CHANGE_THIS>` | Localized strings to change the language of hints your players receive. `{0}` is replaced with remaining disguise time. |

The starting disguise automatically expires after **150 seconds or the configured reveal delay, whichever is longer**. For example:

- `60` allows voluntary revealing after one minute, but the automatic disguise duration remains 150 seconds.
- `180` sets the voluntary reveal delay and automatic disguise duration to three minutes.
