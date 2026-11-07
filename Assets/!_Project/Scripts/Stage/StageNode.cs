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
        StageLockVisual lockVisual;
        StagePurificationVisual purificationVisual;
        public StageLockVisual LockVisual => lockVisual;
        public StagePurificationVisual PurificationVisual => purificationVisual;

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
            // The lock barrier and the purification field are built once in the editor and saved with the scene (13차,
            // Tools/Optimize/Stage Visuals); a scene that never had them builds them here.
            lockVisual = GetComponentInChildren<StageLockVisual>(true);
            if (!lockVisual)
            {
                var effect = new GameObject("Stage lock barrier");
                effect.transform.SetParent(transform, false);
                lockVisual = effect.AddComponent<StageLockVisual>();
            }
            lockVisual.Configure(VisualBounds, number);
            purificationVisual = GetComponentInChildren<StagePurificationVisual>(true);
            if (!purificationVisual)
            {
                var purification = new GameObject("Stage purification"); purification.transform.SetParent(transform, false);
                purificationVisual = purification.AddComponent<StagePurificationVisual>();
            }
            purificationVisual.Configure(VisualBounds);
        }

        public void Refresh()
        {
            State = StageProgress.GetState(Number);
            lockVisual.SetLocked(State == StageState.Locked);
            purificationVisual.SetCleared(State == StageState.Cleared);
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
