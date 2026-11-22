/*
 * TurretModelFit
 * One knob for a turret's size (2026-11-20, user: the new gun turret looked too small and they want to tune it
 * themselves). On the turret prefab's root. Changing Model Scale in the inspector (prefab or a scene instance) resizes
 * the model in step:
 *
 * - baseModel (static base, feet on the ground) and bodyModel (the turning body under the Gun) both get modelScale.
 * - gun and aimPoint (the body's pivot and the weapon's owner point) move to pivotHeight x modelScale above the ground,
 *   so the bigger body still sits on its post.
 * - The bullets start at the gun pivot + the GunStats muzzleOffset; keep that at muzzleAtScale1 x modelScale (see the
 *   field's tooltip; SuggestedMuzzleOffset gives the number) so they leave from the barrel tip. The stats asset is shared, so this doesn't write it.
 * - Applied in OnValidate (editor) only; the values are saved in the prefab, nothing runs in game.
 * - pivotHeight / muzzleAtScale1 match TurretModelBuilder (PivotHeight 1.56, MuzzleZ 0.99) for the gun turret model.
 */

using UnityEngine;

public class TurretModelFit : MonoBehaviour
{
    [Tooltip("Size of the whole turret model (1 = the size it was built at).")]
    [SerializeField, Range(0.5f, 2.5f)] private float modelScale = 1f;
    [Tooltip("Height of the gun pivot above the ground at scale 1 (TurretModelBuilder.PivotHeight).")]
    [SerializeField, Min(0f)] private float pivotHeight = 1.56f;
    [Tooltip("Barrel tip in front of the pivot at scale 1 (TurretModelBuilder.MuzzleZ). Set the gun stats' Muzzle Offset to this x Model Scale.")]
    [SerializeField, Min(0f)] private float muzzleAtScale1 = 0.99f;
    [SerializeField] private Transform baseModel;
    [SerializeField] private Transform bodyModel;
    [SerializeField] private Transform gun;
    [SerializeField] private Transform aimPoint;

    public float ModelScale => modelScale;
    /// <summary>The Muzzle Offset the gun stats should have at this scale.</summary>
    public float SuggestedMuzzleOffset => muzzleAtScale1 * modelScale;

    private void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        Vector3 scale = Vector3.one * modelScale;
        if (baseModel != null)
        {
            baseModel.localScale = scale;
        }
        if (bodyModel != null)
        {
            bodyModel.localScale = scale;
        }
        float height = pivotHeight * modelScale;
        if (gun != null)
        {
            gun.localPosition = new Vector3(gun.localPosition.x, height, gun.localPosition.z);
        }
        if (aimPoint != null)
        {
            aimPoint.localPosition = new Vector3(aimPoint.localPosition.x, height, aimPoint.localPosition.z);
        }
    }
}
