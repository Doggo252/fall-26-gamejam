using System.IO;
using UnityEditor;
using UnityEngine;

// Builds Assets/Art/AppIcon.png (1024x1024) from the Kenney tiles and the in-game crosshair, and sets it as
// Player Settings > Default Icon. Run: unity command script run --file Tools/BuildIcon.cs --entry BuildIcon.Run
public static class BuildIcon
{
    const int Art = 64, Scale = 16; // drawn on a 64x64 pixel-art grid, then each art pixel becomes 16x16
    const string Path = "Assets/Art/AppIcon.png";

    public static string Run()
    {
        Color[] Tile(int n)
        {
            var t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes($"Assets/Art/Kenney/TinyDungeon/tile_{n:0000}.png"));
            return t.GetPixels(); // 16x16, bottom row first
        }

        var px = new Color[Art * Art];
        void Put(int x, int y, Color c) { if (x is >= 0 and < Art && y is >= 0 and < Art) px[y * Art + x] = Color.Lerp(px[y * Art + x], c, c.a); }

        // Dungeon floor, darker toward the edges so the crate stands out.
        var floor = Tile(48);
        for (int y = 0; y < Art; y++)
            for (int x = 0; x < Art; x++)
            {
                float dx = (x + 0.5f) / Art - 0.5f, dy = (y + 0.5f) / Art - 0.5f;
                px[y * Art + x] = floor[(y % 16) * 16 + x % 16] * Mathf.Lerp(1f, 0.35f, (dx * dx + dy * dy) * 3.2f);
                px[y * Art + x].a = 1f;
            }

        // The crate at 2x, in the middle, with a soft shadow.
        var crate = Tile(63);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                var c = crate[(y / 2) * 16 + x / 2];
                if (c.a > 0f) Put(16 + x + 1, 16 + y - 2, new Color(0, 0, 0, 0.45f));
            }
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++) Put(16 + x, 16 + y, crate[(y / 2) * 16 + x / 2]);

        // A red crosshair ring around it (the in-game one, redrawn bigger so the crate stays visible inside),
        // with ticks pointing in from outside.
        var red = new Color(0.82f, 0.23f, 0.18f);
        bool Ring(int x, int y)
        {
            float dx = x + 0.5f - Art / 2, dy = y + 0.5f - Art / 2, d = Mathf.Sqrt(dx * dx + dy * dy);
            bool tick = (Mathf.Abs(dx) < 1f && Mathf.Abs(dy) is >= 19f and <= 26f) || (Mathf.Abs(dy) < 1f && Mathf.Abs(dx) is >= 19f and <= 26f);
            return d is >= 23f and <= 25f || tick;
        }
        var ring = new bool[Art * Art];
        for (int y = 0; y < Art; y++)
            for (int x = 0; x < Art; x++) ring[y * Art + x] = Ring(x, y);
        for (int y = 1; y < Art - 1; y++)
            for (int x = 1; x < Art - 1; x++)
            {
                if (ring[y * Art + x]) { Put(x, y, red); continue; }
                for (int o = 0; o < 9; o++)
                    if (ring[(y + o / 3 - 1) * Art + x + o % 3 - 1]) { Put(x, y, new Color(0.06f, 0.04f, 0.05f)); break; }
            }

        // Gold frame with rounded corners; outside the corners is see-through.
        var gold = new Color(0.95f, 0.78f, 0.36f);
        var dark = new Color(0.06f, 0.04f, 0.05f);
        const float radius = 8f;
        for (int y = 0; y < Art; y++)
            for (int x = 0; x < Art; x++)
            {
                // distance inside the rounded square (0 at its edge)
                float cx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (Art - radius), 0f);
                float cy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (Art - radius), 0f);
                float inside = cx > 0f && cy > 0f ? radius - Mathf.Sqrt(cx * cx + cy * cy) : Mathf.Min(Mathf.Min(x + 0.5f, Art - x - 0.5f), Mathf.Min(y + 0.5f, Art - y - 0.5f));
                if (inside < 0f) px[y * Art + x] = Color.clear;
                else if (inside < 1f) px[y * Art + x] = dark;
                else if (inside < 3f) px[y * Art + x] = gold;
                else if (inside < 4f) px[y * Art + x] = dark;
            }

        var tex = new Texture2D(Art * Scale, Art * Scale, TextureFormat.RGBA32, false);
        var big = new Color[tex.width * tex.height];
        for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++) big[y * tex.width + x] = px[(y / Scale) * Art + x / Scale];
        tex.SetPixels(big);
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(Path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Default; // a plain Texture 2D, which the icon slot needs
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
        return "icon set: " + (PlayerSettings.GetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, IconKind.Any)[0] == icon);
    }
}
