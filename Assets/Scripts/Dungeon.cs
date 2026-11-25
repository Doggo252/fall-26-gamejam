using System.Collections.Generic;
using UnityEngine;

// Pure grid state and rules, no rendering, so it can be tested and snapshotted (undo later).
public class Dungeon
{
    public readonly HashSet<Vector2Int> floor = new(); // every walkable cell, including the exit and cells under crates
    public readonly HashSet<Vector2Int> crates = new(); // movable walls
    public readonly HashSet<Vector2Int> potions = new(); // haste potions: whoever reaches one first moves twice
    public readonly List<(Vector2Int cell, Vector2Int dir)> dwarves = new(); // the hero's companions, patrolling and smashing crates
    public Vector2Int hero, exit;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public bool Walkable(Vector2Int c) => floor.Contains(c) && !crates.Contains(c);

    public bool HasDwarf(Vector2Int c) => dwarves.Exists(w => w.cell == c);

    // Call once the floor is complete: a dwarf patrols left-right if it can, else up-down.
    public void AddDwarf(Vector2Int c) =>
        dwarves.Add((c, floor.Contains(c + Vector2Int.right) || floor.Contains(c + Vector2Int.left) ? Vector2Int.right : Vector2Int.up));

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
        if (!Walkable(to) || to == hero || to == exit || HasDwarf(to)) return false;
        crates.Remove(from);
        crates.Add(to);
        return true;
    }

    // Everything after the villain's action. `movedTo` is where a crate just landed (null for a wait).
    // A crate landing on a potion claims it: the villain moves again and nothing else happens.
    // Otherwise the dwarves patrol, then the hero steps (twice if it drinks a potion).
    public int Resolve(Vector2Int? movedTo)
    {
        if (movedTo is { } c && potions.Remove(c)) return 0;
        DwarvesStep();
        int r = HeroStep();
        if (r == 0 && potions.Remove(hero)) r = HeroStep();
        return r;
    }

    // Each dwarf walks one cell, turns around at walls, and spends its move smashing a crate in its way.
    public void DwarvesStep()
    {
        for (int i = 0; i < dwarves.Count; i++)
        {
            var (c, dir) = dwarves[i];
            if (!floor.Contains(c + dir)) dir = -dir;
            var n = c + dir;
            if (!floor.Contains(n)) n = c; // boxed in on both sides
            else if (crates.Remove(n)) n = c;
            dwarves[i] = (n, dir);
        }
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
        c.potions.UnionWith(potions);
        c.dwarves.AddRange(dwarves);
        return c;
    }

    // ASCII level: '#' wall, '.' floor, 'C' crate, 'H' hero, 'E' exit, 'P' potion, 'D' dwarf. First row is the top.
    public static Dungeon Parse(string[] rows)
    {
        var d = new Dungeon();
        var dwarfCells = new List<Vector2Int>();
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
                if (ch == 'P') d.potions.Add(c);
                if (ch == 'D') dwarfCells.Add(c);
            }
        dwarfCells.ForEach(d.AddDwarf);
        return d;
    }
}
