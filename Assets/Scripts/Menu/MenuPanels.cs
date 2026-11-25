using UnityEngine.UIElements;

// Shows one modal panel at a time over a dimmed scrim. The motion itself is the .panel--open and
// .scrim--on transitions in MainMenu.uss; this only toggles those classes at the right moments.
public class MenuPanels
{
    readonly VisualElement root, scrim;

    public VisualElement Current { get; private set; }

    public MenuPanels(VisualElement root)
    {
        this.root = root;
        scrim = root.Q("scrim");
    }

    public bool IsOpen(string name) => Current != null && Current.name == name;

    public void Open(string name)
    {
        var panel = root.Q(name);
        if (Current == panel) return;
        if (Current != null) Hide(Current); // switching panels keeps the scrim up
        Current = panel;
        scrim.style.display = DisplayStyle.Flex;
        panel.style.display = DisplayStyle.Flex;
        After(20, () => { scrim.AddToClassList("scrim--on"); panel.AddToClassList("panel--open"); }); // next frame, so the transitions run
        After(60, () => panel.Q<Button>()?.Focus());
    }

    public void Close()
    {
        if (Current == null) return;
        Hide(Current);
        Current = null;
        scrim.RemoveFromClassList("scrim--on");
        After(320, () => { if (Current == null) scrim.style.display = DisplayStyle.None; });
    }

    void Hide(VisualElement panel)
    {
        panel.RemoveFromClassList("panel--open");
        After(320, () => { if (Current != panel) panel.style.display = DisplayStyle.None; });
    }

    // UI Toolkit's scheduler runs on real time, so this keeps working while the game is paused.
    public void After(long ms, System.Action action) => root.schedule.Execute(action).StartingIn(ms);
}
