using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

// Rebuilds the Main Menu scene from scratch. Run with:
// unity command script run --file Tools/BuildMenu.cs --entry BuildMenu.Run
// WARNING: deletes everything in Main Menu.unity first. The UI itself lives in Assets/UI (edit it there, not here).
public static class BuildMenu
{
    const string ScenePath = "Assets/Scenes/Main Menu.unity";
    const string TileDir = "Assets/Art/Kenney/TinyDungeon";

    public static string Run()
    {
        // Open first: reopening a scene unloads assets nothing references.
        var scene = EditorSceneManager.OpenScene(ScenePath);

        // Kenney tiles: crisp pixel art, 16 px = 1 world unit.
        foreach (var path in Directory.GetFiles(TileDir, "*.png"))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        Sprite Tile(int i) => AssetDatabase.LoadAssetAtPath<Sprite>($"{TileDir}/tile_{i:0000}.png");

        const string panelPath = "Assets/UI/MenuPanelSettings.asset";
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
        if (panel == null)
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panel, panelPath);
        }
        panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/MenuTheme.tss");
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1920, 1080);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = 0.5f;
        EditorUtility.SetDirty(panel);

        foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>(); // menu clicks need something to hear them
        cam.orthographic = true;
        cam.orthographicSize = 1080f / (2 * 64); // 64 px per tile at 1080p, an exact 4x of the 16 px art
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.02f, 0.04f);

        var allLayers = System.Array.ConvertAll(SortingLayer.layers, l => l.id);
        var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
        global.lightType = Light2D.LightType.Global;
        global.targetSortingLayers = allLayers;
        global.intensity = 0.3f;
        global.color = new Color(0.62f, 0.66f, 0.85f);

        // Tile numbers refer to Kenney's Tiny Dungeon sheet (row * 12 + column).
        var dungeon = new GameObject("Menu Dungeon").AddComponent<MenuDungeon>();
        dungeon.floorSprites = new[] { Tile(48), Tile(48), Tile(48), Tile(49) };
        dungeon.decorSprites = new[] { Tile(64), Tile(65), Tile(113), Tile(114), Tile(115) };
        dungeon.minionSprites = new[] { Tile(108), Tile(110), Tile(111), Tile(120), Tile(122) };
        dungeon.wallFront = Tile(40);
        dungeon.wallTop = Tile(0);
        dungeon.banner = Tile(29);
        dungeon.crate = Tile(72);
        dungeon.hero = Tile(97);
        dungeon.treasure = Tile(89);

        var ui = new GameObject("Menu UI");
        var doc = ui.AddComponent<UIDocument>();
        doc.panelSettings = panel;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu.uxml");
        ui.AddComponent<MainMenu>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.SaveScene(scene);

        // Menu first, so a build starts there.
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Main Scene.unity", true),
        };
        AssetDatabase.SaveAssets();
        return "Main Menu rebuilt";
    }
}
