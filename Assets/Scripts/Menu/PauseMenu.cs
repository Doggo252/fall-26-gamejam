using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// In-level UI (PauseMenu.uxml): the pause screen, the win/lose panel, the hint card and the key reminders.
// Also fades the level in on load. Put the PauseUI prefab in every level scene.
[RequireComponent(typeof(UIDocument))]
public class PauseMenu : MonoBehaviour
{
    public static bool Paused { get; private set; }

    static PauseMenu instance;

    VisualElement root, fade, hint;
    MenuPanels panels;
    SettingsPanel settings;
    bool leaving, showingResult, rolling;
    System.Action hintAction;
    string nextLevel;

    void Start()
    {
        instance = this;
        root = GetComponent<UIDocument>().rootVisualElement;
        fade = root.Q("fade");
        GameText.Apply(root);
        panels = new MenuPanels(root);
        settings = new SettingsPanel(root, panels);

        root.Q<Button>("pause-btn").clicked += Pause;
        root.Q<Button>("resume").clicked += Resume;
        root.Q<Button>("restart").clicked += () => Leave(SceneManager.GetActiveScene().name);
        root.Q<Button>("pause-settings").clicked += settings.Open;
        root.Q<Button>("to-menu").clicked += () => Leave("Main Menu");
        root.Q<Button>("settings-back").clicked += () => { PlayerPrefs.Save(); panels.Open("pause-panel"); };
        root.Q<Button>("result-next").clicked += () => { if (nextLevel == null) RollCredits(); else Leave(nextLevel); };
        root.Q<Button>("help-btn").clicked += () => FindAnyObjectByType<DungeonGame>()?.ShowHint();
        root.Q<Button>("result-retry").clicked += () => Leave(SceneManager.GetActiveScene().name);
        root.Q<Button>("result-menu").clicked += () => Leave("Main Menu");
        hint = root.Q("hint");
        root.Q<Button>("hint-btn").clicked += () => hintAction?.Invoke();
        root.Q<Label>("keys").text = GameText.Get("keys");

        panels.After(50, () => fade.AddToClassList("fade--clear")); // the menu faded out to black, so fade back in
    }

    void Update()
    {
        if (rolling)
        {
            if (Controls.Menu.WasPressedThisFrame()) Leave("Main Menu");
            return;
        }
        if (!Controls.Menu.WasPressedThisFrame() || settings.Busy || leaving || showingResult) return;
        if (panels.IsOpen("settings-panel")) panels.Open("pause-panel");
        else if (Paused) Resume();
        else Pause();
    }

    void Pause()
    {
        if (Paused || leaving || showingResult) return;
        Paused = true;
        Time.timeScale = 0f;
        Sfx.UpdateMusicVolume();
        var game = FindAnyObjectByType<DungeonGame>();
        string level = SceneManager.GetActiveScene().name;
        int best = Progress.BestTurns(level);
        root.Q<Label>("pause-info").text = GameText.Get("pause.info",
            ("level", Progress.DisplayName(level)), ("turn", game ? game.Turns : 0), ("best", best > 0 ? GameText.Get("pause.best", ("best", best)) : GameText.Get("pause.nobest")));
        panels.Open("pause-panel");
    }

    void Resume()
    {
        Paused = false;
        Time.timeScale = 1f;
        Sfx.UpdateMusicVolume();
        PlayerPrefs.Save();
        panels.Close();
    }

    void Leave(string scene)
    {
        if (leaving) return;
        leaving = true;
        PlayerPrefs.Save();
        fade.pickingMode = PickingMode.Position; // swallow clicks during the fade
        fade.RemoveFromClassList("fade--clear");
        panels.After(700, () => SceneManager.LoadScene(scene));
    }

    // The left-hand card. Null text hides it; a button label adds a button that runs onButton; an icon shows beside the title.
    public static void Hint(string title, string text, string button = null, System.Action onButton = null, Sprite icon = null, Color iconTint = default)
    {
        if (instance == null) return;
        var p = instance;
        p.hint.EnableInClassList("hint--hidden", string.IsNullOrEmpty(text));
        if (string.IsNullOrEmpty(text)) return; // keep the old text while it fades out
        p.root.Q<Label>("hint-title").text = title;
        p.root.Q<Label>("hint-title").style.display = string.IsNullOrEmpty(title) ? DisplayStyle.None : DisplayStyle.Flex;
        p.root.Q<Label>("hint-text").text = text;
        var iconBox = p.root.Q("hint-icon");
        iconBox.style.display = icon == null ? DisplayStyle.None : DisplayStyle.Flex;
        iconBox.style.backgroundImage = icon == null ? StyleKeyword.None : new StyleBackground(icon);
        iconBox.style.unityBackgroundImageTintColor = iconTint;
        var btn = p.root.Q<Button>("hint-btn");
        btn.text = button;
        btn.style.display = button == null ? DisplayStyle.None : DisplayStyle.Flex;
        p.hintAction = onButton;
    }

    // The HUD Hint button: "Hint", or a countdown while it's cooling down (secondsLeft > 0).
    public static void HintWait(int secondsLeft)
    {
        if (instance == null) return;
        var btn = instance.root.Q<Button>("help-btn");
        btn.text = GameText.Get(secondsLeft > 0 ? "hud.hint.wait" : "hud.hint", ("n", secondsLeft));
        btn.SetEnabled(secondsLeft <= 0);
    }

