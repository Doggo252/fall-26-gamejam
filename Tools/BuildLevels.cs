using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Gives the level tiles Kenney art, repaints the tutorial (Main Scene) and creates Level 2 to Level 16
// as copies of it with their own layouts. Level names and intro cards are in Assets/Resources/Text.txt. Run with:
// unity command script run --file Tools/BuildLevels.cs --entry BuildLevels.Run
// WARNING: overwrites the map in every level scene listed below. Tools/BuildDay1.cs would undo the art.
public static class BuildLevels
{
    const string Kenney = "Assets/Art/Kenney/TinyDungeon";
    const string MainScene = "Assets/Scenes/Main Scene.unity";

    // Same characters as Dungeon.Parse. Each layout is checked with DungeonSolver before it ships.
    static readonly (string scene, string[] rows)[] Levels =
    {
        ("Level 2", new[] {
            "#########",
            "#...#.C.#",
            "#.#.###.#",
            "#.#..H..#",
            "#.#.#.#.#",
            "#.#.C...#",
            "#+#.#.###",
            "#++.....#",
            "#E#######" }),
        ("Level 3", new[] {
            "###########",
            "#C....#...#",
            "###H###.#.#",
            "#.....C.#C#",
            "#.###.#.#.#",
            "#.........#",
            "#.#######.#",
            "#.....+++.#",
            "#######E###" }),
        ("Level 4", new[] {
            "###########",
            "#.....#...#",
            "#H#.#.###.#",
            "#.#...#...#",
            "###.#.#.###",
            "#...#.PC.C#",
            "#.###+###.#",
            "#...+++..C#",
            "#####E#####" }),
        ("Level 5", new[] {
            "###########",
            "#.#H..C.#C#",
            "#.#.###.#.#",
            "#...#...#.#",
            "#Z###.#.#.#",
            "#.#...#...#",
            "#.#.###.#.#",
            "#...+++.C.#",
            "#####E#####" }),
        ("Level 6", new[] {
            "###########",
            "#.........#",
            "#.#.#D#C#.#",
            "#C#.#.H.#.#",
            "#.#.#.#####",
            "#...#...C.#",
            "#.#.#.###+#",
            "#.#.....++#",
            "#########E#" }),
        ("Level 7", new[] {
            "#############",
            "#.......#CC.#",
            "#.#H#.#.#.#.#",
            "#.#...#.#...#",
            "#.###C#####.#",
            "#R..#.....#.#",
            "###+###.#.#.#",
            "#.+++.......#",
            "###E#########" }),
        ("Level 8", new[] {
            "#############",
            "#..C#...H.B.#",
            "#.###.#.#C#.#",
            "#.....#.#...#",
            "#.#.###.###C#",
            "#.........#.#",
            "#####.###.#.#",
            "#.....+++...#",
            "#######E#####" }),
        ("Level 9", new[] {
            "#############",
            "#.D.SCH.....#",
            "#C#####.###.#",
            "#.....C.....#",
            "#.###.#####.#",
            "#.#...#...#.#",
            "#.###.#.#+#.#",
            "#.......#++.#",
            "#########E###" }),
        ("Level 10", new[] {
            "#############",
            "#...........#",
            "#.#.###C#.#C#",
            "#.#.....#...#",
            "###.#C###.#.#",
            "#..H#.W...#.#",
            "#.###.#+###C#",
            "#.....#++...#",
            "#######E#####" }),
        ("Level 11", new[] {
            "#############",
            "#.......GH..#",
            "###.###.###C#",
            "#...#.......#",
            "#.#.#.#.#####",
            "#.#.........#",
            "#.#C#.#.#.###",
            "#...#...#...#",
            "#.###.#.###.#",
            "#.+++....C..#",
            "###E#########" }),
        ("Level 12", new[] {
            "#############",
            "#..C........#",
            "###.#####.#.#",
            "#C..#....H#.#",
            "#####T#####.#",
            "#...........#",
            "#.###.#.#####",
            "#C#.#.#.#...#",
            "#.#.#.#.###+#",
            "#...#..P..++#",
            "###########E#" }),
        ("Level 13", new[] {
            "#############",
            "#.#.H...#.#.#",
            "#.#.###.#.#.#",
            "#.#..A#.#C..#",
            "#.#.#.#.###.#",
            "#...#.#.#...#",
            "#.###.#.#.#.#",
            "#..C#.#...#.#",
            "#.#C#.#####.#",
            "#.#C..+++...#",
            "#######E#####" }),
        ("Level 14", new[] {
            "###########",
            "#...HZ..C.#",
            "#.###.###.#",
            "#.#...#D..#",
            "#.#.#.#.###",
            "#...#.C..C#",
            "#.###.###.#",
            "#.+++.#...#",
            "###E#######" }),
        ("Level 15", new[] {
            "#############",
            "#.....R.....#",
            "#.###.#.#.#.#",
            "#C#.#H#...#.#",
            "#B#.#####.#.#",
            "#.#.......#.#",
            "#C#####C###.#",
            "#.......+++.#",
            "#########E###" }),
        ("Level 16", new[] {
            "#############",
            "#.....C..H.Z#",
            "#.#.###.###.#",
            "#C#.........#",
            "###.#.###.#.#",
            "#...#.#...#.#",
            "#.###.#.###.#",
            "#...#..C#C..#",
            "###.#######.#",
            "#.....+++.A.#",
            "#######E#####" }),
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
        Tile MakeTile(string name, int art, Color? tint = null)
        {
            var path = $"Assets/Tiles/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, path); }
            t.sprite = Art(art);
            t.color = tint ?? Color.white;
            t.flags = TileFlags.LockColor;
            EditorUtility.SetDirty(t);
            return t;
        }
        // Tile numbers refer to Kenney's Tiny Dungeon sheet (row * 12 + column).
        var tiles = new Tiles
        {
            floor = MakeTile("Floor", 48), wall = MakeTile("Wall", 0), wallFront = MakeTile("WallFront", 40),
            crate = MakeTile("Crate", 63), exit = MakeTile("Exit", 45), heroStart = MakeTile("HeroStart", 97),
            ward = MakeTile("Ward", 42),
            // Same order as the Potion and Foe enums. Recall reuses the stone flask, tinted violet.
            potions = new[] { MakeTile("Potion", 116), MakeTile("PotionSleep", 114), MakeTile("PotionRecall", 113, new Color(0.75f, 0.5f, 1f)),
                MakeTile("PotionRage", 115), MakeTile("PotionStone", 113) },
            foes = new[] { MakeTile("Dwarf", 87), MakeTile("Brute", 86), MakeTile("Wizard", 84), MakeTile("Thief", 111), MakeTile("Hunter", 88) },
        };
        AssetDatabase.SaveAssets();

        // Tile palette for painting levels by hand (Window > 2D > Tile Palette).
        const string palettePath = "Assets/Palettes/Dungeon.prefab";
        var palette = PrefabUtility.LoadPrefabContents(palettePath);
        var paletteMap = palette.GetComponentInChildren<Tilemap>();
        paletteMap.ClearAllTiles();
        var paletteTiles = new System.Collections.Generic.List<Tile> { tiles.wall, tiles.wallFront, tiles.floor, tiles.ward, tiles.crate, tiles.exit, tiles.heroStart };
        paletteTiles.AddRange(tiles.potions);
        paletteTiles.AddRange(tiles.foes);
        for (int i = 0; i < paletteTiles.Count; i++) paletteMap.SetTile(new Vector3Int(i, 0, 0), paletteTiles[i]);
        PrefabUtility.SaveAsPrefabAsset(palette, palettePath);
        PrefabUtility.UnloadPrefabContents(palette);

        Setup(game, tiles, DungeonSelfTest.Level1, tutorial: true);
        EditorSceneManager.SaveScene(scene);

        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new("Assets/Scenes/Main Menu.unity", true),
            new(MainScene, true),
        };
        foreach (var (name, rows) in Levels)
        {
            string path = $"Assets/Scenes/{name}.unity";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(MainScene, path);
            var s = EditorSceneManager.OpenScene(path);
            Setup(Object.FindAnyObjectByType<DungeonGame>(), tiles, rows, tutorial: false);
            EditorSceneManager.SaveScene(s);
            buildScenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
        EditorSceneManager.OpenScene(MainScene);
        return $"{note}; built {Levels.Length} levels; build settings has {buildScenes.Count} scenes";
    }

    class Tiles { public Tile floor, wall, wallFront, ward, crate, exit, heroStart; public Tile[] potions, foes; }

    static void Setup(DungeonGame game, Tiles t, string[] rows, bool tutorial)
    {
        game.floorTile = t.floor; game.crateTile = t.crate; game.exitTile = t.exit; game.heroStartTile = t.heroStart;
        game.wardTile = t.ward; game.potionTiles = t.potions; game.foeTiles = t.foes;
        game.tutorial = tutorial;
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
                    : d.potions.TryGetValue(c, out var p) ? t.potions[(int)p] : d.wards.Contains(c) ? t.ward
                    : d.HasFoe(c) ? t.foes[(int)d.foes.Find(f => f.cell == c).kind] : t.floor;
                game.map.SetTile((Vector3Int)c, tile);
            }
        game.hero.position = game.map.GetCellCenterWorld((Vector3Int)d.hero); // just for the Scene view; the game places the hero
        EditorUtility.SetDirty(game);
        EditorUtility.SetDirty(game.hero.gameObject);
    }
}
