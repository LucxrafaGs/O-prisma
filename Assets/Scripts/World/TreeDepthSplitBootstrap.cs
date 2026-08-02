using UnityEngine;

/// <summary>
/// Aplica a regra de profundidade das árvores já posicionadas em World/Arvores:
/// tronco colide + Y-sort; folhas por cima só quando o personagem está atrás.
/// Não altera posição, escala nem rotação.
/// Player e NPCs já usam <see cref="CharacterDepthSort"/> (mesmo eixo Y).
/// </summary>
[DefaultExecutionOrder(-42)]
public class TreeDepthSplitBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        ApplyAll();
    }

    public static int ApplyAll()
    {
        int added = 0;
        Transform[] roots = FindArvoresRoots();
        for (int r = 0; r < roots.Length; r++)
        {
            Transform root = roots[r];
            if (root == null)
                continue;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                if (ApplyToTree(child.gameObject))
                    added++;
            }
        }

        if (added > 0)
            Debug.Log($"Prisma: SeasonalTree aplicado em {added} árvores (posição preservada; tronco colide; folhas Y-sort).");

        return added;
    }

    private static Transform[] FindArvoresRoots()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        System.Collections.Generic.List<Transform> roots = new(4);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.parent == null)
                continue;

            string n = t.name;
            if (n == "Arvores" || n == "Árvores" || n == "Arvores 2" || n == "Árvores 2")
                roots.Add(t);
        }

        return roots.ToArray();
    }

    private static bool ApplyToTree(GameObject go)
    {
        if (go == null)
            return false;

        // Filhos utilitários — nunca viram árvore.
        if (go.name == "Sombra" || go.name == "Canopy" || go.name == PropDepthSplit.TopChildName)
            return false;

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null)
            return false;

        if (go.GetComponent<SeasonalTree>() != null)
            return false;
        if (go.GetComponent<ElasticFoliage>() != null)
            return false;
        if (go.GetComponent<PlayerController>() != null)
            return false;
        if (go.GetComponent<NpcController>() != null)
            return false;

        // Remove PropDepthSplit genérico se alguém aplicou antes — SeasonalTree é a regra das árvores.
        PropDepthSplit propSplit = go.GetComponent<PropDepthSplit>();
        if (propSplit != null)
        {
            if (Application.isPlaying)
                Object.Destroy(propSplit);
            else
                Object.DestroyImmediate(propSplit);
        }

        go.AddComponent<SeasonalTree>();
        return true;
    }
}
