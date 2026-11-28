using UnityEngine;

public enum Sound { Pick, Drop, Slide, Smash, Zap, Stone, Gulp, Pop, Yoink, Recall, Step, FoeStep, Wait, Huh, Win, Lose, Click, Nope }

// Silly sound effects, synthesized from code the first time one plays, so there are no audio files.
// Sfx.Play(Sound.Gulp) from anywhere. Volume is the Sound effects slider; the master slider is AudioListener.volume.
// Also plays the looping music, from the first scene on, at the Music slider's volume.
public static class Sfx
{
    const int Rate = 44100;
    enum Wave { Square, Saw, Triangle, Sine, Noise }

    static AudioClip[] clips;
    static AudioClip musicClip;
    static GameObject host;
    static AudioSource[] voices;
    static AudioSource music;
    static int next;
    static readonly System.Random rng = new(7);

    // Rebuilt if Unity unloaded them (entering play mode without a domain reload).
    public static AudioClip[] Clips => clips != null && clips[0] != null ? clips : clips = Build();

    public static AudioClip MusicClip => musicClip != null ? musicClip : musicClip = MakeClip("Music", BuildMusic());

    // volume: a fraction of the Sound effects slider, for quieter background sounds.
    public static void Play(Sound s, float pitch = 1f, float volume = 1f)
    {
        MakeHost();
        var v = voices[next++ % voices.Length];
        v.clip = Clips[(int)s];
        v.pitch = pitch * Random.Range(0.95f, 1.05f); // a little wobble so repeats don't sound copy-pasted
        v.volume = GameSettings.Sfx * volume;
        v.Play();
    }

