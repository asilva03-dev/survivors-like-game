/*
 * AngelcatVolley
 * Holds one Angel Cat tower's arrow-volley stats (2026-11-25 upgrade pass). On the Angel Cat prefab root.
 *
 * - The arrows' AngelArrowStick looks this up from the tower that fired them (Projectile.Shooter, the tower's aim point),
 *   so each tower's volley can be upgraded on its own; Turret hands over its own upgraded copy with UseStats.
 * - Stats = the shared asset (Weapons/AngelcatVolleyStats) until an upgrade is bought.
 */

using UnityEngine;

public class AngelcatVolley : MonoBehaviour
{
    [SerializeField] private ArrowVolleyStats stats;

    public ArrowVolleyStats Stats => stats;

    public void UseStats(ArrowVolleyStats replacement)
    {
        if (replacement != null)
        {
            stats = replacement;
        }
    }
}
