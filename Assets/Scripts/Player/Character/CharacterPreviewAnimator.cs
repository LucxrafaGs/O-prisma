// Preview animado na tela de personalizacao.
using System.Collections.Generic;
using UnityEngine;

public class CharacterPreviewAnimator : MonoBehaviour
{
    [SerializeField] private PlayerAppearance appearance;
    [SerializeField] private float frameDuration = 0.135f;

    private static readonly int[] WalkDownFrames = { 32, 33, 34, 35, 36, 37 };

    private int frameIndex;
    private float frameTimer;
    private int activeDisplayFrame = 32;

    private void Awake()
    {
        if (appearance == null)
            appearance = GetComponent<PlayerAppearance>();
    }

    private void Update()
    {
        if (appearance == null)
            return;

        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration)
            return;

        frameTimer -= frameDuration;
        frameIndex = (frameIndex + 1) % WalkDownFrames.Length;
        ApplyDisplayFrame(WalkDownFrames[frameIndex]);
    }

    public void RefreshAppearance()
    {
        if (appearance != null)
            appearance.ApplySavedAppearance();

        frameIndex = 0;
        frameTimer = 0f;
        ApplyDisplayFrame(WalkDownFrames[0]);
    }

    public void PreviewAppearance(Dictionary<CharacterLayerType, string> selection)
    {
        if (appearance == null)
            return;

        appearance.ApplyAppearance(selection);
        frameIndex = 0;
        frameTimer = 0f;
        ApplyDisplayFrame(WalkDownFrames[0]);
    }

    public void PreviewAppearance(string skinId, string outfitId, string hairId, string hatId)
    {
        Dictionary<CharacterLayerType, string> selection = CharacterAppearanceData.Load();
        selection[CharacterLayerType.Skin] = skinId;
        selection[CharacterLayerType.Outfit] = outfitId ?? string.Empty;
        selection[CharacterLayerType.Hair] = hairId ?? string.Empty;
        selection[CharacterLayerType.Hat] = hatId ?? string.Empty;
        PreviewAppearance(selection);
    }

    private void ApplyDisplayFrame(int preferredFrame)
    {
        activeDisplayFrame = appearance.ResolveDisplayFrame(preferredFrame);
        appearance.SetFrame(activeDisplayFrame);
    }
}
