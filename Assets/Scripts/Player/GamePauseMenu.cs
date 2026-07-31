using UnityEngine;

/// <summary>
/// Kept for scene compatibility. Delegates ESC menu to the backpack system.
/// </summary>
[DefaultExecutionOrder(-90)]
public class GamePauseMenu : MonoBehaviour
{
    private void Awake()
    {
        if (GetComponent<PrismaBackpackMenu>() == null)
            gameObject.AddComponent<PrismaBackpackMenu>();

        if (Application.isPlaying)
            Destroy(this);
        else
            DestroyImmediate(this);
    }
}
