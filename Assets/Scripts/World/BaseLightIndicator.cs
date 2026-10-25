/*
 * BaseLightIndicator
 * Ground ring showing the BaseLight edge (debug look until the dark-forest lighting pass). Drawing and setup live in
 * GroundRangeIndicator; colour and opacity are on the LineRenderer. Scene object `BaseLightRing`.
 */

using UnityEngine;

public class BaseLightIndicator : GroundRangeIndicator
{
    [SerializeField] private BaseLight baseLight;

    protected override Vector3 Center => baseLight != null ? baseLight.Center : Vector3.zero;
    protected override float Radius => baseLight != null ? baseLight.Radius : 0f;
}
