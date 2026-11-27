using System.Collections.Generic;
using UnityEngine;

public enum Potion { Haste, Sleep, Recall, Rage, Stone }
public enum Foe { Dwarf, Brute, Wizard, Thief, Hunter }

// What happened during a turn, in order, so the game can animate it. Recorded only when Dungeon.log is set.
// CrateMoved info: 0 the villain slid it, 1 a brute pushed it. FoeMoved info: the companion's index.
public enum Ev { CrateMoved, CrateSmashed, Blast, CrateStoned, PotionByCrate, PotionByHero, PotionByFoe, HeroRecalled, HeroMoved, FoeMoved }

// Pure grid state and rules, no rendering, so it can be tested, solved and snapshotted.
public class Dungeon
{
    public readonly HashSet<Vector2Int> floor = new(); // every walkable cell, including the exit and cells under crates
    public readonly HashSet<Vector2Int> crates = new(); // movable walls (stone ones too)
    public readonly HashSet<Vector2Int> stones = new(); // crates turned to stone: can't be moved, smashed, pushed or blasted
    public readonly HashSet<Vector2Int> wards = new(); // warded floor: crates can't be put there
    public readonly Dictionary<Vector2Int, Potion> potions = new();
    public readonly List<(Foe kind, Vector2Int cell, Vector2Int dir)> foes = new(); // the hero's companions
    public Vector2Int hero, heroStart, exit;
    public Vector2Int heroCameFrom = None; // the hero's last cell
    public readonly HashSet<Vector2Int> walked = new(); // cells the hero has stood on since the start (or a recall)
    public static readonly Vector2Int None = new(-999, -999);
    public int sleep; // hero turns left asleep
    public bool rage; // hero can smash through one crate
    public List<(Ev ev, Vector2Int a, Vector2Int b, int info)> log;

    public static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public bool Walkable(Vector2Int c) => floor.Contains(c) && !crates.Contains(c);
    public bool IsWood(Vector2Int c) => crates.Contains(c) && !stones.Contains(c);
    public bool HasFoe(Vector2Int c) => foes.Exists(f => f.cell == c);
    bool Solid(Vector2Int c) => !floor.Contains(c) || stones.Contains(c); // what patrols turn around at

    // Where a crate may land: free floor that isn't the hero, the exit, a ward or a companion.
    public bool CrateCanEnter(Vector2Int c) => Walkable(c) && c != hero && c != exit && !wards.Contains(c) && !HasFoe(c);

    // Call once the floor is complete: patrols go left-right if they can, else up-down.
    public void AddFoe(Foe kind, Vector2Int c) =>
        foes.Add((kind, c, floor.Contains(c + Vector2Int.right) || floor.Contains(c + Vector2Int.left) ? Vector2Int.right : Vector2Int.up));

    void Log(Ev ev, Vector2Int a, Vector2Int b, int info = 0) => log?.Add((ev, a, b, info));

    // Shortest path from the hero to the exit (hero's cell excluded, exit included), or null if the hero is trapped.
    // With rage the hero may go through one wooden crate.
    public List<Vector2Int> PathToExit() => FindPath(hero, c => c == exit, rage);

    // The way the hero will actually go: the shortest path over fresh ground; failing that, one that at least
    // doesn't start by turning back; failing that, any shortest path. Without this the villain could stall
    // forever by nudging a crate so the hero paces back and forth or walks in circles.
    public List<Vector2Int> HeroRoute() =>
        FindPath(hero, c => c == exit, rage, avoid: walked) ?? FindPath(hero, c => c == exit, rage, heroCameFrom) ?? PathToExit();

