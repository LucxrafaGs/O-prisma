using UnityEngine;

/// <summary>
/// Aplica <see cref="SeasonalTree"/> em todas as árvores da cena (World / World 2 / Arvores),
/// sem alterar posição. Player e NPCs usam <see cref="CharacterDepthSort"/> no mesmo eixo Y;
/// a copa vai para a sorting layer Foliage (sempre na frente).
/// </summary>
[DefaultExecutionOrder(-42)]
public class TreeDepthSplitBootstrap : MonoBehaviour
{
    private static bool appliedThisLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        appliedThisLoad = false;
        ApplyAll();

        GameObject runner = new("TreeDepthSplitRunner");
        Object.DontDestroyOnLoad(runner);
        runner.hideFlags = HideFlags.HideAndDontSave;
        runner.AddComponent<TreeDepthSplitBootstrap>();
    }

    private void Start()
    {
        ApplyAll();
        Destroy(gameObject);
    }

    public static int ApplyAll()
    {
        int added = 0;
        int refreshed = 0;

        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            GameObject go = renderer.gameObject;
            if (!IsTreeCandidate(go))
                continue;

            SeasonalTree existing = go.GetComponent<SeasonalTree>();
            if (existing != null)
            {
                existing.SetupTree();
                refreshed++;
                continue;
            }

            if (ApplyNew(go))
                added++;
        }

        if (added > 0 || refreshed > 0)
        {
            Debug.Log(
                $"Prisma: árvores depth-split — novas={added}, atualizadas={refreshed} " +
                "(folhas na layer Foliage; tronco colide; posições intactas).");
        }
        else if (!appliedThisLoad)
        {
            Debug.LogWarning(
                "Prisma: nenhuma árvore encontrada (pasta Arvores / objetos Arvore*).");
        }

        appliedThisLoad = true;
        return added;
    }

    private static bool IsArvoresFolderName(string n)
    {
        return n == "Arvores" || n == "Árvores" ||
               n == "Arvores 2" || n == "Árvores 2";
    }

    /// <summary>
    /// Instância de árvore (Arvore, Arvore (1), Tree_x) — NÃO a pasta "Arvores".
    /// Bug anterior: "Arvores".StartsWith("Arvore") era true e excluía todos os filhos.
    /// </summary>
    private static bool IsTreeInstanceName(string n)
    {
        if (string.IsNullOrEmpty(n) || IsArvoresFolderName(n))
            return false;

        if (n.StartsWith("Tree_"))
            return true;

        // "Arvore" / "Arvore (27)" — mas não "Arvores..."
        if (n.StartsWith("Arvore") && !n.StartsWith("Arvores"))
            return true;

        if (n.StartsWith("Árvore") && !n.StartsWith("Árvores"))
            return true;

        return false;
    }

    private static bool IsTreeCandidate(GameObject go)
    {
        if (go == null)
            return false;

        string name = go.name;
        if (name == "Sombra" ||
            name == SeasonalTree.CanopyChildName ||
            name == PropDepthSplit.TopChildName)
            return false;

        // Evita aplicar no Canopy/Sombra filho de uma árvore (não na pasta Arvores).
        if (go.transform.parent != null && IsTreeInstanceName(go.transform.parent.name))
            return false;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
            return false;

        if (go.GetComponent<PlayerController>() != null)
            return false;
        if (go.GetComponent<NpcController>() != null)
            return false;
        if (go.GetComponent<ElasticFoliage>() != null)
            return false;

        // Filho direto da pasta Arvores
        if (go.transform.parent != null && IsArvoresFolderName(go.transform.parent.name))
            return true;

        // Qualquer Arvore* / Tree_* sob World
        if (!IsTreeInstanceName(name))
            return false;

        Transform t = go.transform;
        while (t != null)
        {
            if (t.name == "World" || t.name.StartsWith("World "))
                return true;
            t = t.parent;
        }

        return false;
    }

    private static bool ApplyNew(GameObject go)
    {
        PropDepthSplit propSplit = go.GetComponent<PropDepthSplit>();
        if (propSplit != null)
        {
            if (Application.isPlaying)
                Object.Destroy(propSplit);
            else
                Object.DestroyImmediate(propSplit);
        }

        SeasonalTree tree = go.AddComponent<SeasonalTree>();
        tree.SetupTree();
        return true;
    }
}
