using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/Art/Splash.png, a full-screen 2560x1440 splash (dungeon floor, the app icon, the menu title from Text.txt
// in the menu fonts), and sets it as Player Settings > Splash Image > Background Image, with no logos and no Unity logo.
// Run Tools/BuildIcon.cs first (it uses AppIcon.png). Run: unity command script run --file Tools/BuildSplash.cs --entry BuildSplash.Run
public static class BuildSplash
{
    const string Path = "Assets/Art/Splash.png";
    const int W = 2560, H = 1440, TitleWidth = 2200, IconSize = 256;
    static readonly Color Dark = new(0.06f, 0.04f, 0.05f), Gold = new(0.95f, 0.78f, 0.36f), Cream = new(0.93f, 0.88f, 0.78f);

    public static string Run()
    {
        var px = new Color[W * H];
        void Over(int x, int y, Color c) { if (x is >= 0 and < W && y is >= 0 and < H) { ref var d = ref px[y * W + x]; d = Color.Lerp(d, c, c.a); d.a = 1f; } }

        // Dungeon floor tiles at 8x, dimmed, darker toward the edges.
        var floor = Load("Assets/Art/Kenney/TinyDungeon/tile_0048.png");
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x - W / 2f) / W, dy = (y - H / 2f) / H;
                float light = Mathf.Lerp(0.3f, 0.05f, Mathf.Clamp01((dx * dx + dy * dy) * 3.2f));
                var c = floor.GetPixel(x / 8 % 16, y / 8 % 16);
                px[y * W + x] = Color.Lerp(Dark, c, light);
                px[y * W + x].a = 1f;
            }

        // Layout, top to bottom: icon, gap, title block.
        var title = RenderTitle(out int titleH, out RectInt kicker);
        const int gap = 40;
        int top = (H + IconSize + gap + titleH) / 2; // y of the icon's top edge (rows count up from the bottom)
        int iconX = (W - IconSize) / 2, iconY = top - IconSize;

        var icon = Load("Assets/Art/AppIcon.png");
        int step = icon.width / IconSize; // whole-number step keeps the pixel art crisp
        for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++) Over(iconX + x, iconY + y, icon.GetPixel(x * step, y * step));

        int titleX = (W - TitleWidth) / 2, titleY = iconY - gap - titleH;
        for (int y = 0; y < titleH; y++)
            for (int x = 0; x < TitleWidth; x++) Over(titleX + x, titleY + y, title[y * TitleWidth + x]);

        // Gold rules on both sides of the kicker, each ending in a small diamond, like the main menu.
        int ky = titleY + kicker.y + kicker.height / 2;
        foreach (int side in new[] { -1, 1 })
        {
            int inner = titleX + (side < 0 ? kicker.xMin - 40 : kicker.xMax + 40), outer = inner + side * 320;
            for (int x = Mathf.Min(inner, outer); x <= Mathf.Max(inner, outer); x++)
                for (int t = -1; t <= 1; t++) Over(x, ky + t, Gold);
            for (int y = -9; y <= 9; y++)
                for (int x = -9; x <= 9; x++)
                    if (Mathf.Abs(x) + Mathf.Abs(y) <= 9) Over(outer + x, ky + y, Gold);
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(Path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Sprite; // the splash Background Image takes a sprite
        importer.maxTextureSize = 4096; // the default 2048 would shrink it and blur it
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var art = AssetDatabase.LoadAssetAtPath<Sprite>(Path);

        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
        PlayerSettings.SplashScreen.background = art;
        PlayerSettings.SplashScreen.backgroundPortrait = null;
        PlayerSettings.SplashScreen.blurBackgroundImage = false;
        PlayerSettings.SplashScreen.overlayOpacity = 0f; // show the image as it is, with no dark layer over it
        PlayerSettings.SplashScreen.backgroundColor = Dark;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
        AssetDatabase.DeleteAsset("Assets/Art/SplashLogo.png"); // the old logo-only version
        AssetDatabase.SaveAssets();
        return $"splash {art.texture.width}x{art.texture.height}, unity logo {PlayerSettings.SplashScreen.showUnityLogo}";
    }

    static Texture2D Load(string path)
    {
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(path));
        t.filterMode = FilterMode.Point;
        return t;
    }

    // The kicker and title, TitleWidth pixels wide on a see-through background. kicker: where the kicker text sits, in those pixels.
    static Color[] RenderTitle(out int height, out RectInt kicker)
    {
        var scene = EditorSceneManager.NewPreviewScene(); // so the open scene isn't touched
        try
        {
            var titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/CinzelDecorative-Black.ttf");
            var kickerFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/Cinzel-ExtraBold.ttf");
            string title = GameText.Get("menu.title"), kickerText = GameText.Get("menu.kicker").ToUpperInvariant();

            TextMesh Text(string s, Font font, int size, Color color, Vector3 at)
            {
                var go = new GameObject("text");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = at;
                var t = go.AddComponent<TextMesh>();
                t.font = font;
                t.fontSize = size;
                t.characterSize = 0.1f;
                t.anchor = TextAnchor.MiddleCenter;
                t.alignment = TextAlignment.Center;
                t.richText = true;
                t.color = color;
                t.text = s;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                return t;
            }

            Text(Regex.Replace(title, "<.*?>", ""), titleFont, 220, new Color(0f, 0f, 0f, 0.85f), new Vector3(0.14f, -0.2f, 1f)); // drop shadow, like the menu's
            var main = Text(title, titleFont, 220, Cream, Vector3.zero);
            var titleBounds = main.GetComponent<MeshRenderer>().bounds;
            var kick = Text(kickerText, kickerFont, 64, Gold, new Vector3(0f, titleBounds.max.y + 0.6f, 0f));
            var kickBounds = kick.GetComponent<MeshRenderer>().bounds;

            var bounds = titleBounds;
            bounds.Encapsulate(kickBounds);
            bounds.Expand(new Vector3(0.6f, 0.6f, 0f));
            height = Mathf.RoundToInt(TitleWidth * bounds.size.y / bounds.size.x);
            float scale = TitleWidth / bounds.size.x;
            kicker = new RectInt(Mathf.RoundToInt((kickBounds.min.x - bounds.min.x) * scale), Mathf.RoundToInt((kickBounds.min.y - bounds.min.y) * scale),
                Mathf.RoundToInt(kickBounds.size.x * scale), Mathf.RoundToInt(kickBounds.size.y * scale));

            var camGo = new GameObject("cam");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = bounds.extents.y;
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.aspect = (float)TitleWidth / height;

            // Linear: the text's colors are written as they are. sRGB would brighten them a second time and wash them out.
            var rt = new RenderTexture(TitleWidth, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(TitleWidth, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, TitleWidth, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            var result = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return result;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
