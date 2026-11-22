/*
 * RevealLight
 * An external light source that reveals shadow rats (2026-11-21): any weapon may target a shadow rat standing within
 * `radius` (flat) of an enabled RevealLight (see ShadowSight). For lanterns, flares or light turrets later; none are
 * placed yet. The base light is deliberately not one (user: it is too weak).
 *
 * - Active lists every enabled one; Reveals(point) checks them all (a handful at most).
 */

using System.Collections.Generic;
using UnityEngine;

public class RevealLight : MonoBehaviour
{
    public static readonly List<RevealLight> Active = new List<RevealLight>();

    [Tooltip("Shadow rats within this flat distance can be targeted by anything.")]
    [SerializeField, Min(0f)] private float radius = 4f;

    public float Radius => radius;

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    public static bool Reveals(Vector3 point)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            Vector3 offset = point - Active[i].transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= Active[i].radius * Active[i].radius)
            {
                return true;
            }
        }
        return false;
    }
}
