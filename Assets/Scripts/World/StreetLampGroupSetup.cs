using UnityEngine;

/// <summary>
/// Coloque em "Postes". No Play (e no Awake), garante StreetLampLight
/// em todos os filhos cujo nome começa com "Poste".
/// </summary>
[DefaultExecutionOrder(-40)]
public class StreetLampGroupSetup : MonoBehaviour
{
    private void Awake()
    {
        Apply(transform);
    }

    public static void Apply(Transform root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null || !child.name.StartsWith("Poste"))
                continue;

            if (child.GetComponent<StreetLampLight>() == null)
                child.gameObject.AddComponent<StreetLampLight>();

            if (child.GetComponent<BoxCollider2D>() != null &&
                child.GetComponent<StreetLampDepthSplit>() == null)
                child.gameObject.AddComponent<StreetLampDepthSplit>();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAll()
    {
        // Cobre o caso do empty ainda sem este componente na cena.
        StreetLampGroupSetup[] groups = Object.FindObjectsByType<StreetLampGroupSetup>(FindObjectsInactive.Include);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null)
                Apply(groups[i].transform);
        }

        GameObject postes = GameObject.Find("Postes");
        if (postes != null)
        {
            if (postes.GetComponent<StreetLampGroupSetup>() == null)
                postes.AddComponent<StreetLampGroupSetup>();
            Apply(postes.transform);
        }

        // Qualquer Poste solto na cena.
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.parent == null)
                continue;
            if (t.parent.name != "Postes" || !t.name.StartsWith("Poste"))
                continue;
            if (t.GetComponent<StreetLampLight>() == null)
                t.gameObject.AddComponent<StreetLampLight>();
            if (t.GetComponent<BoxCollider2D>() != null &&
                t.GetComponent<StreetLampDepthSplit>() == null)
                t.gameObject.AddComponent<StreetLampDepthSplit>();
        }
    }
}
