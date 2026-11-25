using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Reads the level from the Tilemap, runs the villain/hero turn loop, and draws the board, selection and hints.
// Runs after PauseMenu so the HUD exists when Start shows the first hint.
[DefaultExecutionOrder(100)]
public class DungeonGame : MonoBehaviour
{
    public Tilemap map, overlay;
    public TileBase floorTile, crateTile, exitTile, heroStartTile, potionTile, dwarfTile, markerTile;
    public Transform hero;
    public Sprite dwarfSprite;
    public Color pathColor = new(1f, 0.85f, 0.2f, 0.45f), selectColor = new(0.3f, 0.8f, 1f, 0.6f),
        moveColor = new(0.3f, 0.8f, 1f, 0.3f), hintColor = new(0.85f, 0.12f, 0.1f, 0.55f);
    public float glideSpeed = 8f; // cells per second
    public bool showHeroPath; // off: the hero's route and step count are hidden, so the player has to work them out
    [Tooltip("How far in the player can zoom, as a fraction of the size that fits the whole level.")]
    public float minZoom = 0.4f;
    [Tooltip("Card shown at the start of the level, until the first move.")]
    public string introTitle;
    [TextArea(2, 6)] public string intro;
    [Tooltip("Step-by-step walkthrough instead of the intro card, until this level is beaten once.")]
    public bool tutorial;

    public int Turns { get; private set; } // villain turns taken so far (moves and waits)

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    Dungeon d;
    Vector2Int? selected;
    int result; // +1 villain won, -1 hero escaped, 0 playing
    int guidePage; // tutorial: 0 and 1 are the intro pages, 2 is hands-on
    bool newBest, guiding;
    Vector3 heroTarget;
    float fitSize; // camera size that shows the whole level
    readonly List<SpriteRenderer> dwarfViews = new();

    void Start()
    {
        d = ReadLevel();
        guiding = tutorial && !Progress.IsComplete(SceneManager.GetActiveScene().name);
        // Start markers are for painting only: the game draws the hero and dwarves as sprites.
        map.SetTile((Vector3Int)d.hero, floorTile);
        foreach (var (c, _) in d.dwarves)
        {
            map.SetTile((Vector3Int)c, floorTile);
            var r = new GameObject("Dwarf").AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform);
            r.transform.position = map.GetCellCenterWorld((Vector3Int)c);
            r.sprite = dwarfSprite;
            r.sortingOrder = 2;
            dwarfViews.Add(r);
        }
        AddFloorUnderlay();

