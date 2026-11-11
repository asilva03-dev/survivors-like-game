/*
 * IWadeable
 * Something the player wades through (2026-11-10): trees (ResourceTree) and decoration bushes (DecorBush).
 * PlayerWading calls Rustle with a flat world direction (away from the player) when the player enters it and while they
 * keep moving inside it.
 */

using UnityEngine;

public interface IWadeable
{
    void Rustle(Vector3 push);
}
