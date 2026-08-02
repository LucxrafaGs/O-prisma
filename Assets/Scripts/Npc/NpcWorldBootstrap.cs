using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Host dos sistemas de jogo + NPCs andando.
/// Em Awake recria HUD, relógio, clima, diálogo, save, DevMode e wanderers.
/// </summary>
public class NpcWorldBootstrap : MonoBehaviour
{
    [SerializeField] private int npcCount = 3;
    [SerializeField] private Vector3 npcScale = new(CharacterWorldScale.Uniform, CharacterWorldScale.Uniform, 1f);

    private static readonly string[] Names =
    {
        "Helena",
        "Lucas",
        "Marina",
        "Rafael",
        "Sofia",
        "Caio"
    };

    private static readonly string[][] LinePools =
    {
        new[]
        {
            "Você viu o festival da praça? Eu ainda estou decidindo se vou.",
            "A biblioteca fecha mais cedo nas segundas. Não pergunta como eu descobri.",
            "Pedra Branca fica estranha depois do entardecer... mas é só impressão."
        },
        new[]
        {
            "Treino na quadra depois da aula. Se quiser, aparece.",
            "Não esquece a prova de história. Ou esquece. Eu não sou sua mãe.",
            "Ouvi um barulho perto do farol. Pode ter sido vento."
        },
        new[]
        {
            "Bom dia. Ou boa tarde. Eu perdi a noção do horário.",
            "Se achar uma fita cassete sem rótulo, me avisa. Eu coleciono.",
            "Às vezes o mapa mente. A cidade muda mais do que a gente admite."
        }
    };

    /// <summary>
    /// Se a SampleScene perdeu o NpcWorld, recria o host ao carregar a cena de jogo.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureHostInGameScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != GameScenes.Game && scene.path != "Assets/Scenes/SampleScene.unity")
            return;

        if (Object.FindAnyObjectByType<NpcWorldBootstrap>() != null)
            return;

        GameObject host = new GameObject("NpcWorld");
        host.AddComponent<NpcWorldBootstrap>();
        Debug.Log("Prisma: NpcWorld recriado automaticamente (cena estava sem bootstrap).");
    }

    private void Awake()
    {
        CharacterLibraryAccess.WarmUp();
        EnsureGameSystems();
        EnsureSupportObjects();
        TreeDepthSplitBootstrap.ApplyAll();

        Transform root = transform.Find("NPCs");
        if (root == null)
        {
            GameObject npcs = new GameObject("NPCs");
            npcs.transform.SetParent(transform, false);
            root = npcs.transform;
        }

        // Garante escala atual mesmo se NPCs já existiam na cena.
        npcScale = CharacterWorldScale.Vector;
        for (int i = 0; i < root.childCount; i++)
            CharacterWorldScale.Apply(root.GetChild(i));

        if (root.childCount > 0)
            return;

        CharacterSpriteLibrary library = CharacterLibraryAccess.Get();
        Vector2[] spawns =
        {
            new Vector2(2.4f, 0.8f),
            new Vector2(-2.8f, -1.1f),
            new Vector2(1.2f, -2.4f),
            new Vector2(-1.6f, 1.8f)
        };

        int count = Mathf.Clamp(npcCount, 1, spawns.Length);
        List<int> usedNameIndexes = new();

        for (int i = 0; i < count; i++)
        {
            int nameIndex = PickUnusedIndex(Names.Length, usedNameIndexes);
            usedNameIndexes.Add(nameIndex);

            string npcName = Names[nameIndex];
            string[] lines = LinePools[i % LinePools.Length];
            Vector2 spawn = spawns[i];

            CreateNpc(root, npcName, lines, spawn, library);
        }
    }

    private void EnsureGameSystems()
    {
        if (GetComponent<NpcDialogueSystem>() == null)
            gameObject.AddComponent<NpcDialogueSystem>();

        if (GetComponent<GameTimeClock>() == null)
            gameObject.AddComponent<GameTimeClock>();

        if (GetComponent<GameHudController>() == null)
            gameObject.AddComponent<GameHudController>();

        if (GetComponent<DayNightLighting>() == null)
            gameObject.AddComponent<DayNightLighting>();

        if (GetComponent<DevModeController>() == null)
            gameObject.AddComponent<DevModeController>();

        if (GetComponent<RainWeatherSystem>() == null)
            gameObject.AddComponent<RainWeatherSystem>();

        if (GetComponent<WeatherDirector>() == null)
            gameObject.AddComponent<WeatherDirector>();

        if (GetComponent<GameSessionSave>() == null)
            gameObject.AddComponent<GameSessionSave>();

        if (GetComponent<DappledSunLighting>() == null)
            gameObject.AddComponent<DappledSunLighting>();

        SoundsOfTheWorld.EnsureInScene();

        if (Object.FindAnyObjectByType<EchoApparitionSystem>() == null)
        {
            GameObject echoHost = new GameObject("EchoApparitionSystem");
            echoHost.AddComponent<EchoApparitionSystem>();
        }
    }

    private static void EnsureSupportObjects()
    {
        if (Object.FindAnyObjectByType<PrismaBackpackMenu>() == null
            && Object.FindAnyObjectByType<GamePauseMenu>() == null)
        {
            GameObject pauseHost = new GameObject("GamePauseMenu");
            pauseHost.AddComponent<GamePauseMenu>();
        }

        if (Object.FindAnyObjectByType<CinemachineFollowPlayerSetup>() == null)
        {
            GameObject cameraRig = GameObject.Find("CM_PlayerFollow");
            if (cameraRig == null)
                cameraRig = new GameObject("CM_PlayerFollow");
            cameraRig.AddComponent<CinemachineFollowPlayerSetup>();
        }

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            CharacterWorldScale.Apply(player.transform);
            try
            {
                player.tag = "Player";
            }
            catch (UnityException)
            {
                // Tag Player pode não existir no TagManager do projeto.
            }
        }
    }

    private void CreateNpc(
        Transform parent,
        string npcName,
        string[] lines,
        Vector2 spawn,
        CharacterSpriteLibrary library)
    {
        GameObject npcObject = new GameObject(npcName);
        npcObject.transform.SetParent(parent, false);
        npcObject.transform.position = spawn;
        npcObject.transform.localScale = npcScale;
        npcObject.layer = 0;

        PlayerAppearance appearance = npcObject.AddComponent<PlayerAppearance>();
        appearance.SetApplySavedAppearanceOnAwake(false);

        npcObject.AddComponent<Rigidbody2D>();
        npcObject.AddComponent<BoxCollider2D>();
        NpcController controller = npcObject.AddComponent<NpcController>();

        Dictionary<CharacterLayerType, string> look = CharacterRandomizer.CreateRandomNpcLook(library);
        controller.Configure(npcName, lines, look);
    }

    private static int PickUnusedIndex(int count, List<int> used)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            int index = Random.Range(0, count);
            if (!used.Contains(index))
                return index;
        }

        return Random.Range(0, count);
    }
}
