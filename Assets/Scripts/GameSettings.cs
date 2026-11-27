using UnityEngine;

// Player options saved in PlayerPrefs and applied at startup, whichever scene the game starts in.
public static class GameSettings
{
    // shortcut: Music and Sfx are only stored; nothing plays audio yet. Multiply AudioSource.volume by them once sounds exist.
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
    public static readonly string[] Levels = { "Main Scene", "Level 2", "Level 3", "Level 4", "Level 5", "Level 6" };

    public static string DisplayName(string level) => System.Array.IndexOf(Levels, level) is var i and >= 0 ? GameText.Get("level.name", ("n", i + 1)) : level;

    // The level after this one, or null after the last.
    public static string Next(string level) => System.Array.IndexOf(Levels, level) is var i and >= 0 && i + 1 < Levels.Length ? Levels[i + 1] : null;

    public static int BestTurns(string level) => PlayerPrefs.GetInt("best." + level, 0); // 0 = never beaten

    public static bool IsComplete(string level) => BestTurns(level) > 0;

    // Returns true when this run set a new best.
    public static bool Complete(string level, int turns)
    {
        int best = BestTurns(level);
        if (best != 0 && turns >= best) return false;
        PlayerPrefs.SetInt("best." + level, turns);
        PlayerPrefs.Save();
        return true;
    }

    public static bool IsUnlocked(string[] levels, int i) => i == 0 || IsComplete(levels[i - 1]);

    public static void Reset(string[] levels)
    {
        foreach (var l in levels) PlayerPrefs.DeleteKey("best." + l);
        PlayerPrefs.Save();
    }
}
