using UnityEngine;

/// <summary>
/// Aplica <see cref="PropDepthSplit"/> em props da cena com SpriteRenderer + Collider2D
/// (bancos, lixeiras, jarros, casas, etc.). Não mexe em árvores/folhagem/postes.
/// </summary>
[DefaultExecutionOrder(-40)]
public class PropDepthSplitBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        ApplyAll();
    }

    public static int ApplyAll()
    {
        int added = 0;
        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            GameObject go = renderer.gameObject;
            if (!ShouldApply(go))
                continue;

            if (go.GetComponent<PropDepthSplit>() != null)
                continue;

            go.AddComponent<PropDepthSplit>();
            added++;
        }

        if (added > 0)
            Debug.Log($"Prisma: PropDepthSplit aplicado em {added} props (frente no collider, atrás no topo).");

        return added;
    }

    private static bool ShouldApply(GameObject go)
    {
        if (go.GetComponent<StreetLampDepthSplit>() != null)
            return false;
        if (go.GetComponent<SeasonalTree>() != null)
            return false;
        if (go.GetComponent<ElasticFoliage>() != null)
            return false;
        if (go.GetComponent<WorldOceanBackground>() != null)
            return false;
        if (go.GetComponent<PlayerController>() != null)
            return false;
        if (go.GetComponent<NpcController>() != null)
            return false;
        if (go.GetComponent<AnimatedSpriteLoop>() != null && go.name == "Fonte")
            return false;

        // Precisa de collider sólido no próprio GO ou em filhos.
        if (!HasSolidCollider(go))
            return false;

        // Só props do mundo (não UI).
        Transform t = go.transform;
        while (t != null)
        {
            string n = t.name;
            if (n == "City_Props" || n == "World" || n == "Construction" ||
                n == "Decorações" || n == "Bncos_Praça" || n == "Bancos_Praça" ||
                n == "Lixeiras_Praça" || n == "Postes" || n == "Barcos" ||
                n == "Arvores" || n == "Garden_Interact")
                return true;
            t = t.parent;
        }

        return false;
    }

    private static bool HasSolidCollider(GameObject go)
    {
        Collider2D[] colliders = go.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c != null && c.enabled && !c.isTrigger)
                return true;
        }

        return false;
    }
}
