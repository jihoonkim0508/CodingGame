using System.Collections;
using System.Linq;
using CodingGame.Art.StageSelection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageMapController : MonoBehaviour
    {
        StageNode[] nodes;
        Camera mapCamera;
        StageCameraController cameraController;
        StageMapHUD hud;
        bool entering, unlocking;
        GUIStyle guideTitleStyle, guideBodyStyle, generatedTipStyle;
        Texture2D guidePortrait;
        StageNode recommendedStage;
        public StageNode RecommendedStage => recommendedStage;
        public bool IsUnlocking => unlocking;
        public StageMapHUD HUD => hud;

        public void Configure(StageNode[] stageNodes, Camera camera)
        {
            nodes = stageNodes; mapCamera = camera;
            var cameraData=camera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality=AntialiasingQuality.High;
            cameraData.renderPostProcessing=true;
            cameraController = gameObject.AddComponent<StageCameraController>();
            cameraController.Configure(camera, nodes);
            hud = gameObject.AddComponent<StageMapHUD>();
            hud.Configure(nodes, camera, Enter);
            RefreshNodes();
            PlayPendingUnlock();
        }

        void Update()
        {
            if (entering || unlocking || cameraController.IsMoving) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.wasPressedThisFrame) { DeveloperClear(); return; }
                if (keyboard.qKey.wasPressedThisFrame) { DeveloperReset(); return; }
            }
#endif
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            // Native Canvas buttons handle cards. Only unobstructed map clicks raycast into the world.
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            if (Physics.Raycast(mapCamera.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit) &&
                hit.collider.GetComponentInParent<StageNode>() is StageNode node) Enter(node);
        }

        void Enter(StageNode node)
        {
            if (entering || unlocking || cameraController.IsMoving) return;
            if (node.State == StageState.Locked)
            {
                node.LockVisual.Reject();
                return;
            }
            StageProgress.SelectedStage = node.Number;
            entering = true; hud.SetEntering();
            cameraController.Focus(node.transform);
            GameFlow.LoadScene(GameFlow.DefenseScene, StageCameraController.Duration);
        }

        void DeveloperClear()
        {
            if (entering || unlocking) return;
            StageNode target = nodes.FirstOrDefault(node => node.State == StageState.Available);
            if (target == null) return;
            StageProgress.MarkClearedForPreview(target.Number);
            RefreshNodes();
            PlayPendingUnlock();
        }

        void DeveloperReset()
        {
            if (entering || unlocking) return;
            StageProgress.ResetAll(nodes.Length);
            RefreshNodes();
        }

        void RefreshNodes()
        {
            recommendedStage = null;
            foreach (var node in nodes)
            {
                node.Refresh();
                if (node.State == StageState.Available && (!recommendedStage || node.Number < recommendedStage.Number)) recommendedStage = node;
            }
            hud.Refresh(recommendedStage);
        }

        bool PlayPendingUnlock()
        {
            int number = StageProgress.ConsumePendingUnlock();
            int purified = StageProgress.ConsumePendingPurification();
            var target = nodes.FirstOrDefault(n => n.Number == number && n.State == StageState.Available);
            var source = nodes.FirstOrDefault(n => n.Number == (purified>0?purified:number-1) && n.State==StageState.Cleared);
            if (!source) return false;
            StartCoroutine(UnlockRoute(source,target));
            return true;
        }

        IEnumerator UnlockRoute(StageNode source, StageNode target)
        {
            unlocking = true;
            hud.SetCinematic(true,source);
            var sourceIsland=source.GetComponent<StageIslandView>();
            if(sourceIsland)sourceIsland.BeginPurification();
            source.PurificationVisual.Begin();
            if(target)target.LockVisual.SetLocked(true);
            var island = target?target.GetComponent<StageIslandView>():null;
            if(island) island.RefreshState(StageState.Locked);
            yield return new WaitForSecondsRealtime(.4f);
            cameraController.ShowPurificationIsland(source);
            yield return new WaitForSecondsRealtime(.9f);
            cameraController.RiseOverPurification(source);
            for(float elapsed=0;elapsed<1.25f;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/1.25f));
                if(sourceIsland)sourceIsland.Purify(t);
                source.PurificationVisual.SetProgress(t);
                yield return null;
            }
            if(sourceIsland)sourceIsland.Purify(1);source.PurificationVisual.SetProgress(1);
            yield return new WaitForSecondsRealtime(.35f);
            if(!target)
            {
                cameraController.RestoreOverview();yield return new WaitForSecondsRealtime(1.1f);
                unlocking=false;hud.SetCinematic(false);RefreshNodes();yield break;
            }
            hud.SetCinematic(true,target);
            cameraController.ShowUnlockRoute(source,target);
            yield return new WaitForSecondsRealtime(.85f);
            var effectRoot = new GameObject("Stage unlock journey");
            effectRoot.transform.SetParent(transform,false);
            var effect = effectRoot.AddComponent<StageUnlockEffect>(); effect.Configure(source,target);
            bool approaching = false;
            for(float elapsed=0;elapsed<1.5f;elapsed+=Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed/1.5f);
                effect.Show(Mathf.SmoothStep(0,1,t));
                if(t>.15f&&!approaching)cameraController.FollowUnlockPulse(effect.HeadPosition);
                if(!approaching && t>.68f) { approaching=true; cameraController.ShowUnlockIsland(target); }
                yield return null;
            }
            effect.Show(1); effect.Burst();
            for(float elapsed=0;elapsed<1f;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed);
                target.LockVisual.Release(t);
                if(t>.45f && island) island.RefreshState(StageState.Available);
                yield return null;
            }
            target.Refresh();
            cameraController.ShowOpenedIsland(target);
            yield return new WaitForSecondsRealtime(.7f);
            cameraController.RestoreOverview();
            yield return new WaitForSecondsRealtime(1.1f);
            Destroy(effectRoot);
            unlocking=false; hud.SetCinematic(false); RefreshNodes();
        }

        void OnGUI()
        {
            if (!mapCamera || entering || unlocking || Event.current.type != EventType.Repaint) return;

            if (guideBodyStyle == null)
            {
                Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 28);
                guideBodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, normal = { textColor = Color.white } };
                guideTitleStyle = new GUIStyle(guideBodyStyle) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.35f, .9f, 1f) } };
                generatedTipStyle = new GUIStyle(GUI.skin.box)
                {
                    border = new RectOffset(96, 96, 96, 96),
                    normal = { background = Resources.Load<Texture2D>("LearningSkins/ToolPanel") }
                };
                guidePortrait = Resources.Load<Texture2D>("LearningSkins/StageGuide");
            }

            var previousMatrix = GUI.matrix;
            GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(1 / 6f, 1 / 6f, 1));
            GUI.Box(new Rect(18f * 6, 18f * 6, 550f * 6, 110f * 6), GUIContent.none, generatedTipStyle);
            GUI.matrix = previousMatrix;
            if (guidePortrait) GUI.DrawTexture(new Rect(28, 28, 86, 90), guidePortrait, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(126, 32, 420, 30), "도우미 로봇 · 스테이지 안내", guideTitleStyle);
            string message = recommendedStage
                ? $"빛나는 {recommendedStage.Number}스테이지 섬을 클릭해 줘!"
                : "클리어한 섬을 골라 다시 도전할 수 있어.";
            GUI.Label(new Rect(126, 65, 420, 45), message, guideBodyStyle);
        }

        // Kept in screen coordinates for input/viewport probes.
        Rect CardRect(int index) => hud.CardScreenRect(index);
    }
}
