using UnityEngine;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageNode : MonoBehaviour
    {
        Renderer targetRenderer;
        Material availableMaterial, lockedMaterial, clearedMaterial;

        public int Index { get; private set; }
        public StageDefinition Definition { get; private set; }
        public StageState State { get; private set; }

        public void Configure(int index, StageDefinition definition, StageMaterialPalette palette)
        {
            Index = index;
            Definition = definition;
            targetRenderer = GetComponentInChildren<Renderer>();
            Material originalMaterial = targetRenderer ? targetRenderer.sharedMaterial : null;
            availableMaterial = palette && palette.AvailableMaterial ? palette.AvailableMaterial : originalMaterial;
            lockedMaterial = palette && palette.LockedMaterial ? palette.LockedMaterial : originalMaterial;
            clearedMaterial = palette ? palette.GetClearedMaterial(index, originalMaterial) : originalMaterial;
            if (!TryGetComponent<Collider>(out _)) gameObject.AddComponent<BoxCollider>();
        }

        public void SetState(StageState state, bool selected)
        {
            State = state;
            if (!targetRenderer) return;
            targetRenderer.sharedMaterial = state switch
            {
                StageState.Locked => lockedMaterial,
                StageState.Available => availableMaterial,
                _ => clearedMaterial
            };
        }
    }
}
