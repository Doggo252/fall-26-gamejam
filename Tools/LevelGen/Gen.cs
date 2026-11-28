using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Level generator for The UNchosen ONE. Runs outside Unity on the game's own Dungeon.cs and DungeonSolver.cs.
// Build (from Tools/LevelGen): dotnet build -c Release -o out
//   dotnet out/gen.dll <seed> <tries> <w> <h> <loops> <crates> <potions|-> <foes|-> <wardRadius> <minHero> <minWin> <maxPositions> [seconds] [minUsed] [maxWin]
//     e.g. dotnet out/gen.dll 6 40000 13 9 0.25 3 - D 2 12 10 80000 240 2 18
//   dotnet out/gen.dll check <file> [maxPositions]   report on one ASCII layout
//   dotnet out/gen.dll add <file> <char>             try one more piece on every free cell of a layout
static class Gen
{
    class Result
    {
        public string[] rows; public int turns, used, waits, hero; public double score; public string info;
    }

    static int Main(string[] a)
    {
        if (a[0] == "check")
        {
            var rows = File.ReadAllLines(a[1]).Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToArray();
            int checkPos = a.Length > 2 ? int.Parse(a[2]) : 2_000_000;
            Console.WriteLine(Evaluate(rows, checkPos, 0, 0, 0, verbose: true)?.info ?? "REJECTED");
            return 0;
        }
        if (a[0] == "add")
        {
            // gen add <file> <char>: try the piece on every free cell of a proven layout, print the passing ones.
            var baseRows = File.ReadAllLines(a[1]).Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToArray();
            char piece = a[2][0];
            var cells = new List<(int r, int c)>();
            for (int r = 0; r < baseRows.Length; r++) for (int c = 0; c < baseRows[r].Length; c++) if (baseRows[r][c] == '.') cells.Add((r, c));
            var passed = new ConcurrentBag<Result>();
            Parallel.ForEach(cells, cell =>
            {
                var rows = (string[])baseRows.Clone();
                var line = rows[cell.r].ToCharArray(); line[cell.c] = piece; rows[cell.r] = new string(line);
                var res = Evaluate(rows, 150_000, 0, 0, 2, verbose: false);
                if (res != null && res.waits <= 2) passed.Add(res);
            });
            Console.WriteLine($"{cells.Count} cells, {passed.Count} passed");
            foreach (var r in passed.OrderByDescending(r => r.score).Take(4)) Console.WriteLine($"score {r.score}\n{string.Join("\n", r.rows)}\n{r.info}\n");
            return 0;
        }
        int seed = int.Parse(a[0]), tries = int.Parse(a[1]), w = int.Parse(a[2]), h = int.Parse(a[3]);
        double loops = double.Parse(a[4], System.Globalization.CultureInfo.InvariantCulture);
        int crates = int.Parse(a[5]);
        string potions = a[6] == "-" ? "" : a[6], foes = a[7] == "-" ? "" : a[7];
        int ward = int.Parse(a[8]), minHero = int.Parse(a[9]), minWin = int.Parse(a[10]), maxPos = int.Parse(a[11]);
        int seconds = a.Length > 12 ? int.Parse(a[12]) : 240, minUsed = a.Length > 13 ? int.Parse(a[13]) : 2, maxWin = a.Length > 14 ? int.Parse(a[14]) : 99;

        var found = new ConcurrentBag<Result>();
        var clock = Stopwatch.StartNew();
        int done = 0;
        Parallel.For(0, tries, (i, state) =>
        {
            if (clock.Elapsed.TotalSeconds > seconds) { state.Stop(); return; }
            var rng = new Random(seed * 100003 + i);
            var rows = Make(rng, w, h, loops, crates, potions, foes, ward);
            System.Threading.Interlocked.Increment(ref done);
            if (rows == null) return;
            var r = Evaluate(rows, maxPos, minHero, minWin, minUsed, verbose: false, maxWin);
            if (r != null) found.Add(r);
        });
        Console.WriteLine($"tried {done} in {clock.Elapsed.TotalSeconds:0}s, {found.Count} passed");
        foreach (var r in found.OrderByDescending(r => r.score).Take(3))
        {
            Console.WriteLine($"score {r.score}");
            Console.WriteLine(string.Join("\n", r.rows));
            Console.WriteLine(r.info);
            Console.WriteLine();
        }
        return 0;
    }

