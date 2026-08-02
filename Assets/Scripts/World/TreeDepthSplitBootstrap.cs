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

        // Segunda passagem no 1º frame — cobre árvores spawnadas no Awake de outros sistemas.
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
                "Prisma: nenhuma árvore encontrada (procure World 2 → Arvores). " +
                "Salve a SampleScene se World 2 só existir no editor.");
        }

        appliedThisLoad = true;
        return added;
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

        // Filho Canopy / sombra não é árvore-raiz.
        if (go.transform.parent != null)
        {
            string parentName = go.transform.parent.name;
            if (parentName.StartsWith("Arvore") || parentName.StartsWith("Árvore") || parentName.StartsWith("Tree_"))
                return false;
        }

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
            return false;

        if (go.GetComponent<PlayerController>() != null)
            return false;
        if (go.GetComponent<NpcController>() != null)
            return false;
        if (go.GetComponent<ElasticFoliage>() != null)
            return false;

        bool nameLooksLikeTree =
            name.StartsWith("Arvore") ||
            name.StartsWith("Árvore") ||
            name.StartsWith("Tree_");

        bool underArvoresFolder = false;
        Transform t = go.transform;
        while (t != null)
        {
            string n = t.name;
            if (n == "Arvores" || n == "Árvores" || n == "Arvores 2" || n == "Árvores 2")
            {
                underArvoresFolder = true;
                break;
            }

            t = t.parent;
        }

        // Direto sob pasta Arvores, ou qualquer "Arvore*" no World.
        if (underArvoresFolder)
            return go.transform.parent != null &&
                   (go.transform.parent.name == "Arvores" ||
                    go.transform.parent.name == "Árvores" ||
                    go.transform.parent.name == "Arvores 2" ||
                    go.transform.parent.name == "Árvores 2");

        if (!nameLooksLikeTree)
            return false;

        // Arvore* solta sob World / World 2
        t = go.transform;
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
