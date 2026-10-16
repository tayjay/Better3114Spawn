# Better SCP-3114 Spawning (Halloween 2026)
SCP-3114 (Skeleton) now spawns disguised as a human player, similar to how SCP-953 will work.

## Features
- SCP-3114 spawns in place of a human player at the start of the round. Either Class D, Scientist, or Facility Guard.
- They are automatically disguised as that role, no more awkwardly sneaking in from their old spawn.
- They are locked in their disguise and cannot attack at the start of the round.
- After their disguise expires, they will play as normal.
- Reduced meta-gaming:
  - SCP-3114 can _replace_ any starting human role, meaning counting roles for the lobby size won't expose the player.
  - The replaced role will spawn as a Class D. An extra Class D can tell meta-gamers that SCP-3114 is in play, but it could be anyone.
  - RDM is not advised as SCP-3114 is allowed to reveal if they take enough hostile damage. Disguise also provides damage reduction (Vanilla feature).
### Configs
  - If your idea of a better SCP-3114 spawn is never, you can remove the holiday role from the spawn queue.
  - Shorten or extend the starting disguise duration in the config file.