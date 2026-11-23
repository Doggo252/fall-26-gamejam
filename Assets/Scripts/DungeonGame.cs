using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Reads the level from the Tilemap, runs the villain/hero turn loop, and draws path, selection and status.
public class DungeonGame : MonoBehaviour
{
    public Tilemap map, overlay;
    public TileBase floorTile, crateTile, exitTile, heroStartTile, markerTile;
    public Transform hero;
    public Color pathColor = new(1f, 0.85f, 0.2f, 0.45f), selectColor = new(0.3f, 0.8f, 1f, 0.6f);
    public float glideSpeed = 8f; // cells per second
    public bool showHeroPath; // off: the hero's route and step count are hidden, so the player has to work them out

    Dungeon d;
    Vector2Int? selected;
    int result; // +1 villain won, -1 hero escaped, 0 playing
    int stepsLeft;
    Vector3 heroTarget;

    void Start()
    {
        d = ReadLevel();
        map.SetTile((Vector3Int)d.hero, floorTile); // the start marker is for painting only
        var cam = Camera.main;
        var b = map.localBounds;
        cam.transform.position = new Vector3(b.center.x, b.center.y, cam.transform.position.z);
        cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / cam.aspect) + 1f;
        Redraw();
        hero.position = heroTarget; // start in place, no glide
    }

    // Turns the painted Tilemap into a Dungeon. Public so the editor solver can read the open level too.
    public Dungeon ReadLevel()
    {
        var level = new Dungeon();
        int heroes = 0, exits = 0;
        map.CompressBounds();
        foreach (var p in map.cellBounds.allPositionsWithin)
        {
            var t = map.GetTile(p);
            var c = (Vector2Int)p;
            if (t == floorTile || t == crateTile || t == exitTile || t == heroStartTile) level.floor.Add(c);
            if (t == crateTile) level.crates.Add(c);
            if (t == exitTile) { level.exit = c; exits++; }
            if (t == heroStartTile) { level.hero = c; heroes++; }
        }
        // Anything else (Wall tile or empty) counts as wall.
        if (heroes != 1 || exits != 1)
            Debug.LogError($"Level needs exactly 1 HeroStart and 1 Exit tile, found {heroes} and {exits}.", this);
        return level;
    }

    void Update()
    {
        hero.position = Vector3.MoveTowards(hero.position, heroTarget, glideSpeed * Time.deltaTime);
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        if (result != 0) return;

        if (kb != null && kb.spaceKey.wasPressedThisFrame) EndVillainTurn();

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            Click((Vector2Int)map.WorldToCell(Camera.main.ScreenToWorldPoint(mouse.position.ReadValue())));
    }

    void Click(Vector2Int cell)
    {
        if (result != 0) return;
        if (d.crates.Contains(cell)) selected = cell == selected ? null : cell;
        else if (selected is { } from && d.MoveCrate(from, cell))
        {
            map.SetTile((Vector3Int)from, floorTile);
            map.SetTile((Vector3Int)cell, crateTile);
            selected = null;
            EndVillainTurn();
            return;
        }
        Redraw();
    }

    void EndVillainTurn()
    {
        result = d.HeroStep();
        Redraw();
    }

    void Redraw()
    {
        heroTarget = map.GetCellCenterWorld((Vector3Int)d.hero);
        overlay.ClearAllTiles();
        var path = result == 0 ? d.PathToExit() : null;
        stepsLeft = path?.Count ?? 0;
        if (showHeroPath)
            foreach (var c in path ?? new()) Mark(c, pathColor);
        if (selected is { } s) Mark(s, selectColor);
    }

    void Mark(Vector2Int c, Color color)
    {
        overlay.SetTile((Vector3Int)c, markerTile);
        overlay.SetColor((Vector3Int)c, color);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        string msg = result switch
        {
            1 => "The hero is trapped. Villainy prevails!   [R] restart",
            -1 => "The hero escaped...   [R] restart",
            _ => "Click a crate, then a cell next to it to slide it.   [Space] wait   [R] restart",
        };
        GUI.Label(new Rect(16, 10, Screen.width - 32, 40), msg, style);
        if (result == 0 && showHeroPath)
            GUI.Label(new Rect(16, 44, Screen.width - 32, 40), $"The hero escapes in {stepsLeft} step{(stepsLeft == 1 ? "" : "s")}", style);
    }
}
