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

        Debug.Log("DungeonSelfTest passed");
        return "passed";
    }

    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("DungeonSelfTest failed: " + what);
    }
}
