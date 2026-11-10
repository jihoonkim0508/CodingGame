using UnityEngine;

namespace CodingGame.Art.MapPainter
{
    // Stores the original renderer states; source prefab geometry is retained.
    [DisallowMultipleComponent,AddComponentMenu("")]
    public sealed class WideRoadTile : MonoBehaviour
    {
        [HideInInspector] public Renderer[] originalRenderers;
        [HideInInspector] public bool[] originalEnabled;
    }
}
