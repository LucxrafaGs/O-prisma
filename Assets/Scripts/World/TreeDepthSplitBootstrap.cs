using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica <see cref="SeasonalTree"/> em árvores da cena sem mover posição.
/// Roda em <b>toda</b> cena carregada (MainMenu → SampleScene inclusive).
/// </summary>
[DefaultExecutionOrder(-42)]
public class TreeDepthSplitBootstrap : MonoBehaviour
{
    private static TreeDepthSplitBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureListener();
        ApplyAll();
    }

    private static void EnsureListener()
    {
        if (instance != null)
            return;

        GameObject go = new("TreeDepthSplitRunner");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        instance = go.AddComponent<TreeDepthSplitBootstrap>();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    public static int ApplyAll()
    {
        EnsureListener();

        int added = 0;
        int refreshed = 0;
        int scanned = 0;

        // 1) Filhos diretos de qualquer pasta "Arvores" (caminho mais confiável).
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform folder = transforms[i];
            if (folder == null || !IsArvoresFolderName(folder.name))
                continue;

            for (int c = 0; c < folder.childCount; c++)
            {
                Transform child = folder.GetChild(c);
                if (child == null)
                    continue;

                scanned++;
                ApplyOrRefresh(child.gameObject, ref added, ref refreshed);
            }
        }

        // 2) Qualquer objeto "Arvore*" / "Tree_*" com sprite (caso fique fora da pasta).
        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            GameObject go = renderer.gameObject;
            if (!IsTreeInstanceName(go.name))
                continue;
            if (go.transform.parent != null && IsTreeInstanceName(go.transform.parent.name))
                continue; // Canopy/filho de outra árvore

            scanned++;
            ApplyOrRefresh(go, ref added, ref refreshed);
        }

        if (added > 0 || refreshed > 0)
        {
            Debug.Log(
                $"Prisma: árvores depth-split — novas={added}, atualizadas={refreshed}, " +
                $"candidatas={scanned} (folhas=Foliage; tronco colide).");
        }
        else
        {
            Debug.LogWarning(
                $"Prisma: nenhuma árvore encontrada (scanned={scanned}). " +
                "Confira se existe um empty 'Arvores' com filhos na cena ativa.");
        }

        return added + refreshed;
    }

    private static void ApplyOrRefresh(GameObject go, ref int added, ref int refreshed)
    {
        if (go == null)
            return;

        if (go.name == "Sombra" ||
            go.name == SeasonalTree.CanopyChildName ||
            go.name == PropDepthSplit.TopChildName)
            return;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
            return;

        if (go.GetComponent<PlayerController>() != null)
            return;
        if (go.GetComponent<NpcController>() != null)
            return;
        if (go.GetComponent<ElasticFoliage>() != null)
            return;

        SeasonalTree existing = go.GetComponent<SeasonalTree>();
        if (existing != null)
        {
            existing.SetupTree();
            refreshed++;
            return;
        }

        PropDepthSplit propSplit = go.GetComponent<PropDepthSplit>();
        if (propSplit != null)
            Object.Destroy(propSplit);

        SeasonalTree tree = go.AddComponent<SeasonalTree>();
        tree.SetupTree();
        added++;
    }

    private static bool IsArvoresFolderName(string n)
    {
        if (string.IsNullOrEmpty(n))
            return false;

        // Aceita "Arvores", "Árvores", "Arvores 2", etc.
        string trimmed = n.Trim();
        return trimmed == "Arvores" || trimmed == "Árvores" ||
               trimmed.StartsWith("Arvores ") || trimmed.StartsWith("Árvores ") ||
               trimmed == "Arvores 2" || trimmed == "Árvores 2";
    }

    private static bool IsTreeInstanceName(string n)
    {
        if (string.IsNullOrEmpty(n) || IsArvoresFolderName(n))
            return false;

        if (n.StartsWith("Tree_"))
            return true;

        // "Arvore", "Arvore (1)", "Arvore 01" — nunca a pasta "Arvores"
        if (n.StartsWith("Arvore") && !n.StartsWith("Arvores"))
            return true;

        if (n.StartsWith("Árvore") && !n.StartsWith("Árvores"))
            return true;

        return false;
    }
}
