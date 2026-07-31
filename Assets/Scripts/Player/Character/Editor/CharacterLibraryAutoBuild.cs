#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterLibraryAutoBuild
{
    public const int LibraryBuildVersion = 19;
    private const string VersionKey = "Prisma_CharLibraryBuildVersion";
    private const string TextureVersionKey = "Prisma_CharTextureImportVersion";
    private static readonly string ReloadRequestPath = "Library/prisma-reload.request";

    static CharacterLibraryAutoBuild()
    {
        EditorApplication.delayCall += EnsureProjectUpToDate;
        EditorApplication.update += ProcessReloadRequest;
    }

    private static void ProcessReloadRequest()
    {
        if (!File.Exists(ReloadRequestPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;

        try
        {
            File.Delete(ReloadRequestPath);
            SoftReload();
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"Prisma: falha ao processar reload solicitado. {exception.Message}");
        }
    }

    [MenuItem("Prisma/Reload")]
    public static void SoftReload()
    {
        // Soft only: never force-reimport the ~2500 Player sprites.
        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation();
        Debug.Log("Prisma: soft reload (assets + scripts). Sem reimport forçado de sprites.");
    }

    /// <summary>Compat alias — código antigo / editores que ainda chamam Reload().</summary>
    public static void Reload() => SoftReload();

    [MenuItem("Prisma/Bake Swatch Colors")]
    public static void BakeSwatchColors()
    {
        CharacterSpriteLibrary library = AssetDatabase.LoadAssetAtPath<CharacterSpriteLibrary>(
            "Assets/Resources/CharacterSpriteLibrary.asset");

        if (library == null)
        {
            Debug.LogError("Prisma: CharacterSpriteLibrary nao encontrada.");
            return;
        }

        library.WarmUp();
        library.BakeSwatchColorsForEditor();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log("Prisma: cores dos swatches atualizadas na biblioteca.");
    }

    [MenuItem("Prisma/Force Rebuild Character Library")]
    public static void ForceRebuildLibrary()
    {
        EditorPrefs.SetInt(VersionKey, 0);
        EnsureProjectUpToDate();
    }

    [MenuItem("Prisma/Force Reimport All Player Textures")]
    public static void ForceReimportAllPlayerTextures()
    {
        EditorPrefs.SetInt(TextureVersionKey, 0);
        PlayerTextureImportSettings.ApplyAll(forceReimport: true);
        EditorPrefs.SetInt(TextureVersionKey, PlayerTextureImportSettings.TextureImportVersion);
        EditorPrefs.SetInt(VersionKey, 0);
        EnsureProjectUpToDate();
    }

    private static void EnsureProjectUpToDate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;

        if (EditorPrefs.GetInt(TextureVersionKey, 0) < PlayerTextureImportSettings.TextureImportVersion)
        {
            Debug.Log("Prisma: aplicando import settings só onde faltam (sem force reimport)...");
            PlayerTextureImportSettings.ApplyAll(forceReimport: false);
            EditorPrefs.SetInt(TextureVersionKey, PlayerTextureImportSettings.TextureImportVersion);
            EditorPrefs.SetInt(VersionKey, 0);
        }

        if (EditorPrefs.GetInt(VersionKey, 0) >= LibraryBuildVersion && LibraryHasExpectedEntries())
            return;

        Debug.Log("Prisma: reconstruindo biblioteca de personagem...");
        CharacterSpriteLibraryBuilder.BuildLibrary();
        EditorPrefs.SetInt(VersionKey, LibraryBuildVersion);
    }

    private static bool LibraryHasExpectedEntries()
    {
        CharacterSpriteLibrary library = AssetDatabase.LoadAssetAtPath<CharacterSpriteLibrary>(
            "Assets/Resources/CharacterSpriteLibrary.asset");

        return library != null && library.Entries.Count >= 240;
    }
}
#endif
