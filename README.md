# Better SCP-3114 Spawning (Halloween 2026)

A LabAPI plugin for SCP: Secret Laboratory that starts SCP-3114 disguised among the human players.

Consider it practice for SCP-953.

**Requires LabAPI `1.1.7`, SCPSL `14.2.8`**

## How it works

- **Blend in from the start.** SCP-3114 takes the appearance, spawn point, and starting loadout of a Class-D, Scientist, or Facility Guard.
- **Keep the displaced player in the round.** The human player whose role was replaced spawns as a Class-D.
- **Give the disguise time to work.** Voluntary revealing is initially blocked, and SCP-3114 must reveal before using its attacks. The default reveal delay is **150 seconds (2 minutes, 30 seconds)**.
- **Resume normal play after revealing.** Once the starting disguise ends, SCP-3114 plays as usual.
- **Holiday Spawn Conditions.** The plugin does not force SCP-3114 to spawn, relying on the holiday 14.2.8 patch to enable it. 

## Revealing early

The starting disguise has two exceptions:

- Taking sufficient damage from an enemy player can allow SCP-3114 to reveal early.
- Losing its Hume Shield during the reveal delay forces the active disguise to end.

Vanilla firearm damage reduction still applies while disguised.

## Role counting

SCP-3114 can replace any of the three starting human roles, making role counts less useful for identifying it. An extra Class-D may still suggest that SCP-3114 is present, but the count alone does not identify the disguised player.

## Configuration

| Option | Default | Description |
| --- | --- | --- |
| `remove_scp3114_spawning` | `false` | Set to `true` to remove SCP-3114 from the round-start SCP spawn pool. |
| `scp3114_prevent_reveal_time` | `150` | Seconds from the start of the round before voluntary revealing is allowed, unless an early-reveal condition applies. |

The starting disguise automatically expires after **150 seconds or the configured reveal delay, whichever is longer**. For example:

- `60` allows voluntary revealing after one minute, but automatic revealing still waits 150 seconds.
- `180` delays both voluntary and automatic revealing to three minutes.

The early-reveal conditions above still apply.
