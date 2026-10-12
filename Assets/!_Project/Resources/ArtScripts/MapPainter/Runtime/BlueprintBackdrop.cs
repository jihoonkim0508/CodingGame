using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.Art.MapPainter
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class BlueprintBackdrop : MonoBehaviour
    {
        public static readonly Color DefaultCameraColor=new Color(.045f,.078f,.115f,1);
        [System.NonSerialized] public Camera targetCamera;
        private ModularMapRoot map;
        private MeshRenderer surface;
        private MaterialPropertyBlock block;
        private void OnEnable() { Refresh(); }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if(!map)map=GetComponentInParent<ModularMapRoot>();
            if(!surface)surface=GetComponent<MeshRenderer>();
            if(!map||!surface)return;
            if(!map.gameObject.scene.IsValid()||!map.gameObject.scene.isLoaded)return;
            if(surface.enabled!=map.blueprintBackground)surface.enabled=map.blueprintBackground;
            if(!targetCamera)
            {
                foreach(var root in map.gameObject.scene.GetRootGameObjects())
                    foreach(var camera in root.GetComponentsInChildren<Camera>(true))
                        if(!targetCamera||camera.CompareTag("MainCamera"))targetCamera=camera;
            }
            if(!map.blueprintBackground)
            {
                if(targetCamera && targetCamera.backgroundColor==map.blueprintBlue)
                    targetCamera.backgroundColor=DefaultCameraColor;
                return;
            }
            surface.shadowCastingMode=ShadowCastingMode.Off; surface.receiveShadows=false;
            float y=map.transform.position.y-map.floorHeight-.08f;
            Vector3 min=map.transform.position+new Vector3(-40,0,-40),max=map.transform.position+new Vector3(40,0,40);
            if(targetCamera)
            {
                targetCamera.clearFlags=CameraClearFlags.SolidColor;
                if(targetCamera.backgroundColor!=map.blueprintBlue)targetCamera.backgroundColor=map.blueprintBlue;
                var plane=new Plane(Vector3.up,new Vector3(0,y,0)); bool first=true;
                for(int i=0;i<4;i++)
                {
                    var ray=targetCamera.ViewportPointToRay(new Vector3((i&1)==0?0:1,(i&2)==0?0:1,0));
                    if(!plane.Raycast(ray,out float distance))continue;
                    Vector3 point=ray.GetPoint(distance);
                    if(first) {min=max=point; first=false;} else {min=Vector3.Min(min,point); max=Vector3.Max(max,point);}
                }
            }
            Vector3 position=new Vector3((min.x+max.x)*.5f,y,(min.z+max.z)*.5f);
            Vector3 scale=new Vector3(Mathf.Max(2,max.x-min.x+map.cellSize*4),Mathf.Max(2,max.z-min.z+map.cellSize*4),1);
            if(Vector3.Distance(transform.position,position)>.0001f)transform.position=position;
            if(Vector3.Distance(transform.localScale,scale)>.0001f)transform.localScale=scale;
            var rotation=Quaternion.Euler(90,0,0); if(Quaternion.Angle(transform.rotation,rotation)>.001f)transform.rotation=rotation;
            if(block==null)block=new MaterialPropertyBlock();
            block.SetColor("_PaperColor",map.blueprintBlue); block.SetColor("_LineColor",map.blueprintLines);
            block.SetFloat("_GridSpacing",Mathf.Max(.05f,map.blueprintSpacing)); block.SetFloat("_LineWidth",map.blueprintLineWidth);
            block.SetFloat("_MinorOpacity",map.blueprintMinorOpacity); block.SetFloat("_MajorOpacity",map.blueprintMajorOpacity);
            block.SetFloat("_MajorEvery",map.blueprintMajorEvery);
            block.SetVector("_GridOrigin",new Vector4(map.transform.position.x+map.cellSize*.5f,map.transform.position.z+map.cellSize*.5f,0,0));
            surface.SetPropertyBlock(block);
        }
    }
}
