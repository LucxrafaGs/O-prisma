using UnityEngine;

/// <summary>
/// Cicla sprites em loop no SpriteRenderer (água da fonte, etc.).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class AnimatedSpriteLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] [Min(0.1f)] private float framesPerSecond = 10f;
    [SerializeField] private bool playOnEnable = true;

    private SpriteRenderer spriteRenderer;
    private float timer;
    private int index;

    public Sprite[] Frames
    {
        get => frames;
        set => frames = value;
    }

    public float FramesPerSecond
    {
        get => framesPerSecond;
        set => framesPerSecond = Mathf.Max(0.1f, value);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (!playOnEnable || frames == null || frames.Length == 0 || spriteRenderer == null)
            return;

        index = 0;
        timer = 0f;
        if (frames[0] != null)
            spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null)
            return;

        float step = 1f / framesPerSecond;
        timer += Time.deltaTime;
        while (timer >= step)
        {
            timer -= step;
            index = (index + 1) % frames.Length;
            Sprite next = frames[index];
            if (next != null)
            {
                Vector2 size = spriteRenderer.size;
                SpriteDrawMode mode = spriteRenderer.drawMode;
                spriteRenderer.sprite = next;
                if (mode != SpriteDrawMode.Simple)
                {
                    spriteRenderer.drawMode = mode;
                    spriteRenderer.size = size;
                }
            }
        }
    }
}
