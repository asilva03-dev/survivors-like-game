/*
 * IMeleeReactive
 * Put on a Damageable (same object) that the melee weapon should physically react with even though it does not
 * damage it (the base). MeleeWeapon's sweep then "hits" it once per swing like any target, without damage or
 * knockback, and calls OnMeleeHit.
 * - from: where the swing came from (the player's position). charged: the charged swing.
 */

using UnityEngine;

public interface IMeleeReactive
{
    void OnMeleeHit(Vector3 from, bool charged);
}
