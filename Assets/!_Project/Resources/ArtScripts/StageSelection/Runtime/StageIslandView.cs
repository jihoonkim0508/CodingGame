using System.Collections.Generic;
using CodingGame.StageMap;
using UnityEngine;

namespace CodingGame.Art.StageSelection
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class StageIslandView : MonoBehaviour
    {
        [Range(1,3)] public int stageNumber=1;
        public string stageTitle;
        public Color accent=Color.cyan;
        public Transform visualRoot;
        public Renderer statusRing;
        public Transform[] rotors;
        StageState shown=(StageState)(-1);
        readonly Dictionary<Renderer,Color> originalColors=new Dictionary<Renderer,Color>();
        Renderer[] boundsRenderers;
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Vector3 rest, restScale = Vector3.one;
        bool hovering;
        float hover;
        public Bounds VisualBounds
        {
            get
            {
                if (boundsRenderers == null) boundsRenderers = GetComponentsInChildren<Renderer>(true);
                var bounds=new Bounds(transform.position,Vector3.zero);
                foreach(var renderer in boundsRenderers)
                    if (renderer && renderer.gameObject.activeInHierarchy) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
        }
        void OnEnable()
        {
            originalColors.Clear();shown=(StageState)(-1);
            boundsRenderers=GetComponentsInChildren<Renderer>(true);
            if (block == null) block = new MaterialPropertyBlock();
            foreach(var renderer in boundsRenderers)
                if(renderer.sharedMaterial&&renderer.sharedMaterial.HasProperty(BaseColor))originalColors[renderer]=renderer.sharedMaterial.GetColor(BaseColor);
            if(visualRoot){rest=visualRoot.localPosition;restScale=visualRoot.localScale;}
            RefreshState(Application.isPlaying?StageProgress.GetState(stageNumber):stageNumber==1?StageState.Available:StageState.Locked);
        }
        public void RefreshState(StageState state)
        {
            if(shown==state)return;shown=state;
            foreach(var pair in originalColors)
            {
                if(!pair.Key)continue;pair.Key.GetPropertyBlock(block);
                Color color=pair.Value;
                if(pair.Key==statusRing)color=state==StageState.Locked?new Color(.23f,.34f,.46f):state==StageState.Cleared?new Color(.33f,1,.69f)*1.35f:accent*1.6f;
                else if(state==StageState.Locked)color=Color.Lerp(color,new Color(.30f,.38f,.48f),.22f)*.86f;
                color.a=1;block.SetColor(BaseColor,color);pair.Key.SetPropertyBlock(block);
            }
        }
        // Set by StageMapController every frame: OnMouseEnter never fires, the project reads input only through the Input System.
        public void SetHovered(bool on){hovering=on;}
        void LateUpdate()
        {
            if(!Application.isPlaying)return;
            // Under the mouse the island rises and swells a little (a locked one only stirs), easing in and out over .15 s.
            hover=Mathf.MoveTowards(hover,hovering?(shown==StageState.Locked?.3f:1):0,Time.unscaledDeltaTime/.15f);
            float lift=hover*hover*(3-2*hover);
            if(visualRoot){visualRoot.localPosition=rest+Vector3.up*(Mathf.Sin(Time.unscaledTime*1.05f+stageNumber*1.7f)*.035f+lift*.35f);visualRoot.localScale=restScale*(1+.07f*lift);}
            if(rotors!=null)foreach(var rotor in rotors)if(rotor)rotor.Rotate(0,Time.unscaledDeltaTime*18,0,Space.Self);
            if(statusRing&&shown!=StageState.Locked)
            {
                statusRing.GetPropertyBlock(block);
                var color=(shown==StageState.Cleared?new Color(.33f,1,.69f):accent)*(1.35f+.15f*Mathf.Sin(Time.unscaledTime*2.5f)+lift*.6f);
                color.a=1;block.SetColor(BaseColor,color);statusRing.SetPropertyBlock(block);
            }
        }
        void OnDisable(){if(visualRoot){visualRoot.localPosition=rest;visualRoot.localScale=restScale;}hover=0;}
        void OnTransformChildrenChanged(){boundsRenderers=null;}
    }
}
