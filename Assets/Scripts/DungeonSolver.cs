using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Finds the fewest villain turns that trap the hero, or proves the level can't be won. Used for level design
// and, in game, for the Hint button (on a background thread: it only touches Dungeon, never Unity objects).
// Breadth-first search over whole game positions (hero, crates, potions, companions, hero status). Everything
// in the dungeon is deterministic, so a position fully decides what happens next, and the first win found
// uses the fewest turns.
public static class DungeonSolver
{
    static readonly Vector2Int[] Dirs = Dungeon.Dirs;
    static readonly string[] DirNames = { "up", "right", "down", "left" };

    // A villain turn: slide the crate at `from` one cell in direction `dir`, or wait when `dir` is -1.
    public struct Move
    {
        public Vector2Int from; public int dir;
        public override string ToString() => dir < 0 ? "wait" : $"crate {from} {DirNames[dir]}";
    }

    // Returns the winning moves, or null. `complete` is true when every reachable position was checked,
    // so null then means the level cannot be won at all.
    // singleCrate: the villain may only ever move one crate (whichever it moves first), to prove a level needs more.
    public static List<Move> Solve(Dungeon start, out bool complete, int maxPositions = 200_000, bool singleCrate = false)
    {
        var none = new Vector2Int(-1, -1);
        var nodes = new List<(int parent, Move move)> { (-1, default) };
        var seen = new HashSet<string> { Key(start, none) };
        var queue = new Queue<(Dungeon d, int node, Vector2Int moved)>();
        queue.Enqueue((start, 0, none));
        complete = false;
        while (queue.Count > 0)
        {
            if (seen.Count > maxPositions) return null;
            var (d, node, moved) = queue.Dequeue();
            foreach (var move in Moves(d, singleCrate ? moved : none))
            {
                var next = d.Clone();
                var to = move.dir >= 0 ? move.from + Dirs[move.dir] : (Vector2Int?)null;
                if (to is { } t && !next.MoveCrate(move.from, t)) continue;
                int result = next.Resolve(to);
                if (result == -1) continue; // hero escapes: dead end
                var nextMoved = to ?? moved;
                if (result == 1)
                {
                    var path = new List<Move> { move };
                    for (int n = node; n > 0; n = nodes[n].parent) path.Add(nodes[n].move);
                    path.Reverse();
                    return path;
                }
                if (seen.Add(Key(next, singleCrate ? nextMoved : none)))
                {
                    nodes.Add((node, move));
                    queue.Enqueue((next, nodes.Count - 1, nextMoved));
                }
            }
        }
        complete = true;
        return null;
    }

    static IEnumerable<Move> Moves(Dungeon d, Vector2Int only)
    {
        yield return new Move { dir = -1 };
        foreach (var c in d.crates.ToList())
        {
            if (d.stones.Contains(c) || only.x >= 0 && c != only) continue;
            for (int i = 0; i < 4; i++) yield return new Move { from = c, dir = i };
        }
    }

    // Compact position key: one char per cell (levels are under 64 wide and tall).
    static string Key(Dungeon d, Vector2Int moved)
    {
        static char C(Vector2Int c) => (char)(c.x * 64 + c.y + 64);
        var sb = new System.Text.StringBuilder();
        sb.Append(C(d.hero)).Append(C(d.heroCameFrom == Dungeon.None ? Vector2Int.zero : d.heroCameFrom - d.hero + Vector2Int.one)).Append((char)('0' + d.sleep)).Append(d.rage ? 'r' : '-').Append(C(moved + Vector2Int.one));
        foreach (var c in d.crates.OrderBy(c => c.x * 64 + c.y)) sb.Append(C(c)).Append(d.stones.Contains(c) ? 's' : 'w');
        sb.Append('|');
        foreach (var kv in d.potions.OrderBy(kv => kv.Key.x * 64 + kv.Key.y)) sb.Append(C(kv.Key)).Append((char)('0' + (int)kv.Value));
        sb.Append('|');
        foreach (var f in d.foes) sb.Append(C(f.cell)).Append((char)('0' + System.Array.IndexOf(Dirs, f.dir)));
        sb.Append('|');
        foreach (var c in d.walked.OrderBy(c => c.x * 64 + c.y)) sb.Append(C(c));
        return sb.ToString();
    }

    public static string Report(Dungeon level)
    {
        int heroSteps = level.PathToExit()?.Count ?? 0;
        var moves = Solve(level, out bool complete);
        string head = $"If you do nothing, the hero escapes in {heroSteps} steps.\n";
        if (moves != null)
            return head + $"Winnable in {moves.Count} turns:\n  " + string.Join("\n  ", moves);
        return head + (complete ? "NOT winnable: every possible position was checked." : "Gave up: too many positions to check.");
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Dungeon/Solve Open Level")]
    public static string SolveOpenLevel()
    {
        var game = Object.FindAnyObjectByType<DungeonGame>();
        if (game == null) { Debug.LogWarning("No DungeonGame in the open scene."); return "no level"; }
        var report = Report(game.ReadLevel());
        Debug.Log(report);
        return report;
    }
#endif
}
