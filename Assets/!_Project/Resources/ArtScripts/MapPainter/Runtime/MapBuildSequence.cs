using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MapBuildSequence : MonoBehaviour
    {
        public bool playOnStart=true;
        [Min(.5f)] public float duration=2.4f;
        [Min(0)] public float riseHeight=.6f;
        [Range(0,1)] public float glitch=.15f;
        public Color outlineColor=new Color(.65f,.95f,1,1);
        public Material outlineMaterial;
        [SerializeField,HideInInspector] private string playbackKey;
        private static readonly HashSet<string> played=new HashSet<string>();
        private static string lastScene;
        private MapBuildPlayback playback;
        private Coroutine routine;
        public bool IsAnimating=>playback!=null;
        public event Action Completed;
        public string PlaybackKey=>string.IsNullOrEmpty(playbackKey)?gameObject.scene.path+"/"+name:playbackKey;
        public void EnsureIdentity() { if(string.IsNullOrEmpty(playbackKey))playbackKey=Guid.NewGuid().ToString("N"); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            played.Clear();lastScene=null;SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded;
        }
        // Call before loading/activating a stage when the selection UI reuses the same scene.
        public static void BeginStageEntry() { played.Clear(); }
        private static void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(mode==LoadSceneMode.Single)
            {
                string key=string.IsNullOrEmpty(scene.path)?scene.name:scene.path;
                if(lastScene!=key)BeginStageEntry();
                lastScene=key;
            }
            PlayExistingMaps();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PlayExistingMaps()
        {
            if(lastScene==null)
            {
                var scene=SceneManager.GetActiveScene();
                if(scene.IsValid()&&scene.isLoaded)lastScene=string.IsNullOrEmpty(scene.path)?scene.name:scene.path;
            }
            foreach(var sequence in UnityEngine.Object.FindObjectsByType<MapBuildSequence>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(sequence.isActiveAndEnabled)sequence.TryPlayOnce();
        }
        private void OnEnable() { if(Application.isPlaying)TryPlayOnce(); }
        private void Start() { TryPlayOnce(); }
        private void OnDisable() { Finish(false); }
        private bool TryPlayOnce()
        {
            if(!Application.isPlaying||!isActiveAndEnabled||!playOnStart||played.Contains(PlaybackKey))return false;
            if(!gameObject.scene.isLoaded)return false;
            var map=GetComponent<ModularMapRoot>();
            if(!map||map.floorCells.Count==0)return false;
            played.Add(PlaybackKey);
            if(IsAnimating)return false;
            Begin();routine=StartCoroutine(Animate());return true;
        }
        private IEnumerator Animate()
        {
            float elapsed=0;
            while(playback!=null&&elapsed<duration)
            {
                playback.Sample(elapsed/Mathf.Max(.5f,duration));
                yield return null; elapsed+=Time.unscaledDeltaTime;
            }
            routine=null; Finish();
        }
        public void BeginPreview() { if(Application.isPlaying)return; Begin(); }
        // For a selection UI that activates an already-visible map without loading a scene.
        public void PlayStageEntry() { BeginStageEntry();TryPlayOnce(); }
        private void Begin()
        {
            Finish(false); playback=new MapBuildPlayback(GetComponent<ModularMapRoot>(),this); playback.Sample(0);
        }
        public void SamplePreview(float normalizedTime) { playback?.Sample(normalizedTime); }
        public void Finish(bool notify=true)
        {
            if(routine!=null){StopCoroutine(routine);routine=null;}
            if(playback==null)return;
            playback.Restore(); playback=null; if(notify)Completed?.Invoke();
        }
    }
}
