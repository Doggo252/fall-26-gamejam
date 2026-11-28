# Fall 2026 Game Jam

Theme: **Villain's Perspective**. The player is the villain and wins by stopping the hero.
Deadline: one week from 2026-11-22 (about 2026-11-29).

## The game: Rearrange the Dungeon (2D, turn-based puzzle)

- A grid dungeon built on a Tilemap. The hero always walks the shortest path to the exit. The path and step count are hidden (the player works them out); `DungeonGame.showHeroPath` turns them back on.
- Each turn the villain (player) takes one action: move or rotate a wall, flood a corridor, steal the key, drop a portcullis.
- Then the hero takes one step. The player wins if the hero can never reach the exit, and loses if the hero gets out.
- Hero goals beyond the exit: a key opens a door, a potion gives an extra step.
- Direction change (2026-11-23): the core is still puzzles about stopping the hero from escaping, but the villain also gets obstacles and power-ups to make runs more interesting. Dice and cards are the leading idea (for example, draw or roll for your action options). Exact mechanics are still the user's to design.
- Title: **The UNchosen ONE** (keep that casing: "UN" and "ONE" are the emphasized parts).

Plan:

- Days 1–2: core loop (grid, hero pathfinding, one villain action per turn, win and lose).
- Day 3: more villain actions and hero goals.
- Day 4: 8–12 short levels and a level-select screen.
- Days 5–6: art, sound, a smug villain narrator, undo.
- Day 7: build, fix bugs, submit.

## Project setup

- Unity 6000.6.10f1, URP with the 2D Renderer (`Assets/Settings/Renderer2D.asset`), editor in 2D mode.
- Packages: 2D Sprite, 2D Tilemap, 2D Tilemap Extras (rule tiles), Input System, `com.unity.pipeline` (Unity CLI bridge).
- Main scene: `Assets/Scenes/Main Scene.unity` (orthographic camera, Global Light 2D).
- Game scripts go in `Assets/Scripts/`.
- Main menu: `Assets/Scenes/Main Menu.unity` (build index 0), built by `Tools/BuildMenu.cs`. The UI is UI Toolkit: `Assets/UI/MainMenu.uxml` + `MainMenu.uss` (loaded through the `MenuTheme.tss` panel theme), driven by `Assets/Scripts/Menu/MainMenu.cs`. The live background is `MenuDungeon.cs`.
- Levels are listed in `Progress.Levels` (scene names, in order): `Main Scene` is the tutorial (walkthrough until first beaten), then `Level 2` to `Level 16`, each introducing one piece (see `level*.intro` in Text.txt). `Tools/BuildLevels.cs` holds their ASCII layouts, gives the tiles Kenney art, rebuilds those scenes and Build Settings. Check any new layout with `DungeonSolver` before shipping it. `Progress.Complete(level, turns, stars)` marks a level beaten when the hero is trapped and keeps the best (fewest) turn count and most stars. `Progress.Par` holds each level's fewest possible turns (3 stars at par, 2 within half again, 1 otherwise; using a hint caps a run at 2): after changing a layout, run Dungeon > Check Par Turns and update it.
- Pieces (rules in `Dungeon.cs`, comments there): potions Haste, Sleep, Recall, Rage, Stone; companions Dwarf, Brute, Wizard, Thief, Hunter; wards (`+`, no crates allowed, placed by the exit). Turn order: crate-on-potion effect, companions, hero.
- New-piece cards: the first time a level has a potion or companion the player hasn't met, `DungeonGame.Guide` shows its How to Play title and text on the hint card (with its icon and cells marked) before the level intro. "Got it" saves `seen.<key>` in PlayerPrefs (`Progress.Seen`/`MarkSeen`); Reset progress clears them. Piece keys are `Progress.PotionKeys`/`FoeKeys`, in enum order, and must match `howto.<key>.*` in Text.txt.
- `DungeonSolver` lives in `Assets/Scripts` (not Editor) because the in-game Hint button runs it on a background thread. It must only touch `Dungeon`, never Unity objects.
- Sound effects and music are synthesized in `Sfx.cs` (no audio files): `Sfx.Play(Sound.X)`.
- Hero route rule: shortest path over ground it hasn't walked yet, else one that doesn't turn straight back, else any shortest path. Stops the villain stalling the hero forever.
- `Tools/LevelGen` is a .NET console app (outside Unity, uses the game's own `Dungeon.cs` and `DungeonSolver.cs`) that generates and checks levels much faster than the editor. Usage is at the top of `Gen.cs`.
- Every shipped level must be winnable, unwinnable with a single crate (`DungeonSolver.Solve(..., singleCrate: true)`, complete search), every potion/companion must change the solution when removed and act in it, and no crate in the solution returns to a cell it was on.
- Every level scene needs `Assets/UI/PauseUI.prefab` (pause screen, win/lose panel, hint card, level fade-in, and the scene's EventSystem). `Tools/BuildPause.cs` rebuilds it. The Settings panel is shared: `Assets/UI/Settings.uxml` + `SettingsPanel.cs`, included by both the menu and the pause screen.
- All player-facing text lives in `Assets/Resources/Text.txt` (`key = text`). UXML uses `text="#key"`, code uses `GameText.Get("key")`. Never hard-code player text. `DungeonSelfTest` fails if a key is missing.
- Gameplay input goes through `Controls` (rebindable, saved in PlayerPrefs). Do not read keys directly. Options live in `GameSettings`.
- Fonts: Cinzel Decorative (titles) and Cinzel (UI), SIL OFL, in `Assets/UI/Fonts`. Menu art: Kenney Tiny Dungeon (CC0) in `Assets/Art/Kenney/TinyDungeon`.

## Driving the editor (Unity CLI)

- Wait for the editor: `unity status --until-ready`. If none is running, `unity open . --until-ready`.
- Keep it ticking when unfocused: `unity command editor set_auto_tick --enabled true`.
- After editing C#: `unity command script recompile`, then `unity command script recompile_status` until it completes.
- Check for errors: `unity command console status` (consoleErrors must be 0).
- See the result: `unity command capture game_view --output-path <png>`.
- Bulk scene or asset setup: write a builder script outside `Assets/` and run it with `unity command script run --file <file> --entry Class.Method`.
- In PowerShell, pass `script eval` code in single quotes and use double quotes inside. Single-quoted C# strings become char literals and fail to compile.

## Extras: ask, don't skip

When ponytail (or any minimal-change approach) would leave out an extra such as polish, juice, an option or a nicer UI, don't drop it silently. Build the core change, then end the reply with **"Want any of these?"** listing the extras so the user can pick. Keep a separate line for anything not checked and any risk.

## Git rules

- **Never commit or push.** The user handles all git commits and pushes.
- If a commit ever does happen, **never add a `Co-Authored-By` line** or any other AI attribution to the message.
