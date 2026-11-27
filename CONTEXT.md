# Session context: animations, new pieces, 15 levels

Handoff notes for picking this work up in a new chat. Read CLAUDE.md first; this file only covers what's in flight.
Last updated 2026-11-27. Everything below up to "Not done" is in commit `ea68356` ("Create more enemies and levels").

## The request being worked on

> Add animations to the actual levels. Then add more complicated levels. Make it so you have to move multiple
> boxes instead of just moving one to the exit, that kind of gameplay is boring. Make 15 levels that get
> progressively harder. Add 4 more potions and 4 more enemies.

An earlier request, "Add narrator reactions", was interrupted by this one and is still open (offer it as an extra).

## Done

### Rules (`Assets/Scripts/Dungeon.cs`)
- `enum Potion { Haste, Sleep, Recall, Rage, Stone }`, `enum Foe { Dwarf, Brute, Wizard, Thief, Hunter }`.
- Potions (hero drinks by walking on it; a crate slid onto it uses it up):
  - Haste: hero gets an extra step. Crate on it: villain gets a free move (Resolve returns 0 right away).
  - Sleep: hero skips 2 turns. Recall: hero teleports back to its start. Rage: hero can smash through one crate.
  - Stone: a crate slid onto it turns to stone (can't be moved, smashed, pushed or blasted; blocks the wizard's view).
- Enemies ("companions" of the hero; crates can't be moved onto them):
  - Dwarf: patrols, turns at walls/stone, smashes a crate in its way (stays put that turn).
  - Brute: patrols, pushes a crate ahead; turns around if the crate can't move.
  - Wizard: stands still, blasts the first wooden crate in each straight line.
  - Thief: walks to the nearest potion and takes it.
  - Hunter: walks next to the nearest wooden crate, then smashes it.
- Wards (`+` in ASCII): floor where crates can't be placed. Meant for cells near the exit so the
  "one crate in the doorway" solution is gone.
- **New hero rule** (to stop stall exploits): `HeroRoute()` takes the shortest path over cells the hero hasn't
  walked yet (`walked` set); failing that, one that doesn't start by turning back (`heroCameFrom`); failing that,
  any shortest path. Recall resets `walked`. Without it, solver "solutions" were 30 to 40 turns of nudging a crate
  back and forth so the hero paced or circled. How to Play (`howto.2.body`, the user's own line) does not mention
  this yet: suggest a sentence, don't silently reword it.
- Turn order in `Resolve`: crate-on-potion effect, then `FoesStep()`, then `HeroStep()`.
- Event log: set `d.log` to a list and each rule appends `(Ev, a, b, info)` so the game can animate it.
  CrateMoved info 0 = villain, 1 = brute push. FoeMoved info = foe index.
- ASCII: `#` wall, `.` floor, `+` ward, `C` crate, `H` hero, `E` exit, potions `PZRGS`, foes `DBWTA`.

### Game (`Assets/Scripts/DungeonGame.cs`)
- Tilemap holds only the static board (walls, floor, wards, exit). Crates, potions, foes and the hero are sprites
  (root transform + child "Art" renderer). Sorting: map 0, overlay 1, potions 2, crates 3, foes 4, hero 5, fx 6.
- `PlayTurn` coroutine replays the event log: hop slides, crate break with splinters, wizard beam, potion pop,
  recall shrink/teleport/grow, stone tint, trapped-hero shake. `SyncViews()` snaps sprites to the true state after.
  Input is locked while `animating`; the win/lose panel shows after the animation.
- Idle motion: breathing characters, bobbing potions, the grabbed crate floats, sleeping hero tilts, raging hero
  pulses red.
- Inspector fields: `potionTiles[5]`, `foeTiles[5]` (enum order), `wardTile`, `crateTile` (now `Tile`), `stepTime`.

### Tools and tests
- `Tools/BuildLevels.cs` makes the new tiles (Kenney Tiny Dungeon index = row*12+col): Ward 42, potions
  Haste 116, Sleep 114, Recall 113 tinted violet, Rage 115, Stone 113; foes Dwarf 87, Brute 86, Wizard 84,
  Thief 111, Hunter 88. Palette updated. **Its `Levels` list still holds the old Level 2 to 6 layouts.**
- `DungeonSolver.Solve(..., singleCrate: true)` only lets the villain move one crate: use it to prove a level
  needs 2+ crates. The position key includes hero status, crates/stones, potions, foes and `walked`.
- `DungeonSelfTest` covers every new rule, including the walked-ground rule. It passed before the walked-ground
  change; **after that change it has not been recompiled or run in Unity.**

## Not done (in order)

1. Recompile in Unity, check console is clean, run `DungeonSelfTest.Run()`.
2. Generate 15 levels (Level 2 to Level 16). Planned introduction order:
   L2 two crates, L3 three crates, L4 haste, L5 sleep, L6 dwarf, L7 recall, L8 brute, L9 stone (with a dwarf or
   brute so it matters), L10 wizard, L11 rage, L12 thief (with a potion it can steal), L13 hunter, L14 to L16 mixes.
   Every level must: be winnable, fail the single-crate solve (complete search), and each potion/foe must change
   the best solution when removed. Difficulty: longer minimum solution, more crates used, bigger maps, few waits.
   Watch for solutions that still look like stalling (one crate moved back and forth); reject those.
3. Put the layouts in `Tools/BuildLevels.cs`, set `Progress.Levels` (GameSettings.cs) to 16 scenes, run the builder.
   Old Level 2 to 6 best scores in PlayerPrefs belong to different layouts now: clear them or tell the user.
4. Text in `Assets/Resources/Text.txt`: `level1..level16` titles and intros (human voice, no "oo" in titles because
   of the Cinzel Decorative ligature, no em dashes), How to Play entries for all 10 pieces and wards (the menu's
   `.pieces` row only has potion and dwarf now; probably a scrolling list with icons).
5. Level select layout for 16 tiles inside the 1000x660 menu frame.
6. Verify each built scene with the solver, play-test with game view captures, update CLAUDE.md
   (levels list, pieces, hero rule).

## Level generator (scratch, may be wiped)

A standalone .NET copy of the rules and solver, much faster than running inside Unity and can run 7 levels at once:
`/private/tmp/claude-502/-Users-neilpatil-Coding-Unity-fall-26-gamejam/1cad1dd6-5a2b-4265-a0fc-914c7d7a648b/scratchpad/gen/`

- `Stubs.cs` fakes `Vector2Int`, `Vector3Int`, `Mathf`. `Dungeon.cs` and `DungeonSolver.cs` are copies from the
  project (the solver with `using UnityEditor` and everything from `Report` down removed). Recopy after rule changes.
- `Gen2.cs`: carves a looping maze, puts the exit on the bottom edge, wards cells within N steps of it, places
  pieces at random, and keeps the best-scoring candidates that pass the checks in step 2 above.
- Build: `dotnet build -c Release -o out` (targets net9.0).
- Run: `dotnet out/gen.dll <seed> <tries> <w> <h> <loops> <crates> <potions|-> <foes|-> <wardRadius> <minHero> <minWin> <maxPositions>`
  e.g. `dotnet out/gen.dll 6 3000 13 9 0.25 3 - D 2 10 8 60000`. Each search stops after 240 s.
- Check a fixed layout: `dotnet out/gen.dll check level.txt` (prints min turns, single-crate result, crates used,
  and the result with each special piece removed).

If the folder is gone, rebuild it from the description above.

## House rules to keep

Never commit or push (the user does it). No em dashes anywhere. Player text goes in Text.txt and sounds human;
flag the user's own wording issues instead of rewording them. End replies with "Want any of these?" for extras.
