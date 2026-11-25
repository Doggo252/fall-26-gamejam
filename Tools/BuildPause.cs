using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

// Creates Assets/UI/PauseUI.prefab and places one in Main Scene (replacing any old copy). Run with:
// unity command script run --file Tools/BuildPause.cs --entry BuildPause.Run
// New level scenes need the prefab too: drag Assets/UI/PauseUI.prefab into them.
public static class BuildPause
{
    const string PrefabPath = "Assets/UI/PauseUI.prefab";

    public static string Run()
    {
        // Open first: reopening a scene unloads assets nothing references.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main Scene.unity");

        var go = new GameObject("PauseUI");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/MenuPanelSettings.asset");
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/PauseMenu.uxml");
        go.AddComponent<PauseMenu>();
        go.AddComponent<EventSystem>(); // level scenes have no EventSystem of their own
        go.AddComponent<InputSystemUIInputModule>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "PauseUI") Object.DestroyImmediate(root);
        PrefabUtility.InstantiatePrefab(prefab, scene);
        EditorSceneManager.SaveScene(scene);
        var saved = prefab.GetComponent<UIDocument>();
        return $"PauseUI prefab saved ({(saved.panelSettings && saved.visualTreeAsset ? "references ok" : "MISSING references")})";
    }
}
