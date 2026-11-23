using System.Collections.Generic;
using UnityEngine;

// Pure grid state and rules, no rendering, so it can be tested and snapshotted (undo later).
public class Dungeon
{
    public readonly HashSet<Vector2Int> floor = new(); // every walkable cell, including the exit and cells under crates
    public readonly HashSet<Vector2Int> crates = new(); // movable walls
    public Vector2Int hero, exit;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public bool Walkable(Vector2Int c) => floor.Contains(c) && !crates.Contains(c);

    // Shortest path from the hero to the exit (hero's cell excluded, exit included), or null if the hero is trapped.
    public List<Vector2Int> PathToExit()
    {
        var prev = new Dictionary<Vector2Int, Vector2Int> { [hero] = hero };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(hero);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == exit)
            {
                var path = new List<Vector2Int>();
                for (; c != hero; c = prev[c]) path.Add(c);
                path.Reverse();
                return path;
            }
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (Walkable(n) && !prev.ContainsKey(n)) { prev[n] = c; queue.Enqueue(n); }
            }
        }
        return null;
    }

    // Villain action: slide a crate one cell onto free floor (not the hero, not the exit).
    public bool MoveCrate(Vector2Int from, Vector2Int to)
    {
        var step = to - from;
        if (!crates.Contains(from) || Mathf.Abs(step.x) + Mathf.Abs(step.y) != 1) return false;
        if (!Walkable(to) || to == hero || to == exit) return false;
        crates.Remove(from);
        crates.Add(to);
        return true;
    }

    // Hero's turn. Returns +1 villain wins (hero trapped), -1 villain loses (hero escaped), 0 game goes on.
    public int HeroStep()
    {
        if (hero == exit) return -1;
        var path = PathToExit();
        if (path == null) return 1;
        hero = path[0];
        return hero == exit ? -1 : 0;
    }

    public Dungeon Clone()
    {
        var c = new Dungeon { hero = hero, exit = exit };
        c.floor.UnionWith(floor);
        c.crates.UnionWith(crates);
        return c;
    }

    // ASCII level: '#' wall, '.' floor, 'C' crate, 'H' hero, 'E' exit. First row is the top.
    public static Dungeon Parse(string[] rows)
    {
        var d = new Dungeon();
        for (int r = 0; r < rows.Length; r++)
            for (int x = 0; x < rows[r].Length; x++)
            {
                var c = new Vector2Int(x, rows.Length - 1 - r);
                char ch = rows[r][x];
                if (ch == '#') continue;
                d.floor.Add(c);
                if (ch == 'C') d.crates.Add(c);
                if (ch == 'H') d.hero = c;
                if (ch == 'E') d.exit = c;
            }
        return d;
    }
}
