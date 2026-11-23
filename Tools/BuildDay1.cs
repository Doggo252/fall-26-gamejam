using System.IO;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Scene and asset setup for the day 1 prototype. Run with:
// unity command script run --file Tools/BuildDay1.cs --entry BuildDay1.Run
// WARNING: rebuilds the "Dungeon" object from scratch, which wipes any level painted in Main Scene.
// The tiles and the tile palette are updated in place and are safe to rebuild.
public static class BuildDay1
{
    const string ScenePath = "Assets/Scenes/Main Scene.unity";

    public static string Run()
    {
        // Open first: reopening a scene unloads assets nothing references, which would destroy tiles loaded below.
        var scene = EditorSceneManager.OpenScene(ScenePath);

        // White square sprite, 1 world unit.
        Directory.CreateDirectory("Assets/Art");
        const string spritePath = "Assets/Art/Square.png";
        var tex = new Texture2D(16, 16);
        var white = new Color32[256];
        System.Array.Fill(white, new Color32(255, 255, 255, 255));
        tex.SetPixels32(white);
        File.WriteAllBytes(spritePath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(spritePath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Point;
        imp.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        Directory.CreateDirectory("Assets/Tiles");
        Tile MakeTile(string name, Color color, TileFlags flags = TileFlags.LockColor)
        {
            var path = $"Assets/Tiles/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, path); }
            t.sprite = sprite; t.color = color; t.flags = flags;
            EditorUtility.SetDirty(t);
            return t;
        }
        var floor = MakeTile("Floor", new Color(0.16f, 0.14f, 0.19f));
        var wall = MakeTile("Wall", new Color(0.45f, 0.38f, 0.33f));
        var crate = MakeTile("Crate", new Color(0.8f, 0.5f, 0.18f));
        var exit = MakeTile("Exit", new Color(0.2f, 0.8f, 0.35f));
        var heroStart = MakeTile("HeroStart", new Color(0.55f, 0.12f, 0.18f));
        var marker = MakeTile("Marker", Color.white, TileFlags.None); // runtime overlay only, not in the palette
        AssetDatabase.SaveAssets();

        // Tile palette for painting levels (Window > 2D > Tile Palette).
        Directory.CreateDirectory("Assets/Palettes");
        const string palettePath = "Assets/Palettes/Dungeon.prefab";
        if (!File.Exists(palettePath))
            GridPaletteUtility.CreateNewPalette("Assets/Palettes", "Dungeon", GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        var palette = PrefabUtility.LoadPrefabContents(palettePath);
        var paletteMap = palette.GetComponentInChildren<Tilemap>();
        paletteMap.ClearAllTiles();
        Tile[] paletteTiles = { wall, floor, crate, exit, heroStart };
        for (int i = 0; i < paletteTiles.Length; i++) paletteMap.SetTile(new Vector3Int(i, 0, 0), paletteTiles[i]);
        PrefabUtility.SaveAsPrefabAsset(palette, palettePath);
        PrefabUtility.UnloadPrefabContents(palette);

        var old = GameObject.Find("Dungeon");
        if (old) Object.DestroyImmediate(old);

        var root = new GameObject("Dungeon", typeof(Grid));
        Tilemap MakeMap(string name, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(root.transform);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }
        var map = MakeMap("Map", 0);
        var overlay = MakeMap("Overlay", 1);

        var heroGo = new GameObject("Hero", typeof(SpriteRenderer));
        heroGo.transform.SetParent(root.transform);
        heroGo.transform.localScale = Vector3.one * 0.7f;
        var sr = heroGo.GetComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.color = new Color(0.9f, 0.2f, 0.25f); sr.sortingOrder = 2;

        // Paint the test level: walls fill the bounding box, floor/crate/exit on top.
        var rows = DungeonSelfTest.Level1;
        var d = Dungeon.Parse(rows);
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[0].Length; x++)
            {
                var c = new Vector2Int(x, y);
                TileBase t = !d.floor.Contains(c) ? wall : d.crates.Contains(c) ? crate : c == d.exit ? exit : c == d.hero ? heroStart : floor;
                map.SetTile((Vector3Int)c, t);
            }
        heroGo.transform.position = map.GetCellCenterWorld((Vector3Int)d.hero); // just for the Scene view; the game places the hero

        var game = root.AddComponent<DungeonGame>();
        game.map = map; game.overlay = overlay; game.hero = heroGo.transform;
        game.floorTile = floor; game.crateTile = crate; game.exitTile = exit; game.heroStartTile = heroStart; game.markerTile = marker;

        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        return $"built: {map.GetUsedTilesCount()} tile types, bounds {map.cellBounds}";
    }
}