    // Breadth-first search over walkable cells (plus one wooden crate when smashOne). Empty list: already there.
    public List<Vector2Int> FindPath(Vector2Int from, System.Predicate<Vector2Int> goal, bool smashOne = false,
        Vector2Int? notFirst = null, HashSet<Vector2Int> avoid = null)
    {
        var start = new Vector3Int(from.x, from.y, 0); // z = crates smashed so far
        var prev = new Dictionary<Vector3Int, Vector3Int> { [start] = start };
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            var c = (Vector2Int)s;
            if (goal(c))
            {
                var path = new List<Vector2Int>();
                for (; s != start; s = prev[s]) path.Add((Vector2Int)s);
                path.Reverse();
                return path;
            }
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (s == start && n == notFirst || avoid != null && avoid.Contains(n)) continue;
                int used = s.z;
                if (!Walkable(n))
                {
                    if (!smashOne || used > 0 || !IsWood(n)) continue;
                    used = 1;
                }
                var ns = new Vector3Int(n.x, n.y, used);
                if (!prev.ContainsKey(ns)) { prev[ns] = s; queue.Enqueue(ns); }
            }
        }
        return null;
    }

    // Villain action: slide a wooden crate one cell.
    public bool MoveCrate(Vector2Int from, Vector2Int to)
    {
        var step = to - from;
        if (!IsWood(from) || Mathf.Abs(step.x) + Mathf.Abs(step.y) != 1 || !CrateCanEnter(to)) return false;
        crates.Remove(from);
        crates.Add(to);
        Log(Ev.CrateMoved, from, to);
        return true;
    }

    // Everything after the villain's action. `movedTo` is where a crate just landed (null for a wait).
    // A crate landing on a potion uses it up: haste gives the villain another move right away, stone turns the crate to stone.
    // Then the companions act, then the hero.
    public int Resolve(Vector2Int? movedTo)
    {
        if (movedTo is { } c && potions.Remove(c, out var p))
        {
            Log(Ev.PotionByCrate, c, c, (int)p);
            if (p == Potion.Haste) return 0;
            if (p == Potion.Stone) { stones.Add(c); Log(Ev.CrateStoned, c, c); }
        }
        FoesStep();
        return HeroStep();
    }

    public void FoesStep()
    {
        for (int i = 0; i < foes.Count; i++)
        {
            var (kind, c, dir) = foes[i];
            var was = c;
            switch (kind)
            {
                case Foe.Dwarf:
                case Foe.Brute:
                    Patrol(kind, ref c, ref dir);
                    break;
                case Foe.Wizard:
                    Blast(c);
                    break;
                case Foe.Thief:
                    var toPotion = FindPath(c, x => potions.ContainsKey(x));
                    if (toPotion is { Count: > 0 }) c = toPotion[0];
                    break;
                case Foe.Hunter:
                    var target = System.Array.Find(Dirs, d => IsWood(c + d));
                    if (target != default) { crates.Remove(c + target); Log(Ev.CrateSmashed, c + target, c); break; }
                    var toCrate = FindPath(c, x => System.Array.Exists(Dirs, d => IsWood(x + d)));
                    if (toCrate is { Count: > 0 }) c = toCrate[0];
                    break;
            }
            if (c != was) Log(Ev.FoeMoved, was, c, i);
            if (kind == Foe.Thief && potions.Remove(c, out var taken)) Log(Ev.PotionByFoe, c, c, (int)taken);
            foes[i] = (kind, c, dir);
        }
    }

    // Dwarf and brute: walk one cell, turn around at walls and stone. A dwarf smashes a crate in its way;
    // a brute pushes it, and turns around if the crate can't go anywhere.
    void Patrol(Foe kind, ref Vector2Int c, ref Vector2Int dir)
    {
        if (Solid(c + dir)) dir = -dir;
        var n = c + dir;
        if (Solid(n)) return; // boxed in on both sides
        if (IsWood(n))
        {
            if (kind == Foe.Dwarf) { crates.Remove(n); Log(Ev.CrateSmashed, n, c); return; }
            var beyond = n + dir;
            if (!CrateCanEnter(beyond) || potions.ContainsKey(beyond)) { dir = -dir; return; }
            crates.Remove(n);
            crates.Add(beyond);
            Log(Ev.CrateMoved, n, beyond, 1);
        }
        c = n;
    }

    // Wizard: blasts the first wooden crate he can see in each straight line. Walls and stone block his view.
    void Blast(Vector2Int c)
    {
        foreach (var d in Dirs)
            for (var x = c + d; floor.Contains(x); x += d)
            {
                if (stones.Contains(x)) break;
                if (crates.Remove(x)) { Log(Ev.Blast, c, x); break; }
            }
    }

    // Hero's turn. Returns +1 villain wins (hero trapped), -1 villain loses (hero escaped), 0 game goes on.
    public int HeroStep()
    {
        if (hero == exit) return -1;
        if (PathToExit() == null) return 1;
        if (sleep > 0) { sleep--; return 0; }
        var next = HeroRoute()[0];
        if (crates.Remove(next)) { rage = false; Log(Ev.CrateSmashed, next, hero); }
        Log(Ev.HeroMoved, hero, next);
        heroCameFrom = hero;
        hero = next;
        walked.Add(hero);
        if (hero == exit) return -1;
        if (!potions.Remove(hero, out var p)) return 0;
        Log(Ev.PotionByHero, hero, hero, (int)p);
        switch (p)
        {
            case Potion.Haste: return HeroStep();
            case Potion.Sleep: sleep = 2; break;
            case Potion.Rage: rage = true; break;
            case Potion.Recall:
                if (Walkable(heroStart)) { Log(Ev.HeroRecalled, hero, heroStart); hero = heroStart; heroCameFrom = None; walked.Clear(); walked.Add(hero); }
                break;
        }
        return 0;
    }

    public Dungeon Clone()
    {
        var c = new Dungeon { hero = hero, heroStart = heroStart, heroCameFrom = heroCameFrom, exit = exit, sleep = sleep, rage = rage };
        c.floor.UnionWith(floor);
        c.crates.UnionWith(crates);
        c.stones.UnionWith(stones);
        c.wards.UnionWith(wards);
        c.walked.UnionWith(walked);
        foreach (var kv in potions) c.potions.Add(kv.Key, kv.Value);
        c.foes.AddRange(foes);
        return c;
    }

    // ASCII level, first row is the top:
    // '#' wall, '.' floor, '+' warded floor, 'C' crate, 'H' hero, 'E' exit,
    // potions 'P' haste, 'Z' sleep, 'R' recall, 'G' rage, 'S' stone,
    // companions 'D' dwarf, 'B' brute, 'W' wizard, 'T' thief, 'A' hunter.
    public const string PotionChars = "PZRGS", FoeChars = "DBWTA";

    public static Dungeon Parse(string[] rows)
    {
        var d = new Dungeon();
        var foeCells = new List<(Foe, Vector2Int)>();
        for (int r = 0; r < rows.Length; r++)
            for (int x = 0; x < rows[r].Length; x++)
            {
                var c = new Vector2Int(x, rows.Length - 1 - r);
                char ch = rows[r][x];
                if (ch == '#') continue;
                d.floor.Add(c);
                if (ch == '+') d.wards.Add(c);
                if (ch == 'C') d.crates.Add(c);
                if (ch == 'H') d.hero = d.heroStart = c;
                if (ch == 'E') d.exit = c;
                if (PotionChars.IndexOf(ch) is var p and >= 0) d.potions[c] = (Potion)p;
                if (FoeChars.IndexOf(ch) is var f and >= 0) foeCells.Add(((Foe)f, c));
            }
        foreach (var (kind, c) in foeCells) d.AddFoe(kind, c);
        d.walked.Add(d.hero);
        return d;
    }
}
