using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// The Settings panel (Assets/UI/Settings.uxml), shared by the main menu and the pause screen.
// Each owner wires the panel's Back button itself, since "back" means something different in each.
public class SettingsPanel
{
    static readonly string[] Tabs = { "audio", "graphics", "controls" };

    readonly VisualElement root, underline;
    readonly MenuPanels panels;
    string activeTab = "audio";
    int rebindEndFrame = -1;

    // True while waiting for a key, and on the frame a rebind ends, so the Esc that cancels it doesn't also close a panel.
    public bool Busy => Controls.Rebinding || Time.frameCount <= rebindEndFrame + 1;

    public SettingsPanel(VisualElement root, MenuPanels panels)
    {
        this.root = root;
        this.panels = panels;
        underline = root.Q("tab-underline");

        foreach (var id in Tabs) root.Q<Button>("tab-" + id).clicked += () => ShowTab(id);
        root.Q<Button>("tab-audio").RegisterCallback<GeometryChangedEvent>(_ => MoveUnderline());

        string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
        Slider("vol-master", GameSettings.Master, v => GameSettings.Master = v, Percent);
        Slider("vol-music", GameSettings.Music, v => { GameSettings.Music = v; Sfx.UpdateMusicVolume(); }, Percent);
        float lastBlip = 0f;
        Slider("vol-sfx", GameSettings.Sfx, v =>
        {
            GameSettings.Sfx = v;
            if (Time.unscaledTime - lastBlip < 0.12f) return; // dragging fires every frame; one blip per 0.12 s
            lastBlip = Time.unscaledTime;
            Sfx.Play(Sound.Pick);
        }, Percent);
        Slider("mouse-sens", GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.0") + "x");

        var quality = root.Q<DropdownField>("gfx-quality");
        quality.choices = QualitySettings.names.ToList();
        quality.index = GameSettings.Quality;
        quality.RegisterValueChangedCallback(_ => GameSettings.Quality = quality.index);

        var sizes = Screen.resolutions.Select(r => (r.width, r.height)).Append((Screen.width, Screen.height)).Distinct()
            .OrderByDescending(r => r.width * r.height).ToList();
        var resolution = root.Q<DropdownField>("gfx-resolution");
        resolution.choices = sizes.Select(r => $"{r.width} x {r.height}").ToList();
        resolution.index = sizes.IndexOf((Screen.width, Screen.height));
        resolution.RegisterValueChangedCallback(_ =>
        {
            var (w, h) = sizes[resolution.index];
            Screen.SetResolution(w, h, Screen.fullScreenMode);
        });

        var fullscreen = root.Q<Toggle>("gfx-fullscreen");
        fullscreen.value = GameSettings.Fullscreen;
        fullscreen.RegisterValueChangedCallback(e => GameSettings.Fullscreen = e.newValue);
        var vsync = root.Q<Toggle>("gfx-vsync");
        vsync.value = GameSettings.VSync;
        vsync.RegisterValueChangedCallback(e => GameSettings.VSync = e.newValue);
        var shake = root.Q<Toggle>("gfx-shake");
        shake.value = GameSettings.ScreenShake;
        shake.RegisterValueChangedCallback(e => GameSettings.ScreenShake = e.newValue);

        root.Q<Button>("reset-bindings").clicked += () =>
        {
            if (Controls.Rebinding) return;
            Controls.ResetToDefaults();
            BuildBindings();
        };
        BuildBindings();
    }

    public void Open()
    {
        panels.Open("settings-panel");
        ShowTab(activeTab);
    }

    void Slider(string name, float value, System.Action<float> set, System.Func<float, string> format)
    {
        var slider = root.Q<Slider>(name);
        slider.fill = true;
        var readout = new Label(format(value));
        readout.AddToClassList("value-label");
        slider.Add(readout);
        slider.SetValueWithoutNotify(value);
        slider.RegisterValueChangedCallback(e => { set(e.newValue); readout.text = format(e.newValue); });
    }

    void ShowTab(string id)
    {
        activeTab = id;
        foreach (var t in Tabs)
        {
            root.Q("tab-" + t).EnableInClassList("tab--active", t == id);
            var page = root.Q("page-" + t);
            page.EnableInClassList("page--shown", t == id);
            page.RemoveFromClassList("page--in");
        }
        panels.After(20, () => root.Q("page-" + id).AddToClassList("page--in"));
        MoveUnderline();
    }

    void MoveUnderline()
    {
        var tab = root.Q("tab-" + activeTab).layout;
        underline.style.left = tab.x;
        underline.style.width = tab.width;
    }

    void BuildBindings()
    {
        var list = root.Q("bindings");
        list.Clear();
        foreach (var action in Controls.Rebindable)
        {
            var row = new VisualElement();
            row.AddToClassList("binding-row");
            var label = new Label(GameText.Get("control." + action.name));
            label.AddToClassList("binding-name");
            var key = new Button { text = Controls.KeyName(action) };
            key.AddToClassList("key-btn");
            key.clicked += () =>
            {
                if (Controls.Rebinding) return;
                key.text = GameText.Get("settings.listening");
                key.AddToClassList("key-btn--listening");
                Controls.Rebind(action, () =>
                {
                    rebindEndFrame = Time.frameCount;
                    key.RemoveFromClassList("key-btn--listening");
                    key.text = Controls.KeyName(action);
                });
            };
            row.Add(label);
            row.Add(key);
            list.Add(row);
        }
    }
}