    static string[] Make(Random rng, int w, int h, double loops, int crates, string potions, string foes, int ward)
    {
        var g = new char[h, w];
        for (int r = 0; r < h; r++) for (int c = 0; c < w; c++) g[r, c] = '#';
        // Depth-first maze over odd cells, then knock out extra walls to make loops.
        var stack = new Stack<(int r, int c)>();
        var start = (1 + 2 * rng.Next((h - 1) / 2), 1 + 2 * rng.Next((w - 1) / 2));
        g[start.Item1, start.Item2] = '.';
        stack.Push(start);
        (int, int)[] steps = { (-2, 0), (2, 0), (0, -2), (0, 2) };
        while (stack.Count > 0)
        {
            var (r, c) = stack.Peek();
            var open = steps.Where(s => r + s.Item1 > 0 && r + s.Item1 < h - 1 && c + s.Item2 > 0 && c + s.Item2 < w - 1 && g[r + s.Item1, c + s.Item2] == '#').ToList();
            if (open.Count == 0) { stack.Pop(); continue; }
            var (dr, dc) = open[rng.Next(open.Count)];
            g[r + dr / 2, c + dc / 2] = '.';
            g[r + dr, c + dc] = '.';
            stack.Push((r + dr, c + dc));
        }
        for (int r = 1; r < h - 1; r++)
            for (int c = 1; c < w - 1; c++)
                if (g[r, c] == '#' && (r % 2 == 1) != (c % 2 == 1) && rng.NextDouble() < loops) g[r, c] = '.';
        int ex = 1 + 2 * rng.Next((w - 1) / 2);
        g[h - 1, ex] = 'E';

        // Distance from the exit, for wards and the hero's start.
        var dist = new int[h, w];
        for (int r = 0; r < h; r++) for (int c = 0; c < w; c++) dist[r, c] = -1;
        var q = new Queue<(int, int)>();
        dist[h - 1, ex] = 0; q.Enqueue((h - 1, ex));
        while (q.Count > 0)
        {
            var (r, c) = q.Dequeue();
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int nr = r + dr, nc = c + dc;
                if (nr < 0 || nc < 0 || nr >= h || nc >= w || g[nr, nc] == '#' || dist[nr, nc] >= 0) continue;
                dist[nr, nc] = dist[r, c] + 1; q.Enqueue((nr, nc));
            }
        }
        for (int r = 0; r < h; r++) for (int c = 0; c < w; c++) if (dist[r, c] >= 1 && dist[r, c] <= ward) g[r, c] = '+';

