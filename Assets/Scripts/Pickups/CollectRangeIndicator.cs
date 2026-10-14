/*
 * CollectRangeIndicator
 * Green ground circle showing the player's resource collection radius (ResourceCollector). Drawing
 * and setup live in GroundRangeIndicator; color and opacity are on the LineRenderer.
 */

using UnityEngine;

public class CollectRangeIndicator : GroundRangeIndicator
{
    [SerializeField] private ResourceCollector collector;

    protected override Vector3 Center => collector.transform.position;
    protected override float Radius => collector.CollectRadius;
}
