#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Garante o empty "Sounds of the World" na SampleScene (edit mode).
/// </summary>
public static class SoundsOfTheWorldSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Prisma/Create Sounds of the World")]
    public static void CreateMenu()
    {
        if (EnsureInActiveOrSampleScene(save: true))
            Debug.Log("Prisma: 'Sounds of the World' criado/atualizado na cena.");
    }

    public static bool EnsureInActiveOrSampleScene(bool save)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != SampleScenePath)
        {
            if (save && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            active = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        SoundsOfTheWorld existing = Object.FindAnyObjectByType<SoundsOfTheWorld>();
        if (existing == null)
        {
            GameObject go = new(SoundsOfTheWorld.DefaultObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create Sounds of the World");
            go.AddComponent<SoundsOfTheWorld>();
            EditorSceneManager.MarkSceneDirty(active);
        }
        else if (existing.name != SoundsOfTheWorld.DefaultObjectName)
        {
            Undo.RecordObject(existing.gameObject, "Rename Sounds of the World");
            existing.name = SoundsOfTheWorld.DefaultObjectName;
            EditorSceneManager.MarkSceneDirty(active);
        }

        if (save)
            EditorSceneManager.SaveScene(active);

        return true;
    }
}
#endif