        var free = new List<(int r, int c)>();
        for (int r = 0; r < h; r++) for (int c = 0; c < w; c++) if (g[r, c] == '.') free.Add((r, c));
        var far = free.Where(p => dist[p.r, p.c] >= Math.Max(ward + 4, (w + h) / 2)).ToList();
        if (far.Count == 0) return null;
        var hero = far[rng.Next(far.Count)];
        g[hero.r, hero.c] = 'H';
        free.Remove(hero);
        foreach (var ch in new string('C', crates) + potions + foes)
        {
            if (free.Count == 0) return null;
            var p = free[rng.Next(free.Count)];
            free.Remove(p);
            g[p.r, p.c] = ch;
        }
        // Stone only matters where a dwarf, brute or wizard would wreck the crate, so put it in their line with a crate beside it.
        var all = Enumerable.Range(0, h).SelectMany(r => Enumerable.Range(0, w).Select(c => (r, c))).ToList();
        var stone = all.FindIndex(p => g[p.r, p.c] == 'S');
        var foe = all.FindIndex(p => "DBW".IndexOf(g[p.r, p.c]) >= 0);
        if (stone >= 0 && foe >= 0)
        {
            var (fr, fc) = all[foe];
            var line = new List<(int r, int c)>();
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                for (int r = fr + dr, c = fc + dc; g[r, c] != '#' && g[r, c] != 'E'; r += dr, c += dc)
                    if (g[r, c] == '.') line.Add((r, c));
            if (line.Count > 0)
            {
                var t = line[rng.Next(line.Count)];
                g[all[stone].r, all[stone].c] = '.';
                g[t.r, t.c] = 'S';
                var crateCells = all.Where(p => g[p.r, p.c] == 'C').ToList();
                var beside = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }.Select(d => (r: t.r + d.Item1, c: t.c + d.Item2)).Where(p => g[p.r, p.c] == '.').ToList();
                if (crateCells.Count > 0 && beside.Count > 0)
                {
                    var from = crateCells[rng.Next(crateCells.Count)];
                    var to = beside[rng.Next(beside.Count)];
                    g[from.r, from.c] = '.';
                    g[to.r, to.c] = 'C';
                }
            }
        }
        // Thief: start it 3 to 7 steps from a potion so there's a race for it.
        var thief = all.FindIndex(p => g[p.r, p.c] == 'T');
        var potion = all.FindIndex(p => "PZRG".IndexOf(g[p.r, p.c]) >= 0);
        if (thief >= 0 && potion >= 0)
        {
            var pd = new Dictionary<(int, int), int> { [all[potion]] = 0 };
            var pq = new Queue<(int r, int c)>();
            pq.Enqueue(all[potion]);
            while (pq.Count > 0)
            {
                var (r, c) = pq.Dequeue();
                foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                {
                    var n = (r + dr, c + dc);
                    if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= h || n.Item2 >= w || g[n.Item1, n.Item2] == '#' || pd.ContainsKey(n)) continue;
                    pd[n] = pd[(r, c)] + 1; pq.Enqueue(n);
                }
            }
            var spots = pd.Where(kv => kv.Value >= 3 && kv.Value <= 7 && g[kv.Key.Item1, kv.Key.Item2] == '.').Select(kv => kv.Key).ToList();
            if (spots.Count > 0)
            {
                var s = spots[rng.Next(spots.Count)];
                g[all[thief].r, all[thief].c] = '.';
                g[s.Item1, s.Item2] = 'T';
            }
        }
        var rows = new string[h];
        for (int r = 0; r < h; r++) rows[r] = new string(Enumerable.Range(0, w).Select(c => g[r, c]).ToArray());
        return rows;
    }

    // Replays a solution: how many crates the villain moved, how many waits, and whether a crate went straight back.
    static (int used, int waits, bool back, HashSet<string> did) Replay(string[] rows, List<DungeonSolver.Move> moves)
    {
        var d = Dungeon.Parse(rows);
        d.log = new();
        var id = new Dictionary<Vector2Int, int>();
        foreach (var c in d.crates) id[c] = id.Count;
        var used = new HashSet<int>();
        int waits = 0; bool back = false;
        var did = new HashSet<string>(); // what the special pieces did along the way
        var been = new Dictionary<int, HashSet<Vector2Int>>(); // cells each crate has been on
        foreach (var kv in id) been[kv.Value] = new() { kv.Key };
        foreach (var m in moves)
        {
            d.log.Clear();
            Vector2Int? to = null;
            if (m.dir < 0) waits++;
            else { to = m.from + Dungeon.Dirs[m.dir]; d.MoveCrate(m.from, to.Value); }
            d.Resolve(to);
            foreach (var e in d.log)
            {
                if (e.ev == Ev.CrateMoved)
                {
                    int i = id[e.a]; id.Remove(e.a); id[e.b] = i;
                    if (e.info != 0) { did.Add("Brute"); continue; }
                    used.Add(i);
                    if (!been[i].Add(e.b)) back = true;
                }
                else if (e.ev == Ev.CrateSmashed) { id.Remove(e.a); var f = d.foes.FindIndex(f => f.cell == e.b); if (f >= 0) did.Add(d.foes[f].kind.ToString()); }
                else if (e.ev == Ev.Blast) { id.Remove(e.b); did.Add("Wizard"); }
                else if (e.ev == Ev.PotionByFoe) did.Add("Thief");
                else if (e.ev == Ev.CrateStoned) did.Add("Stone");
            }
        }
        return (used.Count, waits, back, did);
    }

    static string Name(Dungeon d, Vector2Int c) =>
        d.potions.TryGetValue(c, out var p) ? p.ToString() : d.foes.Find(f => f.cell == c).kind.ToString();

    static Result Evaluate(string[] rows, int maxPos, int minHero, int minWin, int minUsed, bool verbose, int maxWin = 99)
    {
        var d = Dungeon.Parse(rows);
        var log = new List<string>();
        int hero = d.PathToExit()?.Count ?? 0;
        log.Add($"hero escapes in {hero} steps if you do nothing");
        if (hero < minHero) return null;

        var single = DungeonSolver.Solve(Dungeon.Parse(rows), out bool singleComplete, maxPos, singleCrate: true);
        log.Add(single != null ? $"single crate WINS in {single.Count}" : singleComplete ? "single crate: impossible (complete)" : "single crate: gave up");
        if (!verbose && (single != null || !singleComplete)) return null;

        var moves = DungeonSolver.Solve(Dungeon.Parse(rows), out bool complete, maxPos);
        if (moves == null) { log.Add(complete ? "NOT winnable" : "gave up"); return verbose ? new Result { info = string.Join("\n", log) } : null; }
        var (used, waits, back, did) = Replay(rows, moves);
        // Pieces that only teach anything when the solution sees them act.
        var mustAct = d.foes.Select(f => f.kind.ToString()).Concat(d.potions.Values.Where(p => p == Potion.Stone).Select(p => "Stone")).Distinct().ToList();
        // A stoned crate is what stops a dwarf or wizard from acting, so once something is stoned the companions are let off.
        var idle = mustAct.Where(k => !did.Contains(k) && !(did.Contains("Stone") && k != "Stone" && k != "Thief")).ToList();
        if (idle.Count > 0) log.Add("NEVER ACTS IN THE SOLUTION: " + string.Join(", ", idle));
        log.Add($"wins in {moves.Count} turns, {used} crates used, {waits} waits{(back ? ", A CRATE GOES BACK" : "")}");
        log.Add("  " + string.Join(", ", moves));
        if (!verbose && (moves.Count < minWin || used < minUsed || back || moves.Count > hero * 3 / 2 || moves.Count > maxWin || idle.Count > 0)) return null;

        // Every special piece must matter: without it the level is unwinnable, takes a different number of turns, or one crate does.
        var specials = d.potions.Keys.Concat(d.foes.Select(f => f.cell)).ToList();
        foreach (var c in specials)
        {
            var w = Dungeon.Parse(rows);
            w.potions.Remove(c);
            w.foes.RemoveAll(f => f.cell == c);
            var m = DungeonSolver.Solve(w.Clone(), out bool wc, maxPos);
            var s = DungeonSolver.Solve(w.Clone(), out bool sc, maxPos, singleCrate: true);
            bool matters = m == null ? wc : m.Count != moves.Count || s != null;
            log.Add($"without {Name(d, c)} at {c}: {(m == null ? (wc ? "unwinnable" : "gave up") : $"{m.Count} turns")}{(s != null ? ", one crate wins" : "")}{(matters ? "" : "  << DOESN'T MATTER")}");
            if (!verbose && !matters) return null;
        }
        return new Result { rows = rows, turns = moves.Count, used = used, waits = waits, hero = hero, score = moves.Count * 2 + used * 5 - waits * 3, info = string.Join("\n", log) };
    }
}
