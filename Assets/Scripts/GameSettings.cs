using UnityEngine;

// Player options saved in PlayerPrefs and applied at startup, whichever scene the game starts in.
public static class GameSettings
{
    // The Sfx class reads Music and Sfx.
    public static float Master { get => PlayerPrefs.GetFloat("audio.master", 0.8f); set { PlayerPrefs.SetFloat("audio.master", value); AudioListener.volume = value; } }
    public static float Music { get => PlayerPrefs.GetFloat("audio.music", 0.7f); set => PlayerPrefs.SetFloat("audio.music", value); }
    public static float Sfx { get => PlayerPrefs.GetFloat("audio.sfx", 0.8f); set => PlayerPrefs.SetFloat("audio.sfx", value); }

    // shortcut: stored only; the game has no mouse-driven camera yet. Use it once pan or zoom exists.
    public static float MouseSensitivity { get => PlayerPrefs.GetFloat("input.sensitivity", 1f); set => PlayerPrefs.SetFloat("input.sensitivity", value); }

    public static int Quality
    {
        get => QualitySettings.GetQualityLevel();
        set { QualitySettings.SetQualityLevel(value, true); PlayerPrefs.SetInt("gfx.quality", value); ApplyVSync(); }
    }

    public static bool VSync { get => PlayerPrefs.GetInt("gfx.vsync", 1) == 1; set { PlayerPrefs.SetInt("gfx.vsync", value ? 1 : 0); ApplyVSync(); } }

    // Unity already remembers fullscreen and resolution between runs, so these go straight to Screen.
    public static bool ScreenShake { get => PlayerPrefs.GetInt("gfx.shake", 1) == 1; set => PlayerPrefs.SetInt("gfx.shake", value ? 1 : 0); }
    public static bool Fullscreen { get => Screen.fullScreen; set => Screen.fullScreen = value; }

    static void ApplyVSync() => QualitySettings.vSyncCount = VSync ? 1 : 0; // a quality level change resets vSyncCount

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        AudioListener.volume = Master;
        if (PlayerPrefs.HasKey("gfx.quality")) QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("gfx.quality"), true);
        ApplyVSync();
    }
}

// Which levels the player has beaten, and in how few turns. Level N unlocks once level N-1 is beaten.
public static class Progress
{
    // Scene names in play order. Each must be in Build Settings (Tools/BuildLevels.cs adds them).
    public static readonly string[] Levels = { "Main Scene", "Level 2", "Level 3", "Level 4", "Level 5", "Level 6", "Level 7", "Level 8",
        "Level 9", "Level 10", "Level 11", "Level 12", "Level 13", "Level 14", "Level 15", "Level 16" };

    public static string DisplayName(string level) => System.Array.IndexOf(Levels, level) is var i and >= 0 ? GameText.Get("level.name", ("n", i + 1)) : level;

    // The level after this one, or null after the last.
    public static string Next(string level) => System.Array.IndexOf(Levels, level) is var i and >= 0 && i + 1 < Levels.Length ? Levels[i + 1] : null;

    public static int BestTurns(string level) => PlayerPrefs.GetInt("best." + level, 0); // 0 = never beaten

    public static bool IsComplete(string level) => BestTurns(level) > 0;

    // Fewest turns that win each level, in Levels order. Dungeon > Check Par Turns re-solves them after a layout change.
    public static readonly int[] Par = { 5, 10, 9, 10, 12, 10, 16, 10, 11, 11, 18, 12, 14, 10, 13, 20 };

    public static int ParFor(string level) => System.Array.IndexOf(Levels, level) is var i and >= 0 ? Par[i] : 0;

    // 3 stars at par, 2 within half again, 1 for any win. A run that used a hint gets 2 at most.
    public static int StarsFor(string level, int turns, bool hinted)
    {
        int par = ParFor(level);
        int stars = par == 0 || turns <= par ? 3 : turns <= par + (par + 1) / 2 ? 2 : 1;
        return hinted ? Mathf.Min(stars, 2) : stars;
    }

    // Saves from before stars existed get stars from their best turn count.
    public static int Stars(string level) => PlayerPrefs.GetInt("stars." + level, IsComplete(level) ? StarsFor(level, BestTurns(level), false) : 0);

    // Returns true when this run set a new best turn count. Keeps the most stars ever earned.
    public static bool Complete(string level, int turns, int stars)
    {
        if (stars > Stars(level)) PlayerPrefs.SetInt("stars." + level, stars);
        int best = BestTurns(level);
        bool newBest = best == 0 || turns < best;
        if (newBest) PlayerPrefs.SetInt("best." + level, turns);
        PlayerPrefs.Save();
        return newBest;
    }

    public static float BestTime(string level) => PlayerPrefs.GetFloat("time." + level, 0f); // seconds, 0 = none

    // Returns true when this win was the fastest yet.
    public static bool CompleteTime(string level, float seconds)
    {
        float best = BestTime(level);
        if (best > 0f && seconds >= best) return false;
        PlayerPrefs.SetFloat("time." + level, seconds);
        PlayerPrefs.Save();
        return true;
    }

    // 83.4 seconds -> "1:23.4"
    public static string FormatTime(float seconds)
    {
        int tenths = (int)(seconds * 10f); // round down, so 59.97 shows 0:59.9 and never 0:60.0
        return $"{tenths / 600}:{tenths % 600 / 10:00}.{tenths % 10}";
    }

    public static bool IsUnlocked(string[] levels, int i) => i == 0 || IsComplete(levels[i - 1]);

    // How to Play keys for each potion and companion, in Potion and Foe enum order (Text.txt has howto.<key>.title and .body).
    public static readonly string[] PotionKeys = { "potion", "sleep", "recall", "rage", "stone" };
    public static readonly string[] FoeKeys = { "dwarf", "brute", "wizard", "thief", "hunter" };

    // Whether the player has dismissed the new-piece card for this piece.
    public static bool Seen(string piece) => PlayerPrefs.GetInt($"seen.{piece}", 0) == 1;

    public static void MarkSeen(string piece)
    {
        PlayerPrefs.SetInt($"seen.{piece}", 1);
        PlayerPrefs.Save();
    }

    public static void Reset(string[] levels)
    {
        foreach (var l in levels) { PlayerPrefs.DeleteKey("best." + l); PlayerPrefs.DeleteKey("stars." + l); PlayerPrefs.DeleteKey("time." + l); }
        foreach (var k in PotionKeys) PlayerPrefs.DeleteKey($"seen.{k}");
        foreach (var k in FoeKeys) PlayerPrefs.DeleteKey($"seen.{k}");
        PlayerPrefs.Save();
    }
}
