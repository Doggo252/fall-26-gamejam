# Session context: animations, new pieces, 15 levels

Handoff notes for picking this work up in a new chat. Read CLAUDE.md first; this file only covers what's in flight.
Last updated 2026-11-27 (second session). The rules and animations are in commit `ea68356`; the 15 levels, text and menu changes below are not committed yet.

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

## Done in the second session

- Self-test passes (one test needed `walked` reset after teleporting the hero).
- Levels 2 to 16 are in `Tools/BuildLevels.cs` and built; `Progress.Levels` has 16 scenes. Order: 2 two crates,
  3 three crates, 4 haste, 5 sleep, 6 dwarf, 7 recall, 8 brute, 9 stone + dwarf, 10 wizard, 11 rage, 12 thief + haste,
  13 hunter, 14 sleep + dwarf, 15 recall + brute, 16 hunter + sleep. Fewest turns: 10, 9, 10, 12, 10, 16, 10, 11, 11,
  18, 12, 14, 10, 13, 20. Every one is winnable, can't be won with one crate (complete search), every potion/companion
  changes the solution when removed and acts in it, and no crate goes back to a cell it was on.
- Text.txt: titles and intros for all 16 levels, How to Play entries for all 10 pieces and wards. Keep player text
  short and plain (the user flagged an earlier draft as sounding like AI).
- Menu: level select is 8x2 tiles; How to Play pieces are a two-column scrolling list with icons.
- Play-tested Level 9 and Level 16 in play mode by replaying the solver's moves: both end in a win, console clean.
- The level generator moved into the repo as `Tools/LevelGen` (see below).
- How to Play frame is taller (`.frame--tall`, 900 of the 1920x1080 reference), so about 2.5 rows of pieces show.
- New-piece cards: first time each potion or companion appears, the hint card explains it (icon, How to Play text,
  its cells marked) until "Got it". Saved as `seen.<key>` in PlayerPrefs, cleared by Reset progress. Self-test checks
  every piece has How to Play text. Checked on Level 5 in play mode.

## Still open

- `howto.2.body` (the user's line) doesn't mention the hero's fresh-ground rule yet. Suggest a sentence, don't reword it.
- Narrator reactions (earlier request) not started.
- Mixed levels with three special pieces never generated; the last three levels use two each.

## Level generator

Now in the repo: `Tools/LevelGen` (usage at the top of `Gen.cs`). It compiles `Assets/Scripts/Dungeon.cs` and
`Assets/Scripts/DungeonSolver.cs` directly with small Unity stubs, so rule changes need no recopying.

- `add` mode tries one more piece on every free cell of a proven layout. This is how levels 14 to 16 were made;
  random search with two special pieces found nothing in 5 minutes.
- Checks: winnable, single crate can't win (complete search), no crate revisits a cell, turns at most 1.5x the hero's
  escape length, each potion/companion changes the result when removed, companions and the stone potion must act in
  the solution (companions are let off once a crate is stoned). The stone potion is placed in a companion's line with
  a crate beside it; the thief starts 3 to 7 steps from a potion.
- Keep maps at 13x9 or 13x11 with 3 to 4 crates: 15x11 with 4 crates runs past the position limit.

## House rules to keep

Never commit or push (the user does it). No em dashes anywhere. Player text goes in Text.txt and sounds human;
flag the user's own wording issues instead of rewording them. End replies with "Want any of these?" for extras.
