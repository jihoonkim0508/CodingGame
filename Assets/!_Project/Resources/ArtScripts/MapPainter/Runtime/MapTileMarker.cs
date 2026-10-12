using UnityEngine;

namespace CodingGame.Art.MapPainter
{
    [DisallowMultipleComponent]
    public sealed class MapTileMarker : MonoBehaviour
    {
        [HideInInspector] public GridCell cell;
        [HideInInspector] public bool road;
        [HideInInspector] public GameObject source;
        [HideInInspector] public int turns;
    }
}