        var cam = Camera.main;
        var b = map.localBounds;
        cam.transform.position = new Vector3(b.center.x, b.center.y, cam.transform.position.z);
        cam.orthographicSize = fitSize = Mathf.Max(b.extents.y, b.extents.x / cam.aspect) + 1f;
        Redraw();
        hero.position = heroTarget; // start in place, no glide
    }

    // Crates, potions and the exit have see-through sprites, so a floor layer goes underneath every walkable cell.
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
        var dwarves = new List<Vector2Int>();
        int heroes = 0, exits = 0;
        map.CompressBounds();
        foreach (var p in map.cellBounds.allPositionsWithin)
        {
            var t = map.GetTile(p);
            var c = (Vector2Int)p;
            if (t == floorTile || t == crateTile || t == exitTile || t == heroStartTile || t == potionTile || t == dwarfTile) level.floor.Add(c);
            if (t == crateTile) level.crates.Add(c);
            if (t == potionTile) level.potions.Add(c);
            if (t == dwarfTile) dwarves.Add(c);
            if (t == exitTile) { level.exit = c; exits++; }
            if (t == heroStartTile) { level.hero = c; heroes++; }
        }
        // Anything else (Wall tiles or empty) counts as wall.
        dwarves.ForEach(level.AddDwarf);
        if (heroes != 1 || exits != 1)
            Debug.LogError($"Level needs exactly 1 HeroStart and 1 Exit tile, found {heroes} and {exits}.", this);
        return level;
    }

    void Update()
    {
        hero.position = Vector3.MoveTowards(hero.position, heroTarget, glideSpeed * Time.deltaTime);
        for (int i = 0; i < dwarfViews.Count; i++)
        {
            var t = dwarfViews[i].transform;
            t.position = Vector3.MoveTowards(t.position, map.GetCellCenterWorld((Vector3Int)d.dwarves[i].cell), glideSpeed * Time.deltaTime);
            dwarfViews[i].flipX = d.dwarves[i].dir.x < 0;
        }
        overlay.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f)); // markers pulse

        if (PauseMenu.Paused) return; // Esc (Controls.Menu) is handled by the pause screen
        MoveCamera();
        if (Controls.Restart.WasPressedThisFrame()) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        if (result != 0) return;

        if (Controls.Wait.WasPressedThisFrame()) EndVillainTurn(null);

        var mouse = Mouse.current;
        if (mouse != null && Controls.Select.WasPressedThisFrame() && !PauseMenu.IsOverUI(mouse.position.ReadValue()))
            Click((Vector2Int)map.WorldToCell(Camera.main.ScreenToWorldPoint(mouse.position.ReadValue())));
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

    void Click(Vector2Int cell)
    {
        if (result != 0) return;
        if (d.crates.Contains(cell))
        {
            selected = cell == selected ? null : cell;
            guidePage = 2; // grabbing a crate skips the tutorial's intro pages
        }
        else if (selected is { } from && d.MoveCrate(from, cell))
        {
            selected = null;
            EndVillainTurn(cell);
            return;
        }
        else selected = null; // clicking empty floor drops the selection
        Redraw();
    }

    // movedTo: where a crate just landed, or null for a wait.
    void EndVillainTurn(Vector2Int? movedTo)
    {
        Turns++;
        guidePage = 2;
        result = d.Resolve(movedTo);
        string level = SceneManager.GetActiveScene().name;
        if (result == 1) newBest = Progress.Complete(level, Turns);
        Redraw();
        if (result == 0) return;

        string turns = $"{Turns} turn{(Turns == 1 ? "" : "s")}";
        string next = Progress.Next(level);
        string body = result == -1 ? $"Press {Controls.KeyName(Controls.Restart)} or Try Again to set a new trap."
            : $"The hero is walled in after {turns}{(newBest ? ", a new best" : "")}."
              + (guiding ? "\nThat's the whole game: every dungeon is a new puzzle." : next == null ? "\nEvery dungeon is yours. The chosen one never stood a chance." : "");
        PauseMenu.ShowResult(result == 1, body, next);
    }

    void Redraw()
    {
        heroTarget = map.GetCellCenterWorld((Vector3Int)d.hero);
        // Sync the board: crates move, potions get used up, dwarves smash crates.
        foreach (var c in d.floor)
            if (c != d.exit) map.SetTile((Vector3Int)c, d.crates.Contains(c) ? crateTile : d.potions.Contains(c) ? potionTile : floorTile);

        overlay.ClearAllTiles();
        var path = result == 0 ? d.PathToExit() : null;
        if (showHeroPath)
            foreach (var c in path ?? new()) Mark(c, pathColor);
        if (selected is { } s)
        {
            Mark(s, selectColor);
            foreach (var dir in Dirs)
                if (d.Clone().MoveCrate(s, s + dir)) Mark(s + dir, moveColor);
        }
        Guide();
    }

    // The hint card: tutorial steps on a first play of the tutorial, otherwise the level intro until the first move.
    void Guide()
    {
        if (result != 0) { PauseMenu.Hint(null, null); return; }
        if (!guiding)
        {
            PauseMenu.Hint(introTitle, Turns == 0 ? intro : null);
            return;
        }
        string select = Controls.SelectVerb;
        switch (guidePage)
        {
            case 0:
                Mark(d.hero, hintColor);
                Mark(d.exit, hintColor);
                PauseMenu.Hint("You are the villain", "That knight is the hero, and the door is the way out.\nYour job: make sure they never reach it.",
                    "Next", () => { guidePage = 1; Redraw(); });
                return;
            case 1:
                PauseMenu.Hint("Taking turns", "Each turn you make one move. Then the hero takes one step along the shortest way to the door.",
                    "Got it", () => { guidePage = 2; Redraw(); });
                return;
        }
        if (selected != null)
            PauseMenu.Hint("Slide it", $"{select} a glowing square next to the crate to slide it there.");
        else if (Turns == 0)
        {
            foreach (var c in d.crates) Mark(c, hintColor);
            PauseMenu.Hint("Your move", $"{select} the crate to grab it.");
        }
        else
        {
            foreach (var dir in Dirs)
                if (d.Walkable(d.exit + dir)) Mark(d.exit + dir, hintColor);
            PauseMenu.Hint("Block the Way Out", "The hero took a step. Keep sliding the crate and park it on the glowing square before the hero gets there.\n" +
                $"Press {Controls.KeyName(Controls.Wait)} to wait a turn, {Controls.KeyName(Controls.Restart)} to start over.");
        }
    }

    void Mark(Vector2Int c, Color color)
    {
        overlay.SetTile((Vector3Int)c, markerTile);
        overlay.SetColor((Vector3Int)c, color);
    }
}
