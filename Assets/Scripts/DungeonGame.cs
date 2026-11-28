using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Reads the level from the Tilemap, runs the villain/hero turn loop, animates each turn, and draws selection and hints.
// The Tilemap keeps only the static board (walls, floor, wards, exit). Crates, potions, companions and the hero are
// sprites so they can slide, hop, break and pop. Runs after PauseMenu so the HUD exists when Start shows the first hint.
[DefaultExecutionOrder(100)]
public class DungeonGame : MonoBehaviour
{
    public Tilemap map, overlay;
    public TileBase floorTile, exitTile, heroStartTile, wardTile, markerTile;
    public Tile crateTile;
    [Tooltip("One per Potion, in enum order: Haste, Sleep, Recall, Rage, Stone.")]
    public Tile[] potionTiles = new Tile[5];
    [Tooltip("One per Foe, in enum order: Dwarf, Brute, Wizard, Thief, Hunter.")]
    public Tile[] foeTiles = new Tile[5];
    public Transform hero;
    public Color pathColor = new(1f, 0.85f, 0.2f, 0.45f), selectColor = new(0.3f, 0.8f, 1f, 0.6f),
        moveColor = new(0.3f, 0.8f, 1f, 0.3f), hintColor = new(0.85f, 0.12f, 0.1f, 0.55f),
        stoneTint = new(0.55f, 0.58f, 0.65f), blastColor = new(0.85f, 0.55f, 1f), rageTint = new(1f, 0.45f, 0.4f);
    public float stepTime = 0.16f; // seconds per hop or slide
    public bool showHeroPath; // off: the hero's route and step count are hidden, so the player has to work them out
    [Tooltip("How far in the player can zoom, as a fraction of the size that fits the whole level.")]
    public float minZoom = 0.4f;
    [Tooltip("Step-by-step walkthrough instead of the intro card, until this level is beaten once.")]
    public bool tutorial;

    public int Turns { get; private set; } // villain turns taken so far (moves and waits)

    static readonly Vector2Int[] Dirs = Dungeon.Dirs;

    Dungeon d;
    Vector2Int? selected;
    int result; // +1 villain won, -1 hero escaped, 0 playing
    int guidePage; // tutorial: 0 and 1 are the intro pages, 2 is hands-on
    bool newBest, guiding, animating;
    int heroSteps; // alternates the squeak pitch
    float playTime; // seconds since the first grab or wait, not counting pauses
    bool newBestTime;
    float shakeLeft, shakeSize;
    Vector3 shakeOffset;

    // Hint: the solver runs on a background thread. Its winning moves stay good while the player follows them,
    // since the dungeon is deterministic, so only the first hint of a streak has to wait for it.
    static readonly string[] DirNames = { "up", "right", "down", "left" }; // Dungeon.Dirs order
    System.Threading.Tasks.Task<(List<DungeonSolver.Move> moves, bool complete)> hintJob;
    List<DungeonSolver.Move> hintPath;
    int hintJobTurn;
    bool hintShown, hintGaveUp, hinted;
    const float HintCooldown = 10f; // seconds between hints, so the player has to think a bit first
    float hintReadyAt;
    int hintWaitShown;
    Vector2Int? hoverCrate; // the crate under the mouse last frame, for the crosshair pop and tick
    float fitSize; // camera size that shows the whole level
    Sprite square;

    // Each moving thing is a root (moved around the grid) with a child sprite (bobbed, squashed, tinted).
    class View { public Transform root; public SpriteRenderer art; public float phase; }
    View heroView;
    readonly Dictionary<Vector2Int, View> crateViews = new(), potionViews = new();
    readonly List<View> foeViews = new();

