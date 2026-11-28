using UnityEngine;

// The mouse cursor everywhere: a red crosshair with a dark outline, so it shows on both the floor and the walls.
// Gold while it's over a crate you can grab (DungeonGame decides), with a quick pop each time it lands on one.
// Drawn from code like the sounds, so there's no image file.
public static class Crosshair
{
    public const int Size = 32;
    public enum Look { Red, Gold, Pop }
    public static readonly Vector2 Hotspot = new(Size / 2, Size / 2);
    static readonly Color Red = new(0.82f, 0.23f, 0.18f), Gold = new(0.95f, 0.78f, 0.36f), Edge = new(0.06f, 0.04f, 0.05f);
    const float PopTime = 0.08f;
    static readonly Texture2D[] textures = new Texture2D[3];
    static Look? showing;
    static float popUntil;

    public static Texture2D Texture(Look look)
    {
        int i = (int)look;
        if (textures[i] == null)
            textures[i] = look == Look.Pop ? Build(Gold, 8f, 11f) : Build(look == Look.Gold ? Gold : Red, 7f, 9f); // pop: bigger, fatter ring
        return textures[i];
    }

    // Call every frame from a level; menus keep the red one.
    public static void Show(bool gold)
    {
        var look = !gold ? Look.Red : Time.unscaledTime < popUntil ? Look.Pop : Look.Gold;
        if (showing == look) return; // SetCursor every frame would be wasteful
        showing = look;
        Cursor.SetCursor(Texture(look), Hotspot, CursorMode.Auto);
    }

    // Hardware cursors can't scale, so the pop is a few frames of the big ring.
    public static void Pop() => popUntil = Time.unscaledTime + PopTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        showing = null; // statics survive between play sessions (no domain reload)
        popUntil = 0f;
        Show(false);
    }

    // Ring between ringIn and ringOut, four ticks with a gap in the middle, and a center dot.
    public static bool IsFill(int x, int y, float ringIn = 7f, float ringOut = 9f)
    {
        float dx = x + 0.5f - Size / 2, dy = y + 0.5f - Size / 2, d = Mathf.Sqrt(dx * dx + dy * dy);
        bool ring = d >= ringIn && d <= ringOut;
        bool tick = (Mathf.Abs(dx) < 1f && Mathf.Abs(dy) is >= 5f and <= 13f) || (Mathf.Abs(dy) < 1f && Mathf.Abs(dx) is >= 5f and <= 13f);
        return ring || tick || d < 1.5f;
    }

    static Texture2D Build(Color fill, float ringIn, float ringOut)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontUnloadUnusedAsset };
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                bool edge = false;
                for (int oy = -1; oy <= 1 && !edge; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (x + ox is >= 0 and < Size && y + oy is >= 0 and < Size && IsFill(x + ox, y + oy, ringIn, ringOut)) { edge = true; break; }
                px[y * Size + x] = IsFill(x, y, ringIn, ringOut) ? fill : edge ? Edge : Color.clear;
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
