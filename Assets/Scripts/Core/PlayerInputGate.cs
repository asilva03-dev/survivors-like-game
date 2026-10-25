/*
 * PlayerInputGate
 * Lets UI modes (the preset draft, build mode) keep the player's mouse actions from firing. A mode calls
 * BlockAttacks() / BlockDash() every frame it is open; each stays blocked for one extra frame after it closes, so the
 * click that closed it (picking a card, placing a chunk) never also swings the axe or dashes.
 */

using UnityEngine;

public static class PlayerInputGate
{
    private static int attacksBlockedFrame = -10;
    private static int dashBlockedFrame = -10;

    public static bool AttacksBlocked => Time.frameCount - attacksBlockedFrame <= 1;
    public static bool DashBlocked => Time.frameCount - dashBlockedFrame <= 1;

    public static void BlockAttacks()
    {
        attacksBlockedFrame = Time.frameCount;
    }

    public static void BlockDash()
    {
        dashBlockedFrame = Time.frameCount;
    }
}
