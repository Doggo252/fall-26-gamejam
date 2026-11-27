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
    bool leaving, showingResult;
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
        root.Q<Button>("result-next").clicked += () => Leave(nextLevel ?? "Main Menu");
        root.Q<Button>("result-retry").clicked += () => Leave(SceneManager.GetActiveScene().name);
        root.Q<Button>("result-menu").clicked += () => Leave("Main Menu");
        hint = root.Q("hint");
        root.Q<Button>("hint-btn").clicked += () => hintAction?.Invoke();
        root.Q<Label>("keys").text = GameText.Get("keys");

        panels.After(50, () => fade.AddToClassList("fade--clear")); // the menu faded out to black, so fade back in
    }

    void Update()
    {
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
        var game = FindAnyObjectByType<DungeonGame>();
        string level = SceneManager.GetActiveScene().name;
        int best = Progress.BestTurns(level);
        root.Q<Label>("pause-info").text = GameText.Get("pause.info",
            ("level", Progress.DisplayName(level)), ("turn", game ? game.Turns : 0), ("best", best > 0 ? best.ToString() : GameText.Get("pause.nobest")));
        panels.Open("pause-panel");
    }

    void Resume()
    {
        Paused = false;
        Time.timeScale = 1f;
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

    // The left-hand card. Null text hides it; a button label adds a button that runs onButton.
    public static void Hint(string title, string text, string button = null, System.Action onButton = null)
    {
        if (instance == null) return;
        var p = instance;
        p.hint.EnableInClassList("hint--hidden", string.IsNullOrEmpty(text));
        if (string.IsNullOrEmpty(text)) return; // keep the old text while it fades out
        p.root.Q<Label>("hint-title").text = title;
        p.root.Q<Label>("hint-title").style.display = string.IsNullOrEmpty(title) ? DisplayStyle.None : DisplayStyle.Flex;
        p.root.Q<Label>("hint-text").text = text;
        var btn = p.root.Q<Button>("hint-btn");
        btn.text = button;
        btn.style.display = button == null ? DisplayStyle.None : DisplayStyle.Flex;
        p.hintAction = onButton;
    }

    // Win/lose panel, shown once the hero has finished its last glide. next: the level after this one, or null.
    public static void ShowResult(bool won, string body, string next)
    {
        if (instance == null) return;
        var p = instance;
        p.showingResult = true;
        p.nextLevel = next;
        p.root.Q<Label>("result-title").text = GameText.Get(won ? "result.won.title" : "result.lost.title");
        p.root.Q<Label>("result-body").text = body;
        p.root.Q<Button>("result-next").style.display = won ? DisplayStyle.Flex : DisplayStyle.None;
        p.root.Q<Button>("result-next").text = GameText.Get(next != null ? "result.next" : "result.finish");
        p.root.Q<Button>("result-retry").text = GameText.Get(won ? "result.replay" : "result.retry");
        p.root.Q<Button>("result-menu").style.display = won && next == null ? DisplayStyle.None : DisplayStyle.Flex;
        p.panels.After(700, () => p.panels.Open("result-panel"));
    }

    void OnDestroy()
    {
        // Never carry a frozen clock into the next scene.
        Paused = false;
        Time.timeScale = 1f;
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
