using UnityEngine;

public static class CharacterP4Animations
{
    public const float ToeTapFrameDuration = 0.4f;
    public const int ToeTapFrameCount = 3;
    public const int ToeTapHoldFrameIndex = 0;
    public const int ToeTapLoopStartFrameIndex = 1;

    // Mana Seed page 4 — bottom 4 rows, cols 1–3 (idle → toe tap → return).
    private static readonly int[] ToeTapDown = { 32, 33, 34 };
    private static readonly int[] ToeTapUp = { 40, 41, 42 };
    private static readonly int[] ToeTapLeft = { 56, 57, 58 };
    private static readonly int[] ToeTapRight = { 48, 49, 50 };

    public static int GetToeTapSpriteIndex(PlayerController.Facing direction, int frame)
    {
        int[] frames = direction switch
        {
            PlayerController.Facing.Down => ToeTapDown,
            PlayerController.Facing.Up => ToeTapUp,
            PlayerController.Facing.Left => ToeTapLeft,
            PlayerController.Facing.Right => ToeTapRight,
            _ => ToeTapDown
        };

        return frames[Mathf.Clamp(frame, 0, ToeTapFrameCount - 1)];
    }
}
