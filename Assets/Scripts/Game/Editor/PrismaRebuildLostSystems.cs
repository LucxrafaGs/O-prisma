#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reconstrói na SampleScene tudo que os scripts sabem recriar após perda da cena:
/// NpcWorld (HUD, relógio, clima, DevMode, save, diálogo, NPCs), backpack, câmera,
/// Fonte demo, árvores e garden interact.
/// O tilemap/mapa pintado à mão NÃO pode ser recuperado só pelos scripts.
/// </summary>
public static class PrismaRebuildLostSystems
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Prisma/Rebuild Lost Systems (após perda da cena)")]
    public static void RebuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        StringBuilder log = new();
        log.AppendLine("Prisma rebuild:");

        EnsurePlayer(log);
        EnsureNpcWorld(log);
        EnsureBackpackHost(log);
        EnsureCameraFollow(log);
        EnsureFonte(log);

        // Menus primeiro (abre outras cenas), depois volta à SampleScene.
        EnsureMenuScenes(log);
        PrismaSceneSetup.SetupGameScenes();
        EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        try
        {
            if (WorldTreesSetup.PopulateActiveSampleScene(save: false))
                log.AppendLine("  + World trees (demo)");
            else
                log.AppendLine("  ! World trees: assets ausentes ou cena errada");
        }
        catch (System.Exception ex)
        {
            log.AppendLine($"  ! World trees: {ex.Message}");
        }

        try
        {
            if (GardenInteractSetup.PopulateOrFixGarden(save: false))
                log.AppendLine("  + Garden interact (demo)");
            else
                log.AppendLine("  ! Garden: assets ausentes");
        }
        catch (System.Exception ex)
        {
            log.AppendLine($"  ! Garden: {ex.Message}");
        }

        try
        {
            if (FountainAnimationSetup.SetupFountain(saveScene: false))
                log.AppendLine("  + Fonte animada");
            else
                log.AppendLine("  ! Fonte: sheet/asset ausente ou falhou");
        }
        catch (System.Exception ex)
        {
            log.AppendLine($"  ! Fonte: {ex.Message}");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        log.AppendLine();
        log.AppendLine("Pronto. Entre em Play pelo MainMenu.");
        log.AppendLine("F2 = Dev Mode · ESC = mochila · Tab = hotbar · clique NPC = diálogo.");
        log.AppendLine("Nota: tilemaps/construções pintadas à mão não voltam só com scripts.");
        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog(
            "Prisma — sistemas reconstruídos",
            log.ToString(),
            "OK");
    }

    private static void EnsurePlayer(StringBuilder log)
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = new GameObject("Player");
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<BoxCollider2D>();
            player.AddComponent<PlayerAppearance>();
            player.AddComponent<PlayerController>();
            log.AppendLine("  + Player criado");
        }
        else
        {
            if (player.GetComponent<PlayerAppearance>() == null)
                player.AddComponent<PlayerAppearance>();
            if (player.GetComponent<Rigidbody2D>() == null)
                player.AddComponent<Rigidbody2D>();
            if (player.GetComponent<BoxCollider2D>() == null)
                player.AddComponent<BoxCollider2D>();
            if (player.GetComponent<PlayerController>() == null)
                player.AddComponent<PlayerController>();
            log.AppendLine("  · Player ok");
        }

        CharacterWorldScale.Apply(player.transform);
        try { player.tag = "Player"; }
        catch (UnityException) { /* tag opcional */ }
    }

    private static void EnsureNpcWorld(StringBuilder log)
    {
        NpcWorldBootstrap existing = Object.FindAnyObjectByType<NpcWorldBootstrap>();
        if (existing != null)
        {
            log.AppendLine("  · NpcWorld ok (HUD/tempo/clima/DevMode/NPCs no Play)");
            return;
        }

        GameObject host = new GameObject("NpcWorld");
        host.AddComponent<NpcWorldBootstrap>();
        Undo.RegisterCreatedObjectUndo(host, "Create NpcWorld");
        log.AppendLine("  + NpcWorld (sistemas + NPCs no Play)");
    }

    private static void EnsureBackpackHost(StringBuilder log)
    {
        if (Object.FindAnyObjectByType<PrismaBackpackMenu>() != null
            || Object.FindAnyObjectByType<GamePauseMenu>() != null)
        {
            log.AppendLine("  · Backpack/Pause ok");
            return;
        }

        GameObject host = new GameObject("GamePauseMenu");
        host.AddComponent<GamePauseMenu>();
        Undo.RegisterCreatedObjectUndo(host, "Create GamePauseMenu");
        log.AppendLine("  + GamePauseMenu (mochila ESC)");
    }

    private static void EnsureCameraFollow(StringBuilder log)
    {
        if (Object.FindAnyObjectByType<CinemachineFollowPlayerSetup>() != null)
        {
            log.AppendLine("  · Camera follow ok");
            return;
        }

        GameObject host = new GameObject("CameraFollowSetup");
        CinemachineFollowPlayerSetup setup = host.AddComponent<CinemachineFollowPlayerSetup>();
        SerializedObject so = new SerializedObject(setup);
        GameObject player = GameObject.Find("Player");
        if (player != null)
            so.FindProperty("player").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(host, "Create CameraFollowSetup");
        log.AppendLine("  + CameraFollowSetup");
    }

    private static void EnsureFonte(StringBuilder log)
    {
        GameObject fonte = GameObject.Find("Fonte");
        if (fonte != null)
        {
            log.AppendLine("  · Fonte ok");
            return;
        }

        fonte = new GameObject("Fonte");
        fonte.transform.position = new Vector3(0f, -1.5f, 0f);
        SpriteRenderer renderer = fonte.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 10;
        CircleCollider2D circle = fonte.AddComponent<CircleCollider2D>();
        circle.radius = 0.55f;
        Rigidbody2D body = fonte.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        fonte.AddComponent<CharacterDepthSort>();
        Undo.RegisterCreatedObjectUndo(fonte, "Create Fonte");
        log.AppendLine("  + Fonte (placeholder; animação no passo seguinte)");
    }

    private static void EnsureMenuScenes(StringBuilder log)
    {
        EnsureBootstrapScene("Assets/Scenes/MainMenu.unity", "MainMenu", typeof(MainMenuBootstrap), log);
        EnsureBootstrapScene("Assets/Scenes/SaveSlots.unity", "SaveSlots", typeof(SaveSlotsBootstrap), log);
        EnsureBootstrapScene(
            "Assets/Scenes/CharacterCustomization.unity",
            "CharacterCustomization",
            typeof(CharacterCustomizationBootstrap),
            log);
    }

    private static void EnsureBootstrapScene(
        string path,
        string objectName,
        System.Type bootstrapType,
        StringBuilder log)
    {
        if (!System.IO.File.Exists(path))
        {
            log.AppendLine($"  ! Cena ausente: {path}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Component existing = Object.FindAnyObjectByType(bootstrapType) as Component;
        if (existing != null)
        {
            log.AppendLine($"  · {objectName} bootstrap ok");
            return;
        }

        GameObject root = GameObject.Find(objectName);
        if (root == null)
            root = new GameObject(objectName);

        root.AddComponent(bootstrapType);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine($"  + {objectName} bootstrap");
    }
}
#endif
