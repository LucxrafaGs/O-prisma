#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeMainMenuStarter
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    static PlayModeMainMenuStarter()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        TryAssignPlayModeStartScene();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
            TryAssignPlayModeStartScene();
    }

    private static void TryAssignPlayModeStartScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
        if (scene == null)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        EditorSceneManager.playModeStartScene = scene;
    }
}
#endif
