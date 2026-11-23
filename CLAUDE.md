# Fall 2026 Game Jam

Theme: **Villain's Perspective**. The player is the villain and wins by stopping the hero.
Deadline: one week from 2026-11-22 (about 2026-11-29).

## The game: Rearrange the Dungeon (2D, turn-based puzzle)

- A grid dungeon built on a Tilemap. The hero always walks the shortest path to the exit. The path and step count are hidden (the player works them out); `DungeonGame.showHeroPath` turns them back on.
- Each turn the villain (player) takes one action: move or rotate a wall, flood a corridor, steal the key, drop a portcullis.
- Then the hero takes one step. The player wins if the hero can never reach the exit, and loses if the hero gets out.
- Hero goals beyond the exit: a key opens a door, a potion gives an extra step.

Plan:
- Days 1–2: core loop (grid, hero pathfinding, one villain action per turn, win and lose).
- Day 3: more villain actions and hero goals.
- Day 4: 8–12 short levels and a level-select screen.
- Days 5–6: art, sound, a smug villain narrator, undo.
- Day 7: build, fix bugs, submit.

## Working together: the creative work is the user's

The user has moral concerns about leaning fully on AI, so the split is:

- **Creative elements are the user's.** Level layouts, puzzle ideas, the design of new villain actions and hero goals, names, the title, story, narrator lines, art direction, and sound choices. For these, Claude guides instead of supplying answers: ask the user to come up with it, and offer questions, constraints, design principles, or a few directions to think about. Once the user has made something, Claude can react, critique, and check it (for example with the level solver).
- **Code is Claude's.** Claude writes it, and each time explains in plain language what was done and how the logic works.
- **Assets are the user's to find** (art, sound, music, fonts). Claude does not generate images unless the user specifically asks. Placeholder squares, plain colors, and UI or help text are fine.
- When a request mixes the two (for example "make a level"), build the code side and hand the creative side back to the user with guidance.

## Project setup

- Unity 6000.6.10f1, URP with the 2D Renderer (`Assets/Settings/Renderer2D.asset`), editor in 2D mode.
- Packages: 2D Sprite, 2D Tilemap, 2D Tilemap Extras (rule tiles), Input System, `com.unity.pipeline` (Unity CLI bridge).
- Main scene: `Assets/Scenes/Main Scene.unity` (orthographic camera, Global Light 2D).
- Game scripts go in `Assets/Scripts/`.

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
