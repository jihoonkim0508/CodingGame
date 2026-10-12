using UnityEngine;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageNode : MonoBehaviour
    {
        Renderer targetRenderer;
        Material availableMaterial, lockedMaterial, clearedMaterial;

        public int Number { get; private set; }
        public StageState State { get; private set; }

        public void Configure(int number, StageMaterialPalette palette)
        {
            Number = number;
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
            targetRenderer.sharedMaterial = State switch
            {
                StageState.Locked => lockedMaterial,
                StageState.Available => availableMaterial,
                _ => clearedMaterial
            };
        }
    }
}
