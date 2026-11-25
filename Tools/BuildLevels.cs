using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Gives the level tiles Kenney art, repaints the tutorial (Main Scene) and creates Level 2 to Level 6
// as copies of it with their own layouts and intro cards. Run with:
// unity command script run --file Tools/BuildLevels.cs --entry BuildLevels.Run
// WARNING: overwrites the map in every level scene listed below. Tools/BuildDay1.cs would undo the art.
public static class BuildLevels
{
    const string Kenney = "Assets/Art/Kenney/TinyDungeon";
    const string MainScene = "Assets/Scenes/Main Scene.unity";

    // '#' wall, '.' floor, 'C' crate, 'H' hero, 'E' exit, 'P' haste potion, 'D' dwarf. Each checked by DungeonSolver.
    static readonly (string scene, string title, string intro, string[] rows)[] Levels =
    {
        ("Level 2", "Choose Your Crate", "Two crates this time, and only one of them can get there in time. Pick wisely.", new[] {
            "###########",
            "#.....#.C.#",
            "#.#...#.#.#",
            "#.CH#.#.#.#",
            "#.###.###.#",
            "#...#.#...#",
            "#.#.#.#.#.#",
            "#.#.....#.#",
            "####E######" }),
        ("Level 3", "Haste Potion", "Whoever reaches a potion first moves twice.\nSlide a crate onto it and you move again before the hero steps. If the hero drinks it, the hero takes two steps.", new[] {
            "###########",
            "#.......H.#",
            "###.#.###.#",
            "#..C#.....#",
            "#.#.##..#.#",
            "#.#..P....#",
            "#.#.###.#.#",
            "#C#.......#",
            "########E##" }),
        ("Level 4", "Race for It", "The hero is one step from a potion. Get there first.", new[] {
            "#############",
            "#........C..#",
            "#.#.###H.##.#",
            "#.......PC#.#",
            "#.#.#.#.###.#",
            "#...#.#.....#",
            "#.###.###..##",
            "#C#.........#",
            "######E######" }),
        ("Level 5", "The Dwarf", "The hero brought a friend. The dwarf paces its hall, turns around at walls and smashes any crate in its way. Crates can't be pushed onto it.", new[] {
            "###########",
            "#H.....C..#",
            "#.#.###.#.#",
            "#.#.......#",
            "#.C..D###.#",
            "#...#.#...#",
            "#.###...###",
            "#.....#...#",
            "#####E#####" }),
        ("Level 6", "One Last Escape", "Three crates, a potion and a dwarf stand between the hero and freedom. Show them who the real chosen one is.", new[] {
            "#############",
            "#.....#.....#",
            "#C###.#.#H..#",
            "#...#...#.#.#",
            "##..#..##.#.#",
            "#..C#...D.C.#",
            "#.###.###.###",
            "#.P...#...#.#",
            "#.#.###.###.#",
            "#.#.........#",
            "######E######" }),
    };

    public static string Run()
    {
        // Open first: reopening a scene unloads assets nothing references, which would destroy tiles loaded below.
        var scene = EditorSceneManager.OpenScene(MainScene);
        var game = Object.FindAnyObjectByType<DungeonGame>();
        var before = game.ReadLevel();
        var tutorial = Dungeon.Parse(DungeonSelfTest.Level1);
        string note = before.floor.SetEquals(tutorial.floor) && before.crates.SetEquals(tutorial.crates) && before.hero == tutorial.hero
            ? "tutorial layout unchanged" : "Main Scene had a different layout; repainted with DungeonSelfTest.Level1";

        Sprite Art(int i) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Kenney}/tile_{i:0000}.png");
        Tile MakeTile(string name, int art)
        {
            var path = $"Assets/Tiles/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, path); }
            t.sprite = Art(art);
            t.color = Color.white;
            t.flags = TileFlags.LockColor;
            EditorUtility.SetDirty(t);
            return t;
        }
        // Tile numbers refer to Kenney's Tiny Dungeon sheet (row * 12 + column).
        var tiles = new Tiles
        {
            floor = MakeTile("Floor", 48), wall = MakeTile("Wall", 0), wallFront = MakeTile("WallFront", 40),
            crate = MakeTile("Crate", 63), exit = MakeTile("Exit", 45), heroStart = MakeTile("HeroStart", 97),
            potion = MakeTile("Potion", 116), dwarf = MakeTile("Dwarf", 87),
        };
        AssetDatabase.SaveAssets();

