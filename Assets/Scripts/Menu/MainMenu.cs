using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Drives the main menu (UI Toolkit): intro animation, panels, level select, embers and the live feed.
// Most motion is USS transitions in MainMenu.uss; this script only adds and removes classes at the right moments.
[RequireComponent(typeof(UIDocument))]
public class MainMenu : MonoBehaviour
{
    static string[] levels => Progress.Levels;
    public MenuDungeon dungeon;
    public int emberCount = 40;

    class Ember { public VisualElement e; public float x, y, rise, sway, phase, alpha; }

    VisualElement root, fade, emberLayer;
    MenuPanels panels;
    SettingsPanel settings;
    readonly List<Ember> embers = new();
    readonly Dictionary<Label, int> shownStats = new();
    bool leaving;

    void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        GameText.Apply(root);
        fade = root.Q("fade");
        emberLayer = root.Q("embers");
        panels = new MenuPanels(root);
        settings = new SettingsPanel(root, panels);

        root.Q("shade").style.backgroundImage = MakeTexture(256, 8, (u, v) => Mathf.SmoothStep(0.9f, 0f, u));
        root.Q("vignette").style.backgroundImage = MakeTexture(256, 256, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, (v - 0.5f) * 0.8f).magnitude;
            return Mathf.Clamp01((d - 0.3f) / 0.4f) * 0.85f;
        });

        root.Q<Button>("play").clicked += () => LoadLevel(NextLevel());
        root.Q<Button>("levels").clicked += () => { BuildLevels(); panels.Open("levels-panel"); };
        root.Q<Button>("howto").clicked += () => { FillHowTo(); panels.Open("howto-panel"); };
        root.Q<Button>("settings").clicked += settings.Open;
        root.Q<Button>("credits").clicked += () => panels.Open("credits-panel");
        root.Q<Button>("quit").clicked += Quit;
        root.Query<Button>(className: "back-btn").ForEach(b => b.clicked += ClosePanel);
        root.Q<Button>("reset-progress").clicked += () => { Progress.Reset(levels); BuildLevels(); };

        SetupEmbers();
        PlayIntro();
    }

    // ---------- Intro and ambient motion ----------

    void PlayIntro()
    {
        var title = root.Q("title");
        title.AddToClassList("title--wide");
        After(80, () => fade.AddToClassList("fade--clear"));
        After(350, () => { root.Q("title-block").RemoveFromClassList("hidden-up"); title.RemoveFromClassList("title--wide"); });
        var buttons = root.Q("buttons").Children().ToList();
        for (int i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            After(700 + i * 85, () => b.RemoveFromClassList("hidden-left"));
        }
        After(1300, () => root.Q("feed").RemoveFromClassList("hidden-right"));
        After(1400, () => root.Q<Button>("play").Focus());

        root.schedule.Execute(() => title.ToggleInClassList("title--glow")).Every(2400).StartingIn(1600);
        root.schedule.Execute(() => root.Q("live-dot").ToggleInClassList("live-dot--dim")).Every(700);
        root.schedule.Execute(UpdateFeed).Every(200);
    }

    void UpdateFeed()
    {
        if (dungeon == null) return;
        Show("stat-steps", dungeon.HeroSteps);
        Show("stat-crates", dungeon.CratesDropped);
        Show("stat-trapped", dungeon.TimesTrapped);
        Show("stat-treasure", dungeon.TreasuresTaken);

        void Show(string name, int value)
        {
            var label = root.Q<Label>(name);
            if (shownStats.TryGetValue(label, out var old) && old == value) return;
            shownStats[label] = value;
            label.text = value.ToString();
            label.AddToClassList("feed-value--bump");
            After(220, () => label.RemoveFromClassList("feed-value--bump"));
        }
    }

    void SetupEmbers()
    {
        for (int i = 0; i < emberCount; i++)
        {
            var m = new Ember { e = new VisualElement { pickingMode = PickingMode.Ignore } };
            m.e.AddToClassList("ember");
            if (i % 4 == 0) m.e.style.backgroundColor = new Color(1f, 0.82f, 0.4f);
            emberLayer.Add(m.e);
            Respawn(m, anywhere: true);
            embers.Add(m);
        }
    }

    void Respawn(Ember m, bool anywhere)
    {
        float w = emberLayer.resolvedStyle.width, h = emberLayer.resolvedStyle.height;
        if (float.IsNaN(w) || w <= 0) (w, h) = (1920f, 1080f); // layout not ready on the first frame
        m.x = Random.Range(0f, w);
        m.y = anywhere ? Random.Range(0f, h) : h + 10f;
        m.rise = Random.Range(25f, 90f);
        m.sway = Random.Range(8f, 40f);
        m.phase = Random.value * 10f;
        m.alpha = Random.Range(0.35f, 0.95f);
        float s = Random.Range(0.6f, 1.5f);
        m.e.style.scale = new Scale(new Vector2(s, s));
    }

    void Update()
    {
        float t = Time.time;
        foreach (var m in embers)
        {
            m.y -= m.rise * Time.deltaTime;
            if (m.y < -20f) Respawn(m, anywhere: false);
            m.e.style.translate = new Translate(m.x + Mathf.Sin(t * 0.9f + m.phase) * m.sway, m.y);
            m.e.style.opacity = m.alpha * (0.6f + 0.4f * Mathf.Sin(t * 5f + m.phase * 3f));
        }

        if (Controls.Menu.WasPressedThisFrame()) ClosePanel();
    }

    void ClosePanel()
    {
        if (settings.Busy) return; // that key press just cancelled a rebind
        PlayerPrefs.Save();
        panels.Close();
    }

    // ---------- Levels ----------

    void BuildLevels()
    {
        var grid = root.Q("level-grid");
        grid.Clear();
        for (int i = 0; i < levels.Length; i++)
        {
            string level = levels[i];
            bool unlocked = Progress.IsUnlocked(levels, i), done = Progress.IsComplete(level);
            var tile = new Button(() => LoadLevel(level));
            tile.AddToClassList("level-tile");
            tile.AddToClassList("hidden-pop");
            tile.EnableInClassList("level-tile--done", done);
            tile.EnableInClassList("level-tile--locked", !unlocked);
            tile.SetEnabled(unlocked);
            var num = new Label((i + 1).ToString());
            num.AddToClassList("level-num");
            var state = new Label(GameText.Get(done ? "levels.done" : unlocked ? "levels.ready" : "levels.locked"));
            state.AddToClassList("level-state");
            tile.Add(num);
            tile.Add(state);
            if (done)
            {
                int best = Progress.BestTurns(level);
                var bestLabel = new Label(GameText.Get("levels.best", ("turns", GameText.Turns(best))));
                bestLabel.AddToClassList("level-best");
                tile.Add(bestLabel);
            }
            grid.Add(tile);
            After(140 + i * 60, () => tile.RemoveFromClassList("hidden-pop"));
        }
    }

    string NextLevel()
    {
        for (int i = 0; i < levels.Length; i++)
            if (!Progress.IsComplete(levels[i])) return levels[i];
        return levels[0];
    }

    void LoadLevel(string scene)
    {
        if (leaving) return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Scene '{scene}' is not in Build Settings.", this);
            return;
        }
        leaving = true;
        PlayerPrefs.Save();
        fade.pickingMode = PickingMode.Position; // swallow clicks during the fade
        fade.RemoveFromClassList("fade--clear");
        After(1150, () => SceneManager.LoadScene(scene));
    }

    void FillHowTo()
    {
        root.Q<Label>("howto-1").text = GameText.Get("howto.1.body"); // again here, in case the keys were rebound
    }

    void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Helpers ----------

    void After(long ms, System.Action a) => panels.After(ms, a);

    static Texture2D MakeTexture(int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(6, 4, 9, (byte)(255 * Mathf.Clamp01(alpha(x / (w - 1f), y / (h - 1f)))));
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
