using UnityEditor;
using UnityEngine;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapPainterSurfaceAssets
    {
        public static Material StylizedMaterial()
        {
            const string path=MapPainterAssets.Generated+"/Modules/StylizedMapSurface.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material)return material;
            var shader=Shader.Find("CodingGame/Art/Stylized Map Surface");
            if(!shader)throw new System.InvalidOperationException("Stylized Map Surface shader has not imported yet.");
            material=new Material(shader) {name="StylizedMapSurface"};
            AssetDatabase.CreateAsset(material,path); return material;
        }
    }
}