        // Tile palette for painting levels by hand (Window > 2D > Tile Palette).
        const string palettePath = "Assets/Palettes/Dungeon.prefab";
        var palette = PrefabUtility.LoadPrefabContents(palettePath);
        var paletteMap = palette.GetComponentInChildren<Tilemap>();
        paletteMap.ClearAllTiles();
        Tile[] paletteTiles = { tiles.wall, tiles.wallFront, tiles.floor, tiles.crate, tiles.exit, tiles.heroStart, tiles.potion, tiles.dwarf };
        for (int i = 0; i < paletteTiles.Length; i++) paletteMap.SetTile(new Vector3Int(i, 0, 0), paletteTiles[i]);
        PrefabUtility.SaveAsPrefabAsset(palette, palettePath);
        PrefabUtility.UnloadPrefabContents(palette);

        Setup(game, tiles, DungeonSelfTest.Level1, "", "", tutorial: true);
        EditorSceneManager.SaveScene(scene);

        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new("Assets/Scenes/Main Menu.unity", true),
            new(MainScene, true),
        };
        foreach (var (name, title, intro, rows) in Levels)
        {
            string path = $"Assets/Scenes/{name}.unity";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(MainScene, path);
            var s = EditorSceneManager.OpenScene(path);
            Setup(Object.FindAnyObjectByType<DungeonGame>(), tiles, rows, title, intro, tutorial: false);
            EditorSceneManager.SaveScene(s);
            buildScenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
        EditorSceneManager.OpenScene(MainScene);
        return $"{note}; built {Levels.Length} levels; build settings has {buildScenes.Count} scenes";
    }

    class Tiles { public Tile floor, wall, wallFront, crate, exit, heroStart, potion, dwarf; }

    static void Setup(DungeonGame game, Tiles t, string[] rows, string title, string intro, bool tutorial)
    {
        game.floorTile = t.floor; game.crateTile = t.crate; game.exitTile = t.exit; game.heroStartTile = t.heroStart;
        game.potionTile = t.potion; game.dwarfTile = t.dwarf; game.dwarfSprite = t.dwarf.sprite;
        game.introTitle = title; game.intro = intro; game.tutorial = tutorial;
        var heroSprite = game.hero.GetComponent<SpriteRenderer>();
        heroSprite.sprite = t.heroStart.sprite;
        heroSprite.color = Color.white;
        game.hero.localScale = Vector3.one;
        Camera.main.backgroundColor = new Color(0.03f, 0.02f, 0.04f);

        var d = Dungeon.Parse(rows);
        game.map.ClearAllTiles();
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[0].Length; x++)
            {
                var c = new Vector2Int(x, y);
                TileBase tile = !d.floor.Contains(c) ? (d.floor.Contains(c + Vector2Int.down) ? t.wallFront : t.wall)
                    : c == d.hero ? t.heroStart : c == d.exit ? t.exit : d.crates.Contains(c) ? t.crate
                    : d.potions.Contains(c) ? t.potion : d.HasDwarf(c) ? t.dwarf : t.floor;
                game.map.SetTile((Vector3Int)c, tile);
            }
        game.hero.position = game.map.GetCellCenterWorld((Vector3Int)d.hero); // just for the Scene view; the game places the hero
        EditorUtility.SetDirty(game);
        EditorUtility.SetDirty(game.hero.gameObject);
    }
}
