using System;
using UnityEditor;
using UnityEngine;

// Assert-based checks of the Dungeon rules. Run from the menu or: unity command script eval 'DungeonSelfTest.Run()'
public static class DungeonSelfTest
{
    public static readonly string[] Level1 =
    {
        "#########",
        "#H......#",
        "#.#####.#",
        "#...C...#",
        "#.#####.#",
        "#.......E",
        "#########",
    };

    [MenuItem("Dungeon/Run Self Test")]
    public static string Run()
    {
        // Doing nothing: hero walks the 11-step shortest path and escapes.
        var d = Dungeon.Parse(Level1);
        Check(d.PathToExit().Count == 11, "shortest path is 11 steps");
        int steps = 0, r = 0;
        while (r == 0) { r = d.HeroStep(); steps++; }
        Check(r == -1 && steps == 11, "passing every turn loses on step 11");
        Check(d.HeroStep() == -1, "stepping after escape stays lost");

        // Illegal moves are rejected.
        d = Dungeon.Parse(Level1);
        var crate = new Vector2Int(4, 3);
        Check(!d.MoveCrate(crate, new Vector2Int(4, 4)), "no move into a wall");
        Check(!d.MoveCrate(crate, new Vector2Int(5, 4)), "no diagonal move");
        Check(!d.MoveCrate(new Vector2Int(3, 3), new Vector2Int(2, 3)), "only crates move");

        // Sliding the crate to the exit's only neighbour traps the hero.
        d = Dungeon.Parse(Level1);
        Vector2Int[] route = { new(4, 3), new(5, 3), new(6, 3), new(7, 3), new(7, 2), new(7, 1) };
        for (int i = 1; i < route.Length; i++)
        {
            Check(d.MoveCrate(route[i - 1], route[i]), $"crate move {i}");
            r = d.HeroStep();
            Check(r == (i == route.Length - 1 ? 1 : 0), $"result after move {i}");
        }
        Check(!d.MoveCrate(new Vector2Int(7, 1), new Vector2Int(8, 1)), "no move onto the exit");

        // Solver: finds the 5-turn win above, and proves a crate-less level can't be won.
        var win = DungeonSolver.Solve(Dungeon.Parse(Level1), out bool complete);
        Check(win != null && win.Count == 5, "solver finds the 5-turn win");
        var noCrate = Dungeon.Parse(Level1);
        noCrate.crates.Clear();
        Check(DungeonSolver.Solve(noCrate, out complete) == null && complete, "solver proves no-crate level unwinnable");

        // The hero doesn't go back over its own steps unless it has to.
        d = Dungeon.Parse(new[] { "#########", "#...H...#", "#.#####.#", "#.......#", "####E####" });
        d.hero = new(3, 3); d.heroCameFrom = new(2, 3); d.walked.Add(new(2, 3));
        Check(d.PathToExit()[0] == new Vector2Int(2, 3) && d.HeroRoute()[0] == new Vector2Int(4, 3), "hero keeps going instead of turning back");
        d.crates.Add(new(7, 2));
        Check(d.HeroRoute()[0] == new Vector2Int(2, 3), "hero turns back when the way ahead is blocked");

        // Potions: the hero drinking one steps twice; a crate landing on one gives the villain a free move.
        d = Dungeon.Parse(new[] { "########", "#H.P..E#", "########" });
        d.Resolve(null);
        d.Resolve(null);
        Check(d.hero == new Vector2Int(4, 1) && d.potions.Count == 0, "hero drinks the potion and steps twice");
        d = Dungeon.Parse(new[] { "########", "#H.CP.E#", "########" });
        Check(d.MoveCrate(new(3, 1), new(4, 1)) && d.Resolve(new Vector2Int(4, 1)) == 0 && d.hero == new Vector2Int(1, 1), "claimed potion freezes the hero");

        // Dwarf: walks, smashes a crate instead of moving, turns around at the wall.
        d = Dungeon.Parse(new[] { "######", "#D..C#", "#H..E#", "######" });
        d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(2, 2), "dwarf walks right");
        d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(3, 2) && d.crates.Count == 1, "dwarf reaches the crate");
        d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(3, 2) && d.crates.Count == 0, "dwarf smashes the crate instead of moving");
        d.FoesStep();
        d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(3, 2) && d.foes[0].dir == Vector2Int.left, "dwarf turns around at the wall");
        d.crates.Add(new(3, 1));
        Check(!d.MoveCrate(new(3, 1), new(3, 2)), "no crate onto a dwarf");

        // Brute: pushes a crate ahead until it hits the wall, then turns around.
        d = Dungeon.Parse(new[] { "#######", "#B.C..#", "#H...E#", "#######" });
        for (int i = 0; i < 3; i++) d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(4, 2) && d.crates.Contains(new(5, 2)), "brute pushes the crate along");
        d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(4, 2) && d.foes[0].dir == Vector2Int.left && d.crates.Contains(new(5, 2)), "brute turns around at a stuck crate");

        // Wizard: blasts the first wooden crate in sight; stone blocks his view.
        d = Dungeon.Parse(new[] { "#######", "#W..C.#", "#H...E#", "#######" });
        d.FoesStep();
        Check(d.crates.Count == 0, "wizard blasts a crate in line");
        d = Dungeon.Parse(new[] { "#######", "#W.CC.#", "#H...E#", "#######" });
        d.stones.Add(new(3, 2));
        d.FoesStep();
        Check(d.crates.Count == 2, "stone blocks the wizard");

        // Thief: walks to the nearest potion and takes it.
        d = Dungeon.Parse(new[] { "#######", "#T..Z.#", "#H...E#", "#######" });
        for (int i = 0; i < 3; i++) d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(4, 2) && d.potions.Count == 0, "thief takes the potion");

        // Hunter: walks up to the nearest crate, then smashes it.
        d = Dungeon.Parse(new[] { "#######", "#A...C#", "#H...E#", "#######" });
        for (int i = 0; i < 3; i++) d.FoesStep();
        Check(d.foes[0].cell == new Vector2Int(4, 2) && d.crates.Count == 1, "hunter walks to the crate");
        d.FoesStep();
        Check(d.crates.Count == 0, "hunter smashes it");

        // Sleep, recall and rage potions on the hero.
        d = Dungeon.Parse(new[] { "########", "#H.Z..E#", "########" });
        d.Resolve(null); d.Resolve(null); d.Resolve(null); d.Resolve(null);
        Check(d.hero == new Vector2Int(3, 1) && d.sleep == 0, "sleeping hero misses two turns");
        d.Resolve(null);
        Check(d.hero == new Vector2Int(4, 1), "hero wakes up and walks on");
        d = Dungeon.Parse(new[] { "########", "#H.R..E#", "########" });
        d.Resolve(null); d.Resolve(null);
        Check(d.hero == new Vector2Int(1, 1), "recall sends the hero back to the start");
        d = Dungeon.Parse(new[] { "#######", "#.#C.E#", "#H....#", "#######" });
        Check(d.PathToExit().Count == 5, "a plain hero walks around the crate");
        d = Dungeon.Parse(new[] { "#######", "#H.C.E#", "#G###.#", "#.....#", "#######" });
        d.hero = new(1, 2); d.rage = true; d.walked.Clear(); d.walked.Add(d.hero);
        var rageSteps = 0;
        while (d.HeroStep() == 0) rageSteps++;
        Check(rageSteps == 4 && d.crates.Count == 0 && !d.rage, "raging hero smashes through one crate");

        // Stone potion and wards.
        d = Dungeon.Parse(new[] { "##########", "#H.CS..E.#", "#........#", "##########" });
        Check(d.MoveCrate(new(3, 2), new(4, 2)) && d.Resolve(new Vector2Int(4, 2)) == 0 && d.stones.Contains(new(4, 2)), "crate on a stone potion turns to stone");
        Check(!d.MoveCrate(new(4, 2), new(5, 2)), "stone crates don't move");
        d = Dungeon.Parse(new[] { "#######", "#HC+E.#", "#.....#", "#######" });
        Check(!d.MoveCrate(new(2, 2), new(3, 2)), "no crate onto a ward");

        // Solver's one-crate mode: the tutorial is solvable with a single crate.
        Check(DungeonSolver.Solve(Dungeon.Parse(Level1), out _, singleCrate: true) != null, "single-crate solve of the tutorial");

        // Text.txt: the format parses, and every key the UI and code ask for is there.
        var parsed = GameText.Parse("# note\na = one = two\n b =  \nc = x\\ny\nbroken line");
        Check(parsed.Count == 3 && parsed["a"] == "one = two" && parsed["b"] == "" && parsed["c"] == "x\ny", "Text.txt format parses");
        var keys = new System.Collections.Generic.List<string>();
        foreach (var f in System.IO.Directory.GetFiles("Assets/UI", "*.uxml"))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(f), "(?:text|label)=\"#([^\"]+)\""))
                keys.Add(m.Groups[1].Value);
        foreach (var f in System.IO.Directory.GetFiles("Assets/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(f), "\"([a-z][a-z0-9]*(?:\\.[a-zA-Z0-9]+)+)\""))
                keys.Add(m.Groups[1].Value);
        for (int i = 1; i <= Progress.Levels.Length; i++) { keys.Add($"level{i}.title"); keys.Add($"level{i}.intro"); }
        foreach (var a in Controls.Rebindable) keys.Add("control." + a.name);
        foreach (var k in System.Linq.Enumerable.Concat(Progress.PotionKeys, Progress.FoeKeys)) { keys.Add($"howto.{k}.title"); keys.Add($"howto.{k}.body"); }
        Check(Progress.PotionKeys.Length == System.Enum.GetValues(typeof(Potion)).Length && Progress.FoeKeys.Length == System.Enum.GetValues(typeof(Foe)).Length, "a How to Play key for every potion and companion");
        var lines = GameText.Parse(System.IO.File.ReadAllText("Assets/Resources/Text.txt"));
        var missing = keys.FindAll(k => !lines.ContainsKey(k) && !k.StartsWith("best.") && !k.StartsWith("audio.") && !k.StartsWith("gfx.") && !k.StartsWith("input.") && !k.StartsWith("controls."));
        Check(missing.Count == 0, "Text.txt is missing: " + string.Join(", ", missing));

        // Stars: one par per level, 3 at par, 2 within half again, a hint caps it at 2.
        Check(Progress.Par.Length == Progress.Levels.Length, "a par turn count for every level");
        Check(Progress.StarsFor("Level 2", 10, false) == 3 && Progress.StarsFor("Level 2", 15, false) == 2 && Progress.StarsFor("Level 2", 16, false) == 1
            && Progress.StarsFor("Level 2", 10, true) == 2, "star thresholds");
        Check(Progress.FormatTime(83.44f) == "1:23.4" && Progress.FormatTime(5f) == "0:05.0", "time format");

        // Every sound builds, is audible and doesn't clip.
        Check(Sfx.Clips.Length == Enum.GetValues(typeof(Sound)).Length, "a clip for every sound");
        foreach (var clip in System.Linq.Enumerable.Append(Sfx.Clips, Sfx.MusicClip))
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float peak = 0f;
            foreach (var s in data) peak = float.IsNaN(s) ? 99f : Mathf.Max(peak, Mathf.Abs(s));
            Check(clip.samples > 0 && peak > 0.05f && peak <= 1f, $"sound {clip.name} is audible and doesn't clip (peak {peak})");
        }

        // Crosshair: the click point (hotspot) is the center dot, the gap around it is see-through, and the edge pixels are empty.
        var cross = Crosshair.Texture(Crosshair.Look.Red);
        var pop = Crosshair.Texture(Crosshair.Look.Pop);
        int h = (int)Crosshair.Hotspot.x;
        Check(cross.GetPixel(h, h).a == 1f && cross.GetPixel(h, h + 3).a == 0f && cross.GetPixel(h, h + 8).a == 1f, "crosshair has a dot, a gap and a ring");
        Check(cross.GetPixel(0, 0).a == 0f && cross.GetPixel(Crosshair.Size - 1, h).a == 0f, "crosshair fits inside its image");
        Check(pop.GetPixel(0, 0).a == 0f && pop.GetPixel(Crosshair.Size - 1, h).a == 0f, "pop crosshair fits inside its image");
        Check(Crosshair.Texture(Crosshair.Look.Gold).GetPixel(h, h) != cross.GetPixel(h, h), "the grab crosshair is a different color");
        Check(pop.GetPixel(h + 7, h + 7).a == 1f && cross.GetPixel(h + 7, h + 7).a == 0f, "the pop crosshair's ring is bigger");

        Debug.Log("DungeonSelfTest passed");
        return "passed";
    }

    // Slow (about a minute): re-solves every level and checks Progress.Par still matches.
    [MenuItem("Dungeon/Check Par Turns")]
    public static string CheckPar()
    {
        var wrong = new System.Collections.Generic.List<string>();
        var open = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        for (int i = 0; i < Progress.Levels.Length; i++)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/Scenes/{Progress.Levels[i]}.unity");
            var moves = DungeonSolver.Solve(UnityEngine.Object.FindAnyObjectByType<DungeonGame>().ReadLevel(), out _);
            if (moves == null || moves.Count != Progress.Par[i]) wrong.Add($"{Progress.Levels[i]}: solver {moves?.Count}, Par {Progress.Par[i]}");
        }
        if (open != "") UnityEditor.SceneManagement.EditorSceneManager.OpenScene(open);
        var report = wrong.Count == 0 ? "Par turns all match" : "Par turns wrong: " + string.Join("; ", wrong);
        Debug.Log(report);
        return report;
    }

    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("DungeonSelfTest failed: " + what);
    }
}
