using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class BlueprintBackdropAssets
    {
        public static BlueprintBackdrop Ensure(ModularMapRoot map,Camera camera,bool undo=true)
        {
            var backdrop=map.GetComponentInChildren<BlueprintBackdrop>(true);
            if(!backdrop)
            {
                var material=MapPainterAssets.LoadOrCreateMaterial(MapPainterAssets.Generated+"/BlueprintGrid.mat","CodingGame/Art/Blueprint Grid");
                var go=GameObject.CreatePrimitive(PrimitiveType.Quad); go.name="Blueprint Background";
                SceneManager.MoveGameObjectToScene(go,map.gameObject.scene); go.transform.SetParent(map.transform,false);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<MeshRenderer>().sharedMaterial=material;
                backdrop=go.AddComponent<BlueprintBackdrop>();
                if(undo)Undo.RegisterCreatedObjectUndo(go,"Create blueprint background");
            }
            backdrop.targetCamera=camera; backdrop.Refresh(); return backdrop;
        }
    }
}
