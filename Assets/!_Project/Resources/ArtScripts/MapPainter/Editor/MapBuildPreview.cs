using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    [InitializeOnLoad]
    public static class MapBuildPreview
    {
        private static MapBuildSequence current;
        private static double started;
        public static bool IsRunning=>current&&current.IsAnimating;
        public static float Progress=>IsRunning?Mathf.Clamp01((float)(EditorApplication.timeSinceStartup-started)/current.duration):0;
        static MapBuildPreview()
        {
            EditorApplication.update+=Update;
            AssemblyReloadEvents.beforeAssemblyReload+=Stop;
            EditorApplication.quitting+=Stop;
            EditorSceneManager.sceneSaving+=OnSaving;
            EditorSceneManager.sceneClosing+=OnClosing;
            Undo.undoRedoPerformed+=Stop;
            EditorApplication.playModeStateChanged+=OnPlayChanged;
        }
        public static void Start(MapBuildSequence sequence)
        {
            Stop();
            if(!sequence||Application.isPlaying||!MapPainterEditing.CanEdit(sequence.GetComponent<ModularMapRoot>()))return;
            if(AnimationMode.InAnimationMode())throw new InvalidOperationException("다른 애니메이션 미리보기를 종료한 뒤 실행하세요.");
            AnimationMode.StartAnimationMode();current=sequence;
            try
            {
                AnimationMode.BeginSampling();
                try
                {
                    foreach(var marker in sequence.GetComponentsInChildren<MapTileMarker>(true))
                    {
                        TrackVector(marker.transform,"m_LocalPosition",marker.transform.localPosition);
                        TrackVector(marker.transform,"m_LocalScale",marker.transform.localScale);
                    }
                    foreach(var renderer in sequence.GetComponentsInChildren<Renderer>(true))
                        if(!renderer.GetComponent<BlueprintBackdrop>())Track(renderer,"m_Enabled",renderer.enabled?"1":"0");
                }
                finally {AnimationMode.EndSampling();}
                sequence.BeginPreview();started=EditorApplication.timeSinceStartup;
            }
            catch {Stop();throw;}
            Repaint();
        }
        private static void TrackVector(Transform target,string property,Vector3 value)
        {
            Track(target,property+".x",value.x.ToString("R",CultureInfo.InvariantCulture));
            Track(target,property+".y",value.y.ToString("R",CultureInfo.InvariantCulture));
            Track(target,property+".z",value.z.ToString("R",CultureInfo.InvariantCulture));
        }
        private static void Track(Component target,string property,string value)
        {
            var binding=EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(target.transform,current.transform),target.GetType(),property);
            AnimationMode.AddPropertyModification(binding,new PropertyModification{target=target,propertyPath=property,value=value},true);
        }
        public static void Stop()
        {
            bool owned=current;
            if(current)current.Finish(false);current=null;
            if(owned&&AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();
            Repaint();
        }
        private static void Update()
        {
            if(!current)return;
            if(!IsRunning||!AnimationMode.InAnimationMode()||Application.isPlaying){Stop();return;}
            float time=(float)(EditorApplication.timeSinceStartup-started)/Mathf.Max(.5f,current.duration);
            if(time>=1){Stop();return;}
            current.SamplePreview(time);Repaint();
        }
        private static void Repaint() {SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();}
        private static void OnSaving(Scene scene,string path){Stop();}
        private static void OnClosing(Scene scene,bool removing){Stop();}
        private static void OnPlayChanged(PlayModeStateChange state)
        {
            Stop();
            if(state==PlayModeStateChange.ExitingPlayMode)
                foreach(var sequence in UnityEngine.Object.FindObjectsByType<MapBuildSequence>(FindObjectsInactive.Include,FindObjectsSortMode.None))sequence.Finish(false);
        }
    }
}
