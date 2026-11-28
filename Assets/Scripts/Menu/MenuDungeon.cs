using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The living dungeon behind the main menu. A hero walks a random maze toward treasure using the real
// game's Dungeon pathfinding, while the dungeon drops crates in its way and minions roam the halls.
public class MenuDungeon : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite[] floorSprites;
    public Sprite[] decorSprites;
    public Sprite[] minionSprites;
    public Sprite wallFront, wallTop, banner, crate, hero, treasure;

    [Header("Feel")]
    public float stepTime = 0.32f;
    [Range(0, 1)] public float crateChance = 0.2f;
    public int maxCrates = 10;
    public Color torchColor = new(1f, 0.55f, 0.2f), heroLightColor = new(1f, 0.85f, 0.6f);

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    readonly Dungeon d = new();
    readonly List<Vector2Int> floorCells = new();
    readonly Queue<Vector2Int> crateOrder = new();
    readonly Dictionary<Vector2Int, SpriteRenderer> crateSprites = new();
    readonly List<(Light2D light, float baseIntensity, float seed)> torches = new();
    readonly List<Walker> minions = new();
    Transform heroT, treasureT;
    int[] allLayers;
    float timer;

    class Walker { public Transform t; public Vector2Int cell, goal; }

    void Start()
    {
        allLayers = System.Array.ConvertAll(SortingLayer.layers, l => l.id);
        var cam = Camera.main;
        // Odd sizes for the maze lattice, plus a margin so the edges stay off screen.
        int h = Mathf.CeilToInt(cam.orthographicSize * 2f) + 3 | 1;
        int w = Mathf.CeilToInt(cam.orthographicSize * 2f * cam.aspect) + 3 | 1;
        cam.transform.position = new Vector3((w - 1) / 2f, (h - 1) / 2f, cam.transform.position.z);

        var open = Carve(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = new Vector2Int(x, y);
                if (open[x, y])
                {
                    d.floor.Add(c);
                    floorCells.Add(c);
                    Spawn(floorSprites[Random.Range(0, floorSprites.Length)], c, 0);
                    if (Random.value < 0.025f) Spawn(decorSprites[Random.Range(0, decorSprites.Length)], c, 1);
                }
                else if (y > 0 && open[x, y - 1])
                {
                    // Wall with floor below shows its brick face; some carry a banner and a torch.
                    bool torch = Random.value < 0.05f;
                    Spawn(torch ? banner : wallFront, c, 0);
                    if (torch) torches.Add((AddLight(transform, (Vector2)c + Vector2.down * 0.4f, torchColor, 3f, 0.7f), 0.7f, Random.value * 100f));
                }
                else Spawn(wallTop, c, 0);
            }

        d.hero = RandomFloor();
        heroT = Spawn(hero, d.hero, 3).transform;
        AddLight(heroT, Vector2.zero, heroLightColor, 4f, 0.8f);
        treasureT = Spawn(treasure, d.hero, 2).transform;
        AddLight(treasureT, Vector2.zero, new Color(1f, 0.9f, 0.4f), 1.8f, 0.5f);
        NewTreasure();

        foreach (var s in minionSprites)
        {
            var m = new Walker { cell = RandomFloor() };
            m.goal = m.cell;
            m.t = Spawn(s, m.cell, 3).transform;
            minions.Add(m);
        }
    }

    // Random maze (recursive backtracker) with extra openings and a few rooms, so there are loops to reroute through.
    static bool[,] Carve(int w, int h)
    {
        var open = new bool[w, h];
        var stack = new Stack<Vector2Int>();
        open[1, 1] = true;
        stack.Push(new Vector2Int(1, 1));
        var options = new List<Vector2Int>();
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            options.Clear();
            foreach (var dir in Dirs)
            {
                var n = c + dir * 2;
                if (n.x > 0 && n.y > 0 && n.x < w - 1 && n.y < h - 1 && !open[n.x, n.y]) options.Add(dir);
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            var pick = options[Random.Range(0, options.Count)];
            open[c.x + pick.x, c.y + pick.y] = true;
            open[c.x + pick.x * 2, c.y + pick.y * 2] = true;
            stack.Push(c + pick * 2);
        }
        for (int x = 1; x < w - 1; x++)
            for (int y = 1; y < h - 1; y++)
                if (!open[x, y] && Random.value < 0.15f && (open[x - 1, y] && open[x + 1, y] || open[x, y - 1] && open[x, y + 1]))
                    open[x, y] = true;
        for (int r = 0; r < w * h / 120; r++)
        {
            int rx = Random.Range(1, w - 5), ry = Random.Range(1, h - 4);
            for (int x = rx; x < rx + 4; x++)
                for (int y = ry; y < ry + 3; y++) open[x, y] = true;
        }
        return open;
    }

    void Update()
    {
        float speed = Time.deltaTime / stepTime;
        heroT.position = Vector3.MoveTowards(heroT.position, (Vector2)d.hero, speed);
        heroT.localScale = new Vector3(1f, 1f + 0.06f * Mathf.Sin(Time.time * 14f), 1f); // walking bob
        foreach (var m in minions) m.t.position = Vector3.MoveTowards(m.t.position, (Vector2)m.cell, speed * 0.7f);
        treasureT.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 3f));
        foreach (var (light, baseIntensity, seed) in torches)
            light.intensity = baseIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(seed, Time.time * 3f));

        timer += Time.deltaTime;
        if (timer < stepTime) return;
        timer = 0f;
        HeroStep();
        foreach (var m in minions) MinionStep(m);
    }

    void HeroStep()
    {
        if (d.hero == d.exit) { Sfx.Play(Sound.Pop, 1f, 0.25f); NewTreasure(); return; }
        var path = d.PathToExit();
        if (path == null)
        {
            // Walled in: the villain wins this round, then the crates crumble and the hunt resumes.
            Sfx.Play(Sound.Huh, 1f, 0.3f);
            while (crateOrder.Count > 0) RemoveOldestCrate();
            return;
        }
        // The dungeon fights back: drop a crate a few cells ahead of the hero.
        if (path.Count > 5 && Random.value < crateChance) DropCrate(path[Random.Range(2, 5)]);
        d.hero = path[0];
        Sfx.Play(Sound.Step, 0.8f, 0.06f); // background sounds stay quiet so the menu isn't noisy
    }

    void MinionStep(Walker m)
    {
        if (m.cell == m.goal) m.goal = RandomFloor();
        // Borrow the dungeon's pathfinder by pointing it at the minion for a moment.
        var (savedHero, savedExit) = (d.hero, d.exit);
        (d.hero, d.exit) = (m.cell, m.goal);
        var path = d.PathToExit();
        (d.hero, d.exit) = (savedHero, savedExit);
        if (path == null || path.Count == 0) { m.goal = m.cell; return; }
        m.cell = path[0];
    }

    void NewTreasure()
    {
        // Pick a spot far from the hero so the walk is worth watching.
        Vector2Int best = d.hero;
        for (int i = 0; i < 12; i++)
        {
            var c = RandomFloor();
            if (!d.crates.Contains(c) && (c - d.hero).sqrMagnitude > (best - d.hero).sqrMagnitude) best = c;
        }
        d.exit = best;
        treasureT.position = (Vector2)best;
    }

    void DropCrate(Vector2Int c)
    {
        if (c == d.exit || c == d.hero || d.crates.Contains(c)) return;
        if (crateOrder.Count >= maxCrates) RemoveOldestCrate();
        d.crates.Add(c);
        crateOrder.Enqueue(c);
        var r = Spawn(crate, c, 2);
        crateSprites[c] = r;
        StartCoroutine(Pop(r.transform, 1.8f, 1f));
        Sfx.Play(Sound.Pick, 0.8f, 0.2f);
    }

    void RemoveOldestCrate()
    {
        var c = crateOrder.Dequeue();
        d.crates.Remove(c);
        StartCoroutine(Pop(crateSprites[c].transform, 1f, 0f, destroy: true));
        crateSprites.Remove(c);
    }

    static IEnumerator Pop(Transform t, float from, float to, bool destroy = false)
    {
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.18f)
        {
            t.localScale = Vector3.one * Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        t.localScale = Vector3.one * to;
        if (destroy) Destroy(t.gameObject);
    }

    Vector2Int RandomFloor() => floorCells[Random.Range(0, floorCells.Count)];

    SpriteRenderer Spawn(Sprite s, Vector2Int c, int order)
    {
        var go = new GameObject(s.name);
        go.transform.SetParent(transform);
        go.transform.position = (Vector2)c;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = s;
        r.sortingOrder = order;
        return r;
    }

    Light2D AddLight(Transform parent, Vector2 pos, Color color, float radius, float intensity)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.targetSortingLayers = allLayers; // lights added from code start with no target layers
        l.color = color;
        l.intensity = intensity;
        l.pointLightInnerRadius = 0f;
        l.pointLightOuterRadius = radius;
        return l;
    }
}
