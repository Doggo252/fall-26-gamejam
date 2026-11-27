using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Every player-facing string lives in Assets/Resources/Text.txt ("key = text" lines).
// UXML refers to a line as text="#key"; code calls GameText.Get("key").
public static class GameText
{
    static Dictionary<string, string> lines;

    // Domain reload is off, so drop the cache each play session or edits to Text.txt wouldn't show.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reload() => lines = null;

    public static Dictionary<string, string> Lines => lines ??= Parse(Resources.Load<TextAsset>("Text").text);

    public static Dictionary<string, string> Parse(string file)
    {
        var result = new Dictionary<string, string>();
        foreach (var raw in file.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (line.Length == 0 || line[0] == '#' || eq < 0) continue;
            result[line[..eq].Trim()] = line[(eq + 1)..].Trim().Replace("\\n", "\n");
        }
        return result;
    }

    // The line for key, with key bindings and any extra {name} values filled in.
    public static string Get(string key, params (string name, object value)[] fill)
    {
        if (!Lines.TryGetValue(key, out var s))
        {
            Debug.LogWarning($"Text.txt has no line for \"{key}\".");
            return key;
        }
        var options = s.Split(" | "); // a few ways to say it: pick one, so repeat lines don't feel canned
        s = options[Random.Range(0, options.Length)];
        string click = Controls.SelectVerb;
        s = s.Replace("{Click}", click).Replace("{click}", char.ToLower(click[0]) + click[1..])
            .Replace("{wait}", Controls.KeyName(Controls.Wait))
            .Replace("{restart}", Controls.KeyName(Controls.Restart))
            .Replace("{pan}", Controls.KeyName(Controls.Pan));
        foreach (var (name, value) in fill) s = s.Replace("{" + name + "}", value.ToString());
        return s;
    }

    // "1 turn", "5 turns", with the words from Text.txt.
    public static string Turns(int n) => $"{n} {Get(n == 1 ? "word.turn" : "word.turns")}";

    // Swaps every "#key" label and button in a UI tree for its line. An empty line hides the label.
    public static void Apply(VisualElement root) =>
        root.Query<TextElement>().ForEach(t =>
        {
            if (!t.text.StartsWith("#")) return;
            t.text = Get(t.text[1..]);
            if (t.text == "") t.style.display = DisplayStyle.None;
        });
}
