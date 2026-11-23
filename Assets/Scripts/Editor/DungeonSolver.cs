using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Level design helper: finds the fewest villain turns that trap the hero, or proves the level can't be won.
// Breadth-first search over game positions (hero cell + crate cells). The hero is deterministic, so a
// position fully decides what happens next, and the first win found uses the fewest turns.
public static class DungeonSolver
{
    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    static readonly string[] DirNames = { "up", "right", "down", "left" };

    // A villain turn: slide the crate at `from` one cell in direction `dir`, or wait when `dir` is -1.
    public struct Move
    {
        public Vector2Int from; public int dir;
        public override string ToString() => dir < 0 ? "wait" : $"crate {from} {DirNames[dir]}";
    }

    // Returns the winning moves, or null. `complete` is true when every reachable position was checked,
    // so null then means the level cannot be won at all.
    public static List<Move> Solve(Dungeon start, out bool complete, int maxPositions = 200_000)
    {
        var seen = new HashSet<string> { Key(start) };
        var queue = new Queue<(Dungeon d, List<Move> moves)>();
        queue.Enqueue((start, new List<Move>()));
        complete = false;
        while (queue.Count > 0)
        {
            if (seen.Count > maxPositions) return null;
            var (d, moves) = queue.Dequeue();
            foreach (var move in Moves(d))
            {
                var next = d.Clone();
                if (move.dir >= 0 && !next.MoveCrate(move.from, move.from + Dirs[move.dir])) continue;
                int result = next.HeroStep();
                if (result == -1) continue; // hero escapes: dead end
                var path = new List<Move>(moves) { move };
                if (result == 1) return path;
                if (seen.Add(Key(next))) queue.Enqueue((next, path));
            }
        }
        complete = true;
        return null;
    }

    static IEnumerable<Move> Moves(Dungeon d)
    {
        yield return new Move { dir = -1 };
        foreach (var c in d.crates.ToList())
            for (int i = 0; i < 4; i++) yield return new Move { from = c, dir = i };
    }

    static string Key(Dungeon d) => d.hero + "|" + string.Join(",", d.crates.OrderBy(c => c.x).ThenBy(c => c.y));

    public static string Report(Dungeon level)
    {
        int heroSteps = level.PathToExit()?.Count ?? 0;
        var moves = Solve(level, out bool complete);
        string head = $"If you do nothing, the hero escapes in {heroSteps} steps.\n";
        if (moves != null)
            return head + $"Winnable in {moves.Count} turns:\n  " + string.Join("\n  ", moves);
        return head + (complete ? "NOT winnable: every possible position was checked." : "Gave up: too many positions to check.");
    }

    [MenuItem("Dungeon/Solve Open Level")]
    public static string SolveOpenLevel()
    {
        var game = Object.FindAnyObjectByType<DungeonGame>();
        if (game == null) { Debug.LogWarning("No DungeonGame in the open scene."); return "no level"; }
        var report = Report(game.ReadLevel());
        Debug.Log(report);
        return report;
    }
}
