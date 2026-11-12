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
        public bool IsPurifying { get; private set; }
        public float Purification { get; private set; }
        static readonly Color Unpurified = new Color(1,.61f,.22f);
        static readonly Color Purified = new Color(.24f,1,.61f);
        public Bounds VisualBounds
        {
            get
            {
                if (boundsRenderers == null) boundsRenderers = CollectRenderers();
                var bounds=new Bounds(transform.position,Vector3.zero);
                foreach(var renderer in boundsRenderers)
                    if (renderer && renderer.gameObject.activeInHierarchy) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
        }
        void OnEnable()
        {
            originalColors.Clear();shown=(StageState)(-1);
            boundsRenderers=CollectRenderers();
            if (block == null) block = new MaterialPropertyBlock();
            foreach(var renderer in boundsRenderers)
                if(renderer.sharedMaterial&&renderer.sharedMaterial.HasProperty(BaseColor))originalColors[renderer]=renderer.sharedMaterial.GetColor(BaseColor);
            if(visualRoot){rest=visualRoot.localPosition;restScale=visualRoot.localScale;}
            RefreshState(Application.isPlaying?StageProgress.GetState(stageNumber):stageNumber==1?StageState.Available:StageState.Locked);
        }
        public void RefreshState(StageState state)
        {
            if(shown==state && !IsPurifying)return;shown=state;
            IsPurifying=false; Purification=state==StageState.Cleared?1:0;
            ApplyLook();
        }
        public void BeginPurification()
        {
            IsPurifying=true; Purification=0; ApplyLook();
        }
        public void Purify(float progress)
        {
            Purification=Mathf.Clamp01(progress); IsPurifying=Purification<1;
            ApplyLook();
        }
        void ApplyLook()
        {
            float scanY=VisualBounds.min.y+VisualBounds.size.y*Purification;
            foreach(var pair in originalColors)
            {
                if(!pair.Key)continue;pair.Key.GetPropertyBlock(block);
                Color color=pair.Value;
                if(pair.Key==statusRing)color=shown==StageState.Locked?new Color(.58f,.3f,.08f):Color.Lerp(Unpurified,Purified,Purification)*1.7f;
                else if(shown==StageState.Locked)color=Color.Lerp(color,new Color(.25f,.32f,.42f),.65f)*.68f;
                else color=Color.Lerp(Color.Lerp(color,new Color(.39f,.3f,.23f),.16f)*.80f,Color.Lerp(color,new Color(.68f,1,.82f),.18f)*1.16f,Purification);
                color.a=1;block.SetColor(BaseColor,color);
                block.SetFloat("_PurifyAmount",Purification);
                block.SetFloat("_PurifyScanY",scanY);
                block.SetFloat("_PurifyFlash",IsPurifying?Mathf.Sin(Purification*Mathf.PI):0);
                pair.Key.SetPropertyBlock(block);
            }
        }
        // Set by StageMapController every frame: OnMouseEnter never fires, the project reads input only through the Input System.
        public void SetHovered(bool on){hovering=on;}
        // 0 to 1, eased: how far the hover rise has gone (the Enter hint swells with it).
        public float HoverLift=>hover*hover*(3-2*hover);
        void LateUpdate()
        {
            if(!Application.isPlaying)return;
            // Under the mouse the island rises and swells a little (a locked one only stirs), easing in and out over .15 s.
            hover=Mathf.MoveTowards(hover,hovering?(shown==StageState.Locked?.3f:1):0,Time.unscaledDeltaTime/.15f);
            float lift=hover*hover*(3-2*hover);
            if(visualRoot){visualRoot.localPosition=rest+Vector3.up*(Mathf.Sin(Time.unscaledTime*1.05f+stageNumber*1.7f)*.035f+lift*.35f);visualRoot.localScale=restScale*(1+.07f*lift);}
            if(shown!=StageState.Locked&&rotors!=null)foreach(var rotor in rotors)if(rotor)rotor.Rotate(0,Time.unscaledDeltaTime*18,0,Space.Self);
            if(statusRing&&shown!=StageState.Locked)
            {
                statusRing.GetPropertyBlock(block);
                var color=Color.Lerp(Unpurified,Purified,Purification)*(1.55f+.12f*Mathf.Sin(Time.unscaledTime*2.5f)+lift*.6f);
                color.a=1;block.SetColor(BaseColor,color);statusRing.SetPropertyBlock(block);
            }
        }
        void OnDisable(){if(visualRoot){visualRoot.localPosition=rest;visualRoot.localScale=restScale;}hover=0;}
        Renderer[] CollectRenderers()
        {
            var renderers = new List<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (!renderer.GetComponentInParent<StageLockVisual>() && !renderer.GetComponentInParent<StagePurificationVisual>()) renderers.Add(renderer);
            return renderers.ToArray();
        }
        void OnTransformChildrenChanged(){boundsRenderers=null;}
    }
}
