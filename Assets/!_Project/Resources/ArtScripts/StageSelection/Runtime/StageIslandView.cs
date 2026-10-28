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
        Vector3 rest;
        bool hovering;
        public Bounds VisualBounds
        {
            get
            {
                var renderers=GetComponentsInChildren<Renderer>();
                var bounds=new Bounds(transform.position,Vector3.zero);
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
        }
        void OnEnable()
        {
            originalColors.Clear();shown=(StageState)(-1);
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterial&&renderer.sharedMaterial.HasProperty("_BaseColor"))originalColors[renderer]=renderer.sharedMaterial.GetColor("_BaseColor");
            if(visualRoot)rest=visualRoot.localPosition;
            RefreshState(Application.isPlaying?StageProgress.GetState(stageNumber):stageNumber==1?StageState.Available:StageState.Locked);
        }
        public void RefreshState(StageState state)
        {
            if(shown==state)return;shown=state;
            foreach(var pair in originalColors)
            {
                if(!pair.Key)continue;var block=new MaterialPropertyBlock();pair.Key.GetPropertyBlock(block);
                Color color=pair.Value;
                if(pair.Key==statusRing)color=state==StageState.Locked?new Color(.23f,.34f,.46f):state==StageState.Cleared?new Color(.33f,1,.69f)*1.35f:accent*1.6f;
                else if(state==StageState.Locked)color=Color.Lerp(color,new Color(.30f,.38f,.48f),.22f)*.86f;
                color.a=1;block.SetColor("_BaseColor",color);pair.Key.SetPropertyBlock(block);
            }
        }
        void OnMouseEnter(){hovering=true;}
        void OnMouseExit(){hovering=false;}
        void LateUpdate()
        {
            if(!Application.isPlaying)return;
            if(visualRoot)visualRoot.localPosition=rest+Vector3.up*(Mathf.Sin(Time.unscaledTime*1.05f+stageNumber*1.7f)*.035f);
            if(rotors!=null)foreach(var rotor in rotors)if(rotor)rotor.Rotate(0,Time.unscaledDeltaTime*18,0,Space.Self);
            if(statusRing&&shown!=StageState.Locked)
            {
                var block=new MaterialPropertyBlock();statusRing.GetPropertyBlock(block);
                var color=(shown==StageState.Cleared?new Color(.33f,1,.69f):accent)*(1.35f+.15f*Mathf.Sin(Time.unscaledTime*2.5f)+(hovering?.3f:0));
                color.a=1;block.SetColor("_BaseColor",color);statusRing.SetPropertyBlock(block);
            }
        }
        void OnDisable(){if(visualRoot)visualRoot.localPosition=rest;}
    }
}
