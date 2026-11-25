using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Gameplay input the player can rebind from Settings. Overrides persist in PlayerPrefs.
public static class Controls
{
    const string PrefsKey = "controls.bindings";

    public static readonly InputActionMap Map = new("Gameplay");
    public static readonly InputAction Select = Map.AddAction("Select", InputActionType.Button, "<Mouse>/leftButton");
    public static readonly InputAction Wait = Map.AddAction("Wait", InputActionType.Button, "<Keyboard>/space");
    public static readonly InputAction Restart = Map.AddAction("Restart", InputActionType.Button, "<Keyboard>/r");
    public static readonly InputAction Menu = Map.AddAction("Menu", InputActionType.Button, "<Keyboard>/escape");
    public static readonly InputAction Pan = Map.AddAction("Pan", InputActionType.Button, "<Mouse>/rightButton");

    public static readonly InputAction[] Rebindable = { Select, Wait, Restart, Pan, Menu };

    public static bool Rebinding { get; private set; }

    static Controls()
    {
        var json = PlayerPrefs.GetString(PrefsKey, "");
        if (json != "") Map.LoadBindingOverridesFromJson(json);
        Map.Enable();
    }

    // Domain reload is off, so the static constructor runs once per editor session, but the Input System
    // disables every action when play mode exits. Without this, clicks and keys are dead from the second play on.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnableOnPlay()
    {
        Rebinding = false;
        Map.Enable();
    }

    public static string KeyName(InputAction a) => a.GetBindingDisplayString(0);

    // "Click" with the default mouse binding, otherwise "Press X on", so hints read naturally either way.
    public static string SelectVerb => Select.bindings[0].effectivePath == "<Mouse>/leftButton" ? "Click" : $"Press {KeyName(Select)} on";

    // Waits for the next key or button press and binds it to the action. Escape cancels.
    public static void Rebind(InputAction action, Action done)
    {
        Rebinding = true;
        action.Disable();
        action.PerformInteractiveRebinding(0)
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => Finish(op))
            .OnCancel(op => Finish(op))
            .Start();

        void Finish(InputActionRebindingExtensions.RebindingOperation op)
        {
            op.Dispose();
            action.Enable();
            Save();
            Rebinding = false;
            done();
        }
    }

    public static void ResetToDefaults()
    {
        Map.RemoveAllBindingOverrides();
        Save();
    }

    static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, Map.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }
}
