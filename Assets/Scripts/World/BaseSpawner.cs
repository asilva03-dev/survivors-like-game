/*
 * BaseSpawner
 * Scene object BasePoint: the designated spot for the base. On Start it places the Base prefab here (once).
 *
 * - Move the BasePoint object to move where the base appears. The gizmo shows the spot and the base's
 *   targetRadius in the Scene view.
 * - Spawning once at Start is a placeholder; a round/run manager can call Spawn() itself later.
 */

using UnityEngine;

public class BaseSpawner : MonoBehaviour
{
    [SerializeField] private BaseStructure basePrefab;
    [SerializeField] private bool spawnOnStart = true;

    public BaseStructure Current { get; private set; }

    private void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

    public BaseStructure Spawn()
    {
        if (Current == null && basePrefab != null)
        {
            Current = Instantiate(basePrefab, transform.position, Quaternion.identity);
        }

        return Current;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        if (basePrefab != null)
        {
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.01f,
                new Vector3(basePrefab.TargetRadius * 2f, 0f, basePrefab.TargetRadius * 2f));
        }
    }
}