    void Start()
    {
        d = ReadLevel();
        guiding = tutorial && !Progress.IsComplete(SceneManager.GetActiveScene().name);
        square = ((Tile)markerTile).sprite;

        // The tilemap keeps the static board; everything that moves becomes a sprite.
        foreach (var c in d.floor)
            if (c != d.exit) map.SetTile((Vector3Int)c, d.wards.Contains(c) ? wardTile : floorTile);
        AddFloorUnderlay();
        foreach (var c in d.crates) crateViews[c] = MakeView(crateTile.sprite, crateTile.color, c, 3);
        foreach (var (c, p) in d.potions) potionViews[c] = MakeView(potionTiles[(int)p].sprite, potionTiles[(int)p].color, c, 2);
        foreach (var f in d.foes) foeViews.Add(MakeView(foeTiles[(int)f.kind].sprite, Color.white, f.cell, 4));
        var heroRoot = new GameObject("Hero").transform;
        heroRoot.SetParent(transform);
        heroRoot.position = Cell(d.hero);
        hero.SetParent(heroRoot, false);
        hero.localPosition = Vector3.zero;
        heroView = new View { root = heroRoot, art = hero.GetComponent<SpriteRenderer>() };
        heroView.art.sortingOrder = 5;
        d.log = new();

        var cam = Camera.main;
        var b = map.localBounds;
        cam.transform.position = new Vector3(b.center.x, b.center.y, cam.transform.position.z);
        cam.orthographicSize = fitSize = Mathf.Max(b.extents.y, b.extents.x / cam.aspect) + 1f;
        Redraw();
    }

    Vector3 Cell(Vector2Int c) => map.GetCellCenterWorld((Vector3Int)c);

    View MakeView(Sprite sprite, Color color, Vector2Int c, int order)
    {
        var root = new GameObject(sprite.name).transform;
        root.SetParent(transform);
        root.position = Cell(c);
        var art = new GameObject("Art").AddComponent<SpriteRenderer>();
        art.transform.SetParent(root, false);
        art.sprite = sprite;
        art.color = color;
        art.sortingOrder = order;
        return new View { root = root, art = art, phase = Random.value * 10f };
    }

    // The exit door has see-through edges, so a floor layer goes underneath every walkable cell.
    void AddFloorUnderlay()
    {
        var go = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(map.transform.parent, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = map.GetComponent<TilemapRenderer>().sortingOrder - 1;
        var floor = go.GetComponent<Tilemap>();
        foreach (var c in d.floor) floor.SetTile((Vector3Int)c, floorTile);
    }

    // Turns the painted Tilemap into a Dungeon. Public so the editor solver can read the open level too.
    public Dungeon ReadLevel()
    {
        var level = new Dungeon();
        var foes = new List<(Foe, Vector2Int)>();
        int heroes = 0, exits = 0;
        map.CompressBounds();
        foreach (var p in map.cellBounds.allPositionsWithin)
        {
            var t = map.GetTile(p);
            if (t == null) continue;
            var c = (Vector2Int)p;
            int potion = System.Array.IndexOf(potionTiles, t), foe = System.Array.IndexOf(foeTiles, t);
            if (t == floorTile || t == crateTile || t == exitTile || t == heroStartTile || t == wardTile || potion >= 0 || foe >= 0) level.floor.Add(c);
            if (t == wardTile) level.wards.Add(c);
            if (t == crateTile) level.crates.Add(c);
            if (potion >= 0) level.potions[c] = (Potion)potion;
            if (foe >= 0) foes.Add(((Foe)foe, c));
            if (t == exitTile) { level.exit = c; exits++; }
            if (t == heroStartTile) { level.hero = level.heroStart = c; level.walked.Add(c); heroes++; }
        }
        // Anything else (Wall tiles or empty) counts as wall.
        foreach (var (kind, c) in foes) level.AddFoe(kind, c);
        if (heroes != 1 || exits != 1)
            Debug.LogError($"Level needs exactly 1 HeroStart and 1 Exit tile, found {heroes} and {exits}.", this);
        return level;
    }

    void OnDestroy() => Crosshair.Show(false); // leaving while over a crate

    void Update()
    {
        Idle();
        var over = HoveredCrate();
        if (over != null && over != hoverCrate) { Crosshair.Pop(); Sfx.Play(Sound.Click, 1.3f, 0.35f); } // landed on a new crate
        hoverCrate = over;
        Crosshair.Show(over != null);
        if (PauseMenu.Paused) return; // Esc (Controls.Menu) is handled by the pause screen
        var camT = Camera.main.transform;
        camT.position -= shakeOffset; // pan and zoom work on the steady view
        MoveCamera();
        shakeOffset = Vector3.zero;
        if (shakeLeft > 0f)
        {
            shakeLeft -= Time.deltaTime;
            shakeOffset = Random.insideUnitCircle * shakeSize * Mathf.Clamp01(shakeLeft / 0.2f);
            camT.position += shakeOffset;
        }
        if (Controls.Restart.WasPressedThisFrame()) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        if (result == 0 && (Turns > 0 || selected != null)) playTime += Time.deltaTime;
        if (hintJob is { IsCompleted: true } job) HintReady(job);
        int hintWait = Mathf.Max(0, Mathf.CeilToInt(hintReadyAt - Time.time));
        if (hintWait != hintWaitShown) PauseMenu.HintWait(hintWaitShown = hintWait);
        if (result != 0 || animating) return;

        if (Controls.Wait.WasPressedThisFrame()) { Sfx.Play(Sound.Wait); EndVillainTurn(null, null); }
        if (Controls.Hint.WasPressedThisFrame()) ShowHint();

        var mouse = Mouse.current;
        if (mouse != null && Controls.Select.WasPressedThisFrame() && !PauseMenu.IsOverUI(mouse.position.ReadValue()))
            Click((Vector2Int)map.WorldToCell(Camera.main.ScreenToWorldPoint(mouse.position.ReadValue())));
    }

    // Always-on motion: breathing characters, bobbing potions, the grabbed crate floating, pulsing markers.
    void Idle()
    {
        float t = Time.time;
        overlay.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(t * 5f));
        foreach (var v in potionViews.Values) v.art.transform.localPosition = new Vector3(0f, 0.06f * Mathf.Sin(t * 3f + v.phase), 0f);
        foreach (var (c, v) in crateViews)
            v.art.transform.localPosition = new Vector3(0f, c == selected ? 0.12f + 0.03f * Mathf.Sin(t * 8f) : 0f, 0f);
        foreach (var v in foeViews) Breathe(v, 2.5f);

        var h = heroView.art;
        bool asleep = d.sleep > 0 && result == 0 && !animating;
        h.transform.localRotation = Quaternion.Euler(0f, 0f, asleep ? 80f : 0f);
        h.color = asleep ? new Color(0.6f, 0.65f, 0.85f) : d.rage ? Color.Lerp(Color.white, rageTint, 0.5f + 0.5f * Mathf.Sin(t * 10f)) : Color.white;
        Breathe(heroView, asleep ? 1f : 3f);
    }

