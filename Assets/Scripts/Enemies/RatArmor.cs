/*
 * RatArmor
 * Shows a layered rat's armor (2026-11-20, enemy layers step 2): one piece per armor layer under Visual > RatModel
 * (so it bobs with RatScuttle), and pops pieces off as the layers are stripped. On the root of Enemy.prefab.
 *
 * - pieces[i] is shown while the enemy has more than i + 1 layers (pieces[0] = Helmet from 2 layers, pieces[1] = Plate
 *   from 3), matching EnemyStats.layers (Bare, Helmet, Plate). Polled from Enemy.Layers in LateUpdate (cheap), so
 *   spawn-time armor (SetExtraLayers) and hits are both picked up before the frame renders.
 * - While a piece with hidesEars is on, the body uses earlessMesh (the ears are under the cap), else earsMesh.
 * - A piece that goes away because of damage pops: ArmorPopEffect plays a chip burst in its popColor at the piece.
 *   A rat killed with armor still on (one big hit) pops what it wore. Pooled re-spawns, despawns and dives don't pop.
 * - ApplyTo(visualCopy) gives BaseDive's model copy the same pieces and body, so a diving rat keeps its armor.
 */

using System;
using UnityEngine;

public class RatArmor : MonoBehaviour
{
    [Serializable]
    public class Piece
    {
        public string name = "Helmet";
        public GameObject piece;
        [Tooltip("The body swaps to the earless mesh while this piece is on.")]
        public bool hidesEars;
        public Color popColor = Color.white;
    }

    [SerializeField] private Enemy enemy;
    [Tooltip("The rat body's MeshFilter (Visual > RatModel).")]
    [SerializeField] private MeshFilter body;
    [SerializeField] private Mesh earsMesh;
    [SerializeField] private Mesh earlessMesh;
    [Tooltip("Armor pieces, innermost first: element 0 shows from 2 layers, element 1 from 3, ...")]
    [SerializeField] private Piece[] pieces = new Piece[0];

    private int shownLayers = -1;
    private string bodyPath;
    private string[] piecePaths;

    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }
        Transform visual = enemy != null ? enemy.Visual : null;
        if (visual == null)
        {
            return;
        }
        bodyPath = body != null ? PathFrom(visual, body.transform) : null;
        piecePaths = new string[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
        {
            piecePaths[i] = pieces[i].piece != null ? PathFrom(visual, pieces[i].piece.transform) : null;
        }
    }

    private void OnEnable()
    {
        shownLayers = -1;
    }

    private void LateUpdate()
    {
        if (enemy == null)
        {
            return;
        }
        int layers = enemy.Layers;
        if (layers != shownLayers)
        {
            Show(layers, shownLayers > 0 && layers < shownLayers);
            shownLayers = layers;
        }
    }

    private void OnDisable()
    {
        // Killed (not despawned or dived) with armor on: pop what it still wore.
        if (enemy != null && enemy.Health <= 0f)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].piece != null && pieces[i].piece.activeSelf)
                {
                    Pop(pieces[i]);
                }
            }
        }
    }

    private void Show(int layers, bool pop)
    {
        bool earless = false;
        for (int i = 0; i < pieces.Length; i++)
        {
            Piece p = pieces[i];
            if (p.piece == null)
            {
                continue;
            }
            bool on = layers >= i + 2;
            if (!on && pop && p.piece.activeSelf)
            {
                Pop(p);
            }
            p.piece.SetActive(on);
            earless |= on && p.hidesEars;
        }
        SetBody(body, earless);
    }

    private void SetBody(MeshFilter filter, bool earless)
    {
        Mesh wanted = earless ? earlessMesh : earsMesh;
        if (filter != null && wanted != null && filter.sharedMesh != wanted)
        {
            filter.sharedMesh = wanted;
        }
    }

    private static void Pop(Piece p)
    {
        MeshFilter filter = p.piece.GetComponent<MeshFilter>();
        Vector3 point = filter != null && filter.sharedMesh != null
            ? filter.transform.TransformPoint(filter.sharedMesh.bounds.center)
            : p.piece.transform.position;
        ArmorPopEffect.Play(point, p.popColor);
    }

    /// <summary>Gives a copy of this enemy's Visual (BaseDive's diver) the same armor pieces and body mesh.</summary>
    public void ApplyTo(Transform visualCopy)
    {
        if (visualCopy == null || piecePaths == null)
        {
            return;
        }
        bool earless = false;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (piecePaths[i] == null || pieces[i].piece == null)
            {
                continue;
            }
            Transform copy = visualCopy.Find(piecePaths[i]);
            bool on = pieces[i].piece.activeSelf;
            if (copy != null)
            {
                copy.gameObject.SetActive(on);
            }
            earless |= on && pieces[i].hidesEars;
        }
        Transform copyBody = bodyPath != null ? visualCopy.Find(bodyPath) : null;
        SetBody(copyBody != null ? copyBody.GetComponent<MeshFilter>() : null, earless);
    }

    private static string PathFrom(Transform root, Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null && p != root; p = p.parent)
        {
            path = p.name + "/" + path;
        }
        return path;
    }
}
