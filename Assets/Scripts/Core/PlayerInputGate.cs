/*
 * PlayerInputGate
 * Lets UI modes (the preset draft, later build mode) keep the player's mouse attacks from firing. A mode calls
 * BlockAttacks() every frame it is open; attacks stay blocked for one extra frame after it closes, so the click that
 * closed it (picking a card, placing a chunk) never also swings the axe.
 */

using UnityEngine;

public static class PlayerInputGate
{
    private static int blockedFrame = -10;

    public static bool AttacksBlocked => Time.frameCount - blockedFrame <= 1;

    public static void BlockAttacks()
    {
        blockedFrame = Time.frameCount;
    }
}
