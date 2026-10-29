using UnityEngine;
using CodingGame.Art.StageSelection;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageNode : MonoBehaviour
    {
        Renderer targetRenderer;
        Material availableMaterial, lockedMaterial, clearedMaterial;
        StageIslandView island;

        public int Number { get; private set; }
        public StageState State { get; private set; }
        public Bounds VisualBounds => island ? island.VisualBounds : targetRenderer ? targetRenderer.bounds : new Bounds(transform.position,Vector3.one);

        public void Configure(int number, StageMaterialPalette palette)
        {
            Number = number;
            island=GetComponent<StageIslandView>();
            targetRenderer = GetComponent<Renderer>();
            Material original = targetRenderer.sharedMaterial;
            availableMaterial = palette && palette.AvailableMaterial ? palette.AvailableMaterial : original;
            lockedMaterial = palette && palette.LockedMaterial ? palette.LockedMaterial : original;
            clearedMaterial = palette ? palette.GetClearedMaterial(number - 1, original) : original;
            if (!TryGetComponent<Collider>(out _)) gameObject.AddComponent<BoxCollider>();
        }

        public void Refresh()
        {
            State = StageProgress.GetState(Number);
            if(island){island.RefreshState(State);return;}
            targetRenderer.sharedMaterial = State switch
            {
                StageState.Locked => lockedMaterial,
                StageState.Available => availableMaterial,
                _ => clearedMaterial
            };
        }
    }
}