    // Call after the Music slider moves or the game pauses or resumes. Paused, the music drops to a third.
    public static void UpdateMusicVolume()
    {
        if (music != null) music.volume = GameSettings.Music * (PauseMenu.Paused ? 0.3f : 1f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartMusic()
    {
        MakeHost();
        music.clip = MusicClip;
        UpdateMusicVolume();
        music.Play();
    }

    // One object that lives across scenes, so the music doesn't restart on every level.
    static void MakeHost()
    {
        if (host != null) return;
        host = new GameObject("Sfx");
        Object.DontDestroyOnLoad(host);
        voices = new AudioSource[8]; // a few voices so overlapping sounds each keep their own pitch
        for (int i = 0; i < voices.Length; i++) voices[i] = host.AddComponent<AudioSource>();
        music = host.AddComponent<AudioSource>();
        music.loop = true;
    }

    static AudioClip[] Build()
    {
        float[] Trombone(float f, float secs, float vib = 0f) => Mix(Tone(Wave.Saw, f, f * 0.98f, secs, 0.14f, vib: vib, depth: 0.03f), Tone(Wave.Triangle, f, f * 0.98f, secs, 0.25f, vib: vib, depth: 0.03f));

        var c = new float[System.Enum.GetValues(typeof(Sound)).Length][];
        c[(int)Sound.Pick] = Tone(Wave.Square, 520, 900, 0.06f, 0.15f);
        c[(int)Sound.Drop] = Tone(Wave.Square, 700, 380, 0.06f, 0.15f);
        c[(int)Sound.Slide] = Mix(Tone(Wave.Noise, 2500, 900, 0.14f, 0.18f, decay: 10), Tone(Wave.Triangle, 260, 150, 0.14f, 0.35f));
        c[(int)Sound.Smash] = Mix(Tone(Wave.Noise, 1400, 150, 0.3f, 0.5f, decay: 10), Tone(Wave.Square, 180, 50, 0.22f, 0.18f));
        c[(int)Sound.Zap] = Tone(Wave.Square, 1500, 180, 0.18f, 0.16f); // pew
        c[(int)Sound.Stone] = Mix(Tone(Wave.Sine, 320, 70, 0.3f, 0.65f, decay: 12), Tone(Wave.Noise, 500, 100, 0.1f, 0.25f)); // bonk
        c[(int)Sound.Gulp] = Cat(Tone(Wave.Sine, 180, 420, 0.08f, 0.6f), Silence(0.05f), Tone(Wave.Sine, 200, 480, 0.08f, 0.6f), Silence(0.05f), Tone(Wave.Sine, 240, 620, 0.1f, 0.6f));
        c[(int)Sound.Pop] = Cat(Tone(Wave.Sine, 500, 1500, 0.05f, 0.6f), Tone(Wave.Triangle, 1600, 1600, 0.25f, 0.35f, decay: 14)); // pop, ding
        c[(int)Sound.Yoink] = Cat(Tone(Wave.Sine, 400, 2200, 0.16f, 0.45f), Silence(0.04f), Tone(Wave.Square, 900, 900, 0.04f, 0.1f), Silence(0.03f), Tone(Wave.Square, 1050, 1050, 0.04f, 0.1f));
        c[(int)Sound.Recall] = Cat(Tone(Wave.Sine, 1700, 280, 0.28f, 0.45f, vib: 9, depth: 0.04f), Tone(Wave.Sine, 280, 1300, 0.2f, 0.45f)); // slide whistle down and up
        c[(int)Sound.Step] = Tone(Wave.Sine, 900, 1350, 0.05f, 0.22f, vib: 60, depth: 0.05f); // squeaky shoes
        c[(int)Sound.FoeStep] = Tone(Wave.Triangle, 230, 150, 0.06f, 0.4f, decay: 30);
        c[(int)Sound.Wait] = Cat(Tone(Wave.Square, 1800, 1800, 0.015f, 0.15f), Silence(0.12f), Tone(Wave.Square, 1300, 1300, 0.015f, 0.15f)); // tick tock
        c[(int)Sound.Huh] = Tone(Wave.Sine, 520, 190, 0.55f, 0.5f, vib: 16, depth: 0.25f, decay: 3); // boi-oi-oing
        c[(int)Sound.Win] = Cat(Kazoo(523, 0.1f), Kazoo(659, 0.1f), Kazoo(784, 0.1f), Kazoo(1047, 0.5f));
        c[(int)Sound.Lose] = Cat(Trombone(294, 0.32f), Trombone(277, 0.32f), Trombone(262, 0.32f), Trombone(247, 1f, vib: 5)); // wah wah wah waaah
        c[(int)Sound.Click] = Tone(Wave.Triangle, 900, 1150, 0.035f, 0.3f);
        c[(int)Sound.Nope] = Cat(Tone(Wave.Square, 220, 200, 0.07f, 0.12f), Silence(0.04f), Tone(Wave.Square, 180, 160, 0.1f, 0.12f)); // eh-eh

        var result = new AudioClip[c.Length];
        for (int i = 0; i < c.Length; i++) result[i] = MakeClip(((Sound)i).ToString(), c[i]);
        return result;
    }

    static AudioClip MakeClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        clip.hideFlags = HideFlags.DontUnloadUnusedAsset; // nothing references it, so a scene load would unload it
        return clip;
    }

    // A sneaky villain tiptoe in A minor: plucky bass, a thump and tick beat, and the tune played twice,
    // the second time on kazoo. 64 eighth notes, about 13 seconds, loops cleanly. -1 is a rest.
    static float[] BuildMusic()
    {
        const float eighth = 0.2f;
        int[] bass =
        {
            45, -1, 40, -1, 45, -1, 40, -1,   45, -1, 40, -1, 45, 46, 47, 48,
            38, -1, 33, -1, 38, -1, 33, -1,   40, -1, 35, -1, 40, 41, 42, 44,
        };
        int[] tune =
        {
            69, -1, 72, -1, 71, -1, 68, -1,   69, -1, 64, -1, 65, 66, 67, 68,
            74, -1, 77, -1, 76, -1, 72, -1,   71, -1, 68, -1, 64, -1, -1, -1,
        };
        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        int slot = (int)(eighth * Rate);
        var song = new float[slot * bass.Length * 2];
        void Add(float[] note, int at) { for (int i = 0; i < note.Length && at + i < song.Length; i++) song[at + i] += note[i]; }
        for (int s = 0; s < bass.Length * 2; s++)
        {
            int at = s * slot, b = bass[s % bass.Length], m = tune[s % tune.Length];
            if (b >= 0) Add(Tone(Wave.Triangle, Hz(b), Hz(b), eighth * 0.6f, 0.3f), at);
            if (m >= 0) Add(s < bass.Length ? Tone(Wave.Square, Hz(m), Hz(m), eighth * 0.8f, 0.05f, decay: 8) : Kazoo(Hz(m), eighth * 0.85f, 0.08f), at);
            if (s % 4 == 0) Add(Tone(Wave.Sine, 130, 45, 0.12f, 0.3f, decay: 20), at);
            if (s % 2 == 1) Add(Tone(Wave.Noise, 7000, 7000, 0.03f, 0.05f, decay: 60), at);
        }
        return song;
    }

    static float[] Kazoo(float f, float secs, float vol = 0.16f) =>
        Mix(Tone(Wave.Saw, f, f, secs, vol, vib: 5.5f, depth: 0.012f), Tone(Wave.Noise, f * 2, f * 2, secs, vol * 0.2f));

    // One note sweeping from f0 to f1 Hz. vib/depth: wobble speed (Hz) and size (fraction of pitch). decay: how fast it fades.
    static float[] Tone(Wave w, float f0, float f1, float secs, float vol, float vib = 0f, float depth = 0f, float decay = 0f)
    {
        int n = (int)(secs * Rate);
        var data = new float[n];
        double phase = 0;
        float held = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n, time = (float)i / Rate;
            float f = f0 * Mathf.Pow(f1 / f0, t) * (1f + depth * Mathf.Sin(2f * Mathf.PI * vib * time));
            double before = phase;
            phase += f / Rate;
            float p = (float)(phase % 1.0);
            if (w == Wave.Noise && (int)(phase * 2) != (int)(before * 2)) held = (float)rng.NextDouble() * 2f - 1f; // pitched, crunchy noise
            float v = w switch
            {
                Wave.Square => p < 0.5f ? 1f : -1f,
                Wave.Saw => 2f * p - 1f,
                Wave.Triangle => 1f - 4f * Mathf.Abs(p - 0.5f),
                Wave.Sine => Mathf.Sin(2f * Mathf.PI * p),
                _ => held,
            };
            float env = Mathf.Min(1f, time / 0.004f) * Mathf.Min(1f, (n - i) / (0.02f * Rate)) * Mathf.Exp(-decay * time); // no clicks at either end
            data[i] = v * env * vol;
        }
        return data;
    }

    static float[] Silence(float secs) => new float[(int)(secs * Rate)];

    static float[] Cat(params float[][] parts)
    {
        var all = new System.Collections.Generic.List<float>();
        foreach (var p in parts) all.AddRange(p);
        return all.ToArray();
    }

    static float[] Mix(float[] a, float[] b)
    {
        var m = new float[Mathf.Max(a.Length, b.Length)];
        for (int i = 0; i < m.Length; i++) m[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
        return m;
    }
}
