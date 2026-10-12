using UnityEngine;

namespace CodingGame.StageMap
{
    [CreateAssetMenu(menuName = "Coding Game/Stage Material Palette")]
    public sealed class StageMaterialPalette : ScriptableObject
    {
        public Material AvailableMaterial;
        public Material LockedMaterial;
        public Material[] ClearedMaterials;

        public Material GetClearedMaterial(int index, Material fallback)
        {
            if (ClearedMaterials == null || index < 0 || index >= ClearedMaterials.Length)
                return fallback;
            return ClearedMaterials[index] ? ClearedMaterials[index] : fallback;
        }
    }
}