    static void Breathe(View v, float speed)
    {
        float s = 0.04f * Mathf.Sin(Time.time * speed + v.phase);
        v.art.transform.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f);
    }

    // Scroll zooms toward the cursor, holding Pan (right mouse by default) drags the view. Both scale with mouse sensitivity.
    void MoveCamera()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        var cam = Camera.main;
        float sens = GameSettings.MouseSensitivity;
        var pointer = mouse.position.ReadValue();

        float scroll = mouse.scroll.ReadValue().y; // about 1 per wheel notch (uniform scroll deltas)
        if (scroll != 0f)
        {
            var before = cam.ScreenToWorldPoint(pointer);
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * Mathf.Pow(0.88f, scroll * sens), fitSize * minZoom, fitSize);
            cam.transform.position += before - cam.ScreenToWorldPoint(pointer);
        }
        if (Controls.Pan.IsPressed())
        {
            float worldPerPixel = 2f * cam.orthographicSize / Screen.height;
            cam.transform.position -= (Vector3)(mouse.delta.ReadValue() * worldPerPixel * sens);
        }

        // Keep the view centered somewhere over the level.
        var b = map.localBounds;
        var p = cam.transform.position;
        cam.transform.position = new Vector3(Mathf.Clamp(p.x, b.min.x, b.max.x), Mathf.Clamp(p.y, b.min.y, b.max.y), p.z);
    }

    // The crate a click right now would pick up, if any: turns the crosshair gold.
    Vector2Int? HoveredCrate()
    {
        var mouse = Mouse.current;
        if (mouse == null || result != 0 || animating || PauseMenu.Paused) return null;
        var pos = mouse.position.ReadValue();
        var cell = (Vector2Int)map.WorldToCell(Camera.main.ScreenToWorldPoint(pos));
        return !PauseMenu.IsOverUI(pos) && d.IsWood(cell) ? cell : null;
    }

    void Click(Vector2Int cell)
    {
        if (d.IsWood(cell))
        {
            selected = cell == selected ? null : cell;
            Sfx.Play(selected == null ? Sound.Drop : Sound.Pick);
            guidePage = 2; // grabbing a crate skips the tutorial's intro pages
        }
        else if (selected is { } from && d.MoveCrate(from, cell))
        {
            selected = null;
            EndVillainTurn(from, cell);
            return;
        }
        else if (selected != null) { selected = null; Sfx.Play(Sound.Nope); } // the crate can't go there, so drop the selection
        Redraw();
    }

    // from/movedTo: the crate's move, or both null for a wait. The crate move itself is already in the log.
    void EndVillainTurn(Vector2Int? from, Vector2Int? movedTo)
    {
        // Followed the hint: the rest of its solution still works. Anything else: work it out again next time.
        int dir = from is { } f && movedTo is { } t ? System.Array.IndexOf(Dirs, t - f) : -1;
        if (hintPath != null && hintPath[0].dir == dir && (dir < 0 || hintPath[0].from == from)) hintPath.RemoveAt(0);
        else hintPath = null;
        if (hintPath is { Count: 0 }) hintPath = null;
        hintShown = false;

        Turns++;
        guidePage = 2;
        result = d.Resolve(movedTo);
        var events = d.log;
        d.log = new();
        if (result == 1)
        {
            string level = SceneManager.GetActiveScene().name;
            newBest = Progress.Complete(level, Turns, Progress.StarsFor(level, Turns, hinted));
            newBestTime = Progress.CompleteTime(level, playTime);
        }
        StartCoroutine(PlayTurn(events));
    }

    // The Hint button and key. Shows the next winning move, working it out first if needed.
    public void ShowHint()
    {
        if (result != 0 || PauseMenu.Paused || hintShown) return;
        if (Time.time < hintReadyAt) { Sfx.Play(Sound.Nope); return; }
        hintReadyAt = Time.time + HintCooldown;
        hinted = true;
        hintShown = true;
        if (hintPath == null && hintJob == null)
        {
            var start = d.Clone();
            hintJobTurn = Turns;
            hintJob = System.Threading.Tasks.Task.Run(() => (DungeonSolver.Solve(start, out bool complete), complete));
        }
        Redraw();
    }

    void HintReady(System.Threading.Tasks.Task<(List<DungeonSolver.Move> moves, bool complete)> job)
    {
        hintJob = null;
        if (hintJobTurn != Turns) return; // the player moved on while it was thinking
        if (job.IsFaulted) { Debug.LogException(job.Exception); hintGaveUp = true; }
        else { hintPath = job.Result.moves; hintGaveUp = !job.Result.complete; }
        if (!animating) Redraw();
    }

    void HintCard()
    {
        string title = GameText.Get("hint.title");
        if (hintJob != null) PauseMenu.Hint(title, GameText.Get("hint.thinking"));
        else if (hintPath == null) PauseMenu.Hint(title, GameText.Get(hintGaveUp ? "hint.toohard" : "hint.none"));
        else if (hintPath[0].dir < 0) PauseMenu.Hint(title, GameText.Get("hint.wait"));
        else
        {
            var m = hintPath[0];
            Mark(m.from, hintColor);
            Mark(m.from + Dirs[m.dir], hintColor);
            PauseMenu.Hint(title, GameText.Get("hint.move", ("dir", GameText.Get("dir." + DirNames[m.dir]))));
        }
    }

    void Rumble(float size, float time)
    {
        if (!GameSettings.ScreenShake) return;
        shakeSize = size;
        shakeLeft = time;
    }

    IEnumerator PlayTurn(List<(Ev ev, Vector2Int a, Vector2Int b, int info)> events)
    {
        animating = true;
        overlay.ClearAllTiles();
        foreach (var e in events) yield return Play(e.ev, e.a, e.b, e.info);
        SyncViews();
        if (result == 1) { Sfx.Play(Sound.Huh); yield return Shake(heroView.art.transform, 0.35f); } // the hero realizes they're stuck
        animating = false;
        Redraw();
        if (result != 0) ShowResult();
    }

    IEnumerator Play(Ev ev, Vector2Int a, Vector2Int b, int info)
    {
        switch (ev)
        {
            case Ev.CrateMoved when crateViews.Remove(a, out var crate):
                crateViews[b] = crate;
                Sfx.Play(Sound.Slide, info == 1 ? 0.7f : 1f); // the brute's push is lower
                var slide = Slide(crate, b, 0f);
                return info == 1 ? Run(slide) : slide; // a brute's push slides along with the brute's own step
            case Ev.CrateSmashed when crateViews.Remove(a, out var smashed):
                return Break(smashed);
            case Ev.Blast when crateViews.Remove(b, out var blasted):
                Sfx.Play(Sound.Zap);
                Rumble(0.25f, 0.3f);
                return Seq(Beam(Cell(a), Cell(b)), Break(blasted));
            case Ev.CrateStoned when crateViews.TryGetValue(a, out var stoned):
                Sfx.Play(Sound.Stone);
                Rumble(0.08f, 0.15f);
                return Seq(Squash(stoned.art.transform), Tint(stoned, stoneTint));
            case Ev.PotionByCrate or Ev.PotionByHero or Ev.PotionByFoe when potionViews.Remove(a, out var potion):
                Sfx.Play(ev == Ev.PotionByHero ? Sound.Gulp : ev == Ev.PotionByFoe ? Sound.Yoink : Sound.Pop);
                return Pop(potion);
            case Ev.HeroRecalled:
                Sfx.Play(Sound.Recall);
                return Seq(ScaleTo(heroView.art.transform, 0f, 0.15f), Teleport(heroView, b), ScaleTo(heroView.art.transform, 1f, 0.15f));
            case Ev.HeroMoved:
                Sfx.Play(Sound.Step, heroSteps++ % 2 == 0 ? 1f : 1.15f);
                return Slide(heroView, b, 0.18f);
            case Ev.FoeMoved:
                Sfx.Play(Sound.FoeStep);
                return Slide(foeViews[info], b, 0.14f);
        }
        return null;
    }

    // ---------- Animation pieces ----------

    IEnumerator Run(IEnumerator e) { StartCoroutine(e); yield break; }

    static IEnumerator Seq(params IEnumerator[] parts) { foreach (var p in parts) yield return p; }

    IEnumerator Slide(View v, Vector2Int to, float hop)
    {
        var from = v.root.position;
        var target = Cell(to);
        if (Mathf.Abs(target.x - from.x) > 0.01f) v.art.flipX = target.x < from.x;
        for (float k = 0f; k < 1f; k += Time.deltaTime / stepTime)
        {
            float e = k * k * (3f - 2f * k);
            v.root.position = Vector3.Lerp(from, target, e) + Vector3.up * (hop * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        v.root.position = target;
        if (hop > 0f) yield return Squash(v.art.transform);
    }

    static IEnumerator Squash(Transform t)
    {
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.08f)
        {
            float s = 0.12f * Mathf.Sin(k * Mathf.PI);
            t.localScale = new Vector3(1f + s, 1f - s, 1f);
            yield return null;
        }
    }

    // A crate breaks: a quick shake, then it shrinks away while splinters fly.
    IEnumerator Break(View v)
    {
        Sfx.Play(Sound.Smash);
        Rumble(0.12f, 0.2f);
        yield return Shake(v.art.transform, 0.1f);
        for (int i = 0; i < 6; i++) StartCoroutine(Splinter(v.root.position));
        yield return ScaleTo(v.art.transform, 0f, 0.1f);
        Destroy(v.root.gameObject);
    }

    IEnumerator Splinter(Vector3 at)
    {
        var r = new GameObject("Splinter").AddComponent<SpriteRenderer>();
        r.sprite = square;
        r.color = new Color(0.62f, 0.4f, 0.22f);
        r.sortingOrder = 6;
        r.transform.position = at;
        r.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
        var velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 4f), 0f);
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.45f)
        {
            velocity += Vector3.down * 12f * Time.deltaTime;
            r.transform.position += velocity * Time.deltaTime;
            r.transform.Rotate(0f, 0f, 600f * Time.deltaTime);
            r.color = new Color(r.color.r, r.color.g, r.color.b, 1f - k);
            yield return null;
        }
        Destroy(r.gameObject);
    }

    // The wizard's zap: a bright bar from him to the crate that fades out.
    IEnumerator Beam(Vector3 from, Vector3 to)
    {
        var r = new GameObject("Beam").AddComponent<SpriteRenderer>();
        r.sprite = square;
        r.sortingOrder = 6;
        r.transform.position = (from + to) / 2f;
        var gap = to - from;
        r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(gap.y, gap.x) * Mathf.Rad2Deg);
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.25f)
        {
            r.transform.localScale = new Vector3(gap.magnitude, 0.25f * (1f - k), 1f);
            r.color = new Color(blastColor.r, blastColor.g, blastColor.b, 1f - k * k);
            yield return null;
        }
        Destroy(r.gameObject);
    }

    // A potion gets used: it swells and fades.
    static IEnumerator Pop(View v)
    {
        var start = v.art.color;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.2f)
        {
            v.art.transform.localScale = Vector3.one * (1f + 0.8f * k);
            v.art.color = new Color(start.r, start.g, start.b, 1f - k);
            yield return null;
        }
        Destroy(v.root.gameObject);
    }

    static IEnumerator Tint(View v, Color color) { v.art.color = color; yield break; }

    IEnumerator Teleport(View v, Vector2Int c) { v.root.position = Cell(c); yield break; }

    static IEnumerator ScaleTo(Transform t, float to, float time)
    {
        float from = t.localScale.x;
        for (float k = 0f; k < 1f; k += Time.deltaTime / time)
        {
            t.localScale = Vector3.one * Mathf.Lerp(from, to, k);
            yield return null;
        }
        t.localScale = Vector3.one * to;
    }

    static IEnumerator Shake(Transform t, float time)
    {
        for (float k = 0f; k < time; k += Time.deltaTime)
        {
            t.localPosition = new Vector3(Mathf.Sin(k * 90f) * 0.06f, t.localPosition.y, 0f);
            yield return null;
        }
        t.localPosition = new Vector3(0f, t.localPosition.y, 0f);
    }

    // After the show, put every sprite exactly where the rules say, in case an animation and the rules disagree.
    void SyncViews()
    {
        Sync(crateViews, d.crates, c => MakeView(crateTile.sprite, crateTile.color, c, 3));
        Sync(potionViews, new HashSet<Vector2Int>(d.potions.Keys), c => MakeView(potionTiles[(int)d.potions[c]].sprite, potionTiles[(int)d.potions[c]].color, c, 2));
        foreach (var (c, v) in crateViews)
        {
            v.root.position = Cell(c);
            v.art.color = d.stones.Contains(c) ? stoneTint : crateTile.color;
        }
        for (int i = 0; i < foeViews.Count; i++)
        {
            var (kind, cell, dir) = d.foes[i];
            foeViews[i].root.position = Cell(cell);
            if (kind is Foe.Dwarf or Foe.Brute && dir.x != 0) foeViews[i].art.flipX = dir.x < 0;
        }
        heroView.root.position = Cell(d.hero);
    }

    static void Sync(Dictionary<Vector2Int, View> views, HashSet<Vector2Int> truth, System.Func<Vector2Int, View> make)
    {
        foreach (var c in new List<Vector2Int>(views.Keys))
            if (!truth.Contains(c)) { Destroy(views[c].root.gameObject); views.Remove(c); }
        foreach (var c in truth)
            if (!views.ContainsKey(c)) views[c] = make(c);
    }

    void ShowResult()
    {
        string level = SceneManager.GetActiveScene().name;
        string next = Progress.Next(level);
        var lines = new List<string> { GameText.Get(result == -1 ? "result.lost" : newBest ? "result.won.best" : "result.won", ("turns", GameText.Turns(Turns))) };
        int stars = 0;
        if (result == 1)
        {
            lines.Add(GameText.Get(guiding ? "result.won.tutorial" : next == null ? "result.won.last" : "result.won.normal"));
            stars = Progress.StarsFor(level, Turns, hinted);
            if (hinted) lines.Add(GameText.Get("result.hinted"));
            else if (stars < 3) lines.Add(GameText.Get("result.par", ("turns", GameText.Turns(Progress.ParFor(level)))));
            lines.Add(GameText.Get(newBestTime ? "result.time.best" : "result.time",
                ("time", Progress.FormatTime(playTime)), ("best", Progress.FormatTime(Progress.BestTime(level)))));
        }
        Sfx.Play(result == 1 ? Sound.Win : Sound.Lose);
        PauseMenu.ShowResult(result == 1, string.Join("\n", lines.FindAll(l => l != "")), next, stars);
    }

    void Redraw()
    {
        overlay.ClearAllTiles();
        var path = result == 0 ? d.HeroRoute() : null;
        if (showHeroPath)
            foreach (var c in path ?? new()) Mark(c, pathColor);
        if (selected is { } s)
        {
            Mark(s, selectColor);
            foreach (var dir in Dirs)
                if (d.CrateCanEnter(s + dir)) Mark(s + dir, moveColor);
        }
        Guide();
    }

    // The hint card: tutorial steps on a first play of the tutorial, otherwise a card for each piece the player hasn't
    // met yet, then the level intro until the first move.
    void Guide()
    {
        if (result != 0) { PauseMenu.Hint(null, null); return; }
        if (hintShown) { HintCard(); return; }
        if (!guiding)
        {
            foreach (var p in d.potions.Values)
                if (NewPieceCard(Progress.PotionKeys[(int)p], potionTiles[(int)p], c => d.potions.TryGetValue(c, out var q) && q == p)) return;
            foreach (var f in d.foes)
                if (NewPieceCard(Progress.FoeKeys[(int)f.kind], foeTiles[(int)f.kind], c => d.foes.Exists(g => g.cell == c && g.kind == f.kind))) return;
            int n = System.Array.IndexOf(Progress.Levels, SceneManager.GetActiveScene().name) + 1;
            PauseMenu.Hint(GameText.Get($"level{n}.title"), Turns == 0 ? GameText.Get($"level{n}.intro") : null);
            return;
        }
        switch (guidePage)
        {
            case 0:
                Mark(d.hero, hintColor);
                Mark(d.exit, hintColor);
                PauseMenu.Hint(GameText.Get("tutorial.1.title"), GameText.Get("tutorial.1.body"), GameText.Get("tutorial.1.button"), () => { guidePage = 1; Redraw(); });
                return;
            case 1:
                PauseMenu.Hint(GameText.Get("tutorial.2.title"), GameText.Get("tutorial.2.body"), GameText.Get("tutorial.2.button"), () => { guidePage = 2; Redraw(); });
                return;
        }
        if (selected != null)
            PauseMenu.Hint(GameText.Get("tutorial.slide.title"), GameText.Get("tutorial.slide.body"));
        else if (Turns == 0)
        {
            foreach (var c in d.crates) Mark(c, hintColor);
            PauseMenu.Hint(GameText.Get("tutorial.grab.title"), GameText.Get("tutorial.grab.body"));
        }
        else
        {
            foreach (var dir in Dirs)
                if (d.Walkable(d.exit + dir)) Mark(d.exit + dir, hintColor);
            PauseMenu.Hint(GameText.Get("tutorial.block.title"), GameText.Get("tutorial.block.body"));
        }
    }

    // Shows the card for a piece met for the first time, with its cells marked, until the player presses the button.
    bool NewPieceCard(string piece, Tile tile, System.Predicate<Vector2Int> isPiece)
    {
        if (Progress.Seen(piece)) return false;
        foreach (var c in d.floor) if (isPiece(c)) Mark(c, hintColor);
        PauseMenu.Hint(GameText.Get("newpiece.title", ("name", GameText.Get($"howto.{piece}.title"))), GameText.Get($"howto.{piece}.body"),
            GameText.Get("newpiece.button"), () => { Progress.MarkSeen(piece); Redraw(); }, tile.sprite, tile.color);
        return true;
    }

    void Mark(Vector2Int c, Color color)
    {
        overlay.SetTile((Vector3Int)c, markerTile);
        overlay.SetColor((Vector3Int)c, color);
    }
}