    // Win/lose panel, shown once the hero has finished its last glide. next: the level after this one, or null.
    // stars: 1 to 3 for a win.
    public static void ShowResult(bool won, string body, string next, int stars = 0)
    {
        if (instance == null) return;
        var p = instance;
        p.showingResult = true;
        p.nextLevel = next;
        p.root.Q<Label>("result-title").text = GameText.Get(won ? "result.won.title" : "result.lost.title");
        p.root.Q<Label>("result-body").text = body;
        p.root.Q<Button>("result-next").style.display = won ? DisplayStyle.Flex : DisplayStyle.None;
        p.root.Q<Button>("result-next").text = GameText.Get(next != null ? "result.next" : "result.credits");
        p.root.Q<Button>("result-retry").text = GameText.Get(won ? "result.replay" : "result.retry");
        p.root.Q<Button>("result-menu").style.display = won && next == null ? DisplayStyle.None : DisplayStyle.Flex;
        var row = p.root.Q("result-stars");
        row.style.display = won ? DisplayStyle.Flex : DisplayStyle.None;
        Stars.Fill(row, stars, 46f);
        for (int i = 0; i < stars; i++) row[i].style.scale = new Scale(Vector2.zero);
        p.panels.After(700, () =>
        {
            p.panels.Open("result-panel");
            if (!won) return;
            p.Confetti();
            for (int i = 0; i < stars; i++)
            {
                var star = row[i];
                float pitch = 1f + i * 0.25f;
                p.panels.After(350 + i * 260, () => { star.style.scale = new Scale(Vector2.one); Sfx.Play(Sound.Pop, pitch); });
            }
        });
    }

    class Bit { public VisualElement e; public Vector2 pos, vel; public float spin, angle; }

    // Paper bits flutter down over the win screen.
    void Confetti()
    {
        Color[] colors = { new(0.95f, 0.78f, 0.36f), new(0.82f, 0.23f, 0.18f), new(0.48f, 0.83f, 0.54f), new(0.4f, 0.6f, 1f), new(0.8f, 0.5f, 1f) };
        var layer = fade.parent;
        var size = layer.layout.size;
        var bits = new System.Collections.Generic.List<Bit>();
        for (int i = 0; i < 140; i++)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.width = Random.Range(8f, 16f);
            e.style.height = Random.Range(5f, 9f);
            e.style.backgroundColor = colors[Random.Range(0, colors.Length)];
            layer.Insert(layer.IndexOf(fade), e); // under the fade, over everything else
            bits.Add(new Bit { e = e, pos = new Vector2(Random.Range(0f, size.x), Random.Range(-size.y * 0.6f, -10f)),
                vel = new Vector2(Random.Range(-80f, 80f), Random.Range(50f, 250f)), spin = Random.Range(-500f, 500f) });
        }
        float last = Time.unscaledTime;
        root.schedule.Execute(() =>
        {
            float dt = Time.unscaledTime - last;
            last = Time.unscaledTime;
            foreach (var b in bits)
            {
                b.vel += new Vector2(Mathf.Sin(b.angle * 0.02f) * 60f, 220f) * dt; // flutter side to side while falling
                b.vel.y = Mathf.Min(b.vel.y, 320f);
                b.pos += b.vel * dt;
                b.angle += b.spin * dt;
                b.e.style.left = b.pos.x;
                b.e.style.top = b.pos.y;
                b.e.style.rotate = new Rotate(new Angle(b.angle, AngleUnit.Degree));
            }
            bits.RemoveAll(b => { if (b.pos.y < size.y + 20f) return false; b.e.RemoveFromHierarchy(); return true; });
        }).Every(16).Until(() => bits.Count == 0);
    }

    // After the last level: the credits scroll up while the hero trudges off, then back to the main menu.
    // Click or Esc skips it.
    void RollCredits()
    {
        if (leaving || rolling) return;
        rolling = true;
        panels.Close();
        var roll = root.Q("roll");
        var text = root.Q("roll-text");
        var hero = root.Q("roll-hero");
        var game = FindAnyObjectByType<DungeonGame>();
        if (game != null) hero.style.backgroundImage = new StyleBackground(game.hero.GetComponent<SpriteRenderer>().sprite);
        roll.style.display = DisplayStyle.Flex;
        roll.RegisterCallback<ClickEvent>(_ => Leave("Main Menu"));
        Sfx.Play(Sound.Lose);
        const float length = 18f;
        float start = Time.unscaledTime;
        root.schedule.Execute(() =>
        {
            float t = Time.unscaledTime - start, k = t / length;
            text.style.top = Mathf.Lerp(roll.layout.height, -text.layout.height, k);
            hero.style.left = Mathf.Lerp(-120f, roll.layout.width + 120f, k);
            hero.style.translate = new Translate(0f, -6f * Mathf.Abs(Mathf.Sin(t * 3f))); // slow, sad steps
            if (t >= length) Leave("Main Menu");
        }).Every(16).Until(() => leaving);
    }

    void OnDestroy()
    {
        // Never carry a frozen clock into the next scene.
        Paused = false;
        Time.timeScale = 1f;
        Sfx.UpdateMusicVolume();
        if (instance == this) instance = null;
    }

    // True when a screen position (Input System coordinates, origin bottom-left) is over pause-screen UI,
    // so the game can ignore that click.
    public static bool IsOverUI(Vector2 screenPos)
    {
        if (instance == null || instance.root?.panel == null) return false;
        var panel = instance.root.panel;
        var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y)));
        return picked is Button || picked?.GetFirstAncestorOfType<Button>() != null; // only buttons count, not the empty full-screen root
    }
}
