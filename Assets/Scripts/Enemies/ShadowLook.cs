/*
 * ShadowLook
 * The shadow rat's look (2026-11-21, user's picks: ink-violet fur + a patchy smoke veil). On the root of rat prefabs that
 * can spawn as shadows (Enemy.prefab, BagRat.prefab, PotRat.prefab, AthleticRat.prefab). Polls Enemy.IsShadow in LateUpdate (it is set right after the
 * spawner's Get and reset by the pool), so the look is right before the frame renders.
 *
 * - Fur: every MeshRenderer under the enemy's Visual (body, armor pieces, sack; not the veil) has its materials swapped
 *   by `swaps` (normal rat material -> its ink-violet version: fur, pink parts, dark parts) while it is a shadow, and
 *   put back otherwise. Other materials (bottle cap, iron plate, burlap) stay, so armor layers still read.
 * - Veil: the `veil` child (a smooth sphere with Custom/ShadowVeil, under Visual > RatModel so it scuttles along) is
 *   shown only while it is a shadow. Its size and placement are set on the prefab per rat type.
 * - ApplyTo(visualCopy) gives BaseDive's model copy the same fur and veil, so a diving shadow rat stays a shadow.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public class ShadowLook : MonoBehaviour
{
    [Serializable]
    public class Swap
    {
        public Material normal;
        public Material shadow;
    }

    [SerializeField] private Enemy enemy;
    [Tooltip("The veil child (Custom/ShadowVeil sphere), hidden unless this enemy is a shadow.")]
    [SerializeField] private GameObject veil;
    [SerializeField] private List<Swap> swaps = new List<Swap>();

    private MeshRenderer[] renderers;
    private Material[][] normalSets;
    private Material[][] shadowSets;
    private string[] paths;
    private string veilPath;
    private int shown = -1;

    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }
        Transform visual = enemy != null && enemy.Visual != null ? enemy.Visual : transform;
        List<MeshRenderer> list = new List<MeshRenderer>();
        foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (veil == null || !r.transform.IsChildOf(veil.transform))
            {
                list.Add(r);
            }
        }
        renderers = list.ToArray();
        normalSets = new Material[renderers.Length][];
        shadowSets = new Material[renderers.Length][];
        paths = new string[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            normalSets[i] = renderers[i].sharedMaterials;
            shadowSets[i] = ShadowSet(normalSets[i]);
            paths[i] = PathFrom(visual, renderers[i].transform);
        }
        veilPath = veil != null ? PathFrom(visual, veil.transform) : null;
    }

    private void OnEnable()
    {
        shown = -1;
    }

    private void LateUpdate()
    {
        bool shadow = enemy != null && enemy.IsShadow;
        int state = shadow ? 1 : 0;
        if (state == shown)
        {
            return;
        }
        shown = state;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterials = shadow ? shadowSets[i] : normalSets[i];
        }
        if (veil != null)
        {
            veil.SetActive(shadow);
        }
    }

    private Material[] ShadowSet(Material[] normal)
    {
        Material[] set = new Material[normal.Length];
        for (int i = 0; i < normal.Length; i++)
        {
            set[i] = normal[i];
            for (int s = 0; s < swaps.Count; s++)
            {
                if (swaps[s].normal == normal[i] && swaps[s].shadow != null)
                {
                    set[i] = swaps[s].shadow;
                    break;
                }
            }
        }
        return set;
    }

    /// <summary>Gives a copy of this enemy's Visual (BaseDive's diver) the same fur and veil as this enemy now.</summary>
    public void ApplyTo(Transform visualCopy)
    {
        if (visualCopy == null || paths == null)
        {
            return;
        }
        bool shadow = enemy != null && enemy.IsShadow;
        for (int i = 0; i < paths.Length; i++)
        {
            Transform copy = paths[i].Length == 0 ? visualCopy : visualCopy.Find(paths[i]);
            MeshRenderer r = copy != null ? copy.GetComponent<MeshRenderer>() : null;
            if (r != null)
            {
                r.sharedMaterials = shadow ? shadowSets[i] : normalSets[i];
            }
        }
        if (veilPath != null)
        {
            Transform copyVeil = visualCopy.Find(veilPath);
            if (copyVeil != null)
            {
                copyVeil.gameObject.SetActive(shadow);
            }
        }
    }

    private static string PathFrom(Transform root, Transform t)
    {
        if (t == root)
        {
            return "";
        }
        string path = t.name;
        for (Transform p = t.parent; p != null && p != root; p = p.parent)
        {
            path = p.name + "/" + path;
        }
        return path;
    }
}
