/*
 * ResourceCollector
 * The player's collection range: collectable resource drops (wood now, xp and others later) inside
 * collectRadius are pulled toward the player.
 *
 * - Put it on the Player. collectRadius is flat (XZ) distance and can be tweaked live; the green
 *   ground circle (CollectRangeIndicator) follows it.
 * - Only touches WoodDrop for now. WeaponPickup is separate and keeps its own walk-over pickup.
 */

using UnityEngine;

public class ResourceCollector : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float collectRadius = 4f;

    public float CollectRadius => collectRadius;

    private void Update()
    {
        Vector3 center = transform.position;
        float sqr = collectRadius * collectRadius;

        for (int i = 0; i < WoodDrop.Active.Count; i++)
        {
            WoodDrop drop = WoodDrop.Active[i];
            if (!drop.IsCollectable)
            {
                continue;
            }

            Vector3 offset = drop.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude <= sqr)
            {
                drop.Attract(transform);
            }
        }
    }
}
