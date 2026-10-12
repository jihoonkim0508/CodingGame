using UnityEditor;
using UnityEngine;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapBuildAssets
    {
        public static MapBuildSequence Ensure(ModularMapRoot map,bool undo=true)
        {
            var sequence=map.GetComponent<MapBuildSequence>();
            if(!sequence)sequence=undo?Undo.AddComponent<MapBuildSequence>(map.gameObject):map.gameObject.AddComponent<MapBuildSequence>();
            if(!sequence.outlineMaterial)
            {
                var material=MapPainterAssets.LoadOrCreateMaterial(MapPainterAssets.Generated+"/MapBuildOutline.mat","CodingGame/Art/Map Build Outline");
                if(undo)Undo.RecordObject(sequence,"Configure map build animation");
                sequence.outlineMaterial=material;
            }
            sequence.EnsureIdentity();EditorUtility.SetDirty(sequence);return sequence;
        }
    }
}
