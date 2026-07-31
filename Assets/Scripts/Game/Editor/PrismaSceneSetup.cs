#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrismaSceneSetup
{
    private const string ScenesFolder = "Assets/Scenes";

    [InitializeOnLoadMethod]
    private static void EnsureScenesOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!File.Exists($"{ScenesFolder}/MainMenu.unity") || !File.Exists($"{ScenesFolder}/SaveSlots.unity"))
                SetupGameScenes();
        };
    }

    [MenuItem("Prisma/Setup Game Scenes")]
    public static void SetupGameScenes()
    {
        Directory.CreateDirectory(ScenesFolder);

        CreateBootstrapScene("MainMenu", typeof(MainMenuBootstrap));
        CreateBootstrapScene("SaveSlots", typeof(SaveSlotsBootstrap));
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Prisma: cenas MainMenu e SaveSlots criadas. Build Settings atualizado.");
    }

    private static void CreateBootstrapScene(string sceneName, System.Type bootstrapType)
    {
        string scenePath = $"{ScenesFolder}/{sceneName}.unity";
        if (File.Exists(scenePath))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = new GameObject(sceneName);
        root.AddComponent(bootstrapType);

        Camera camera = Object.FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 1f);

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static void UpdateBuildSettings()
    {
        string[] scenePaths =
        {
            $"{ScenesFolder}/MainMenu.unity",
            $"{ScenesFolder}/CharacterCustomization.unity",
            $"{ScenesFolder}/SaveSlots.unity",
            $"{ScenesFolder}/SampleScene.unity"
        };

        var scenes = new EditorBuildSettingsScene[scenePaths.Length];
        for (int i = 0; i < scenePaths.Length; i++)
        {
            scenes[i] = new EditorBuildSettingsScene(scenePaths[i], File.Exists(scenePaths[i]));
        }

        EditorBuildSettings.scenes = scenes;
    }
}
#endif
