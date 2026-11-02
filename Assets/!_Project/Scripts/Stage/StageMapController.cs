using System.Collections;
using System.Linq;
using CodingGame.Art.StageSelection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageMapController : MonoBehaviour
    {
        StageNode[] nodes;
        Camera mapCamera;
        StageCameraController cameraController;
        StageMapHUD hud;
        string toast;
        bool entering, unlocking;
        StageNode recommendedStage;
        string guideMessage;
        public StageNode RecommendedStage => recommendedStage;
        public string GuideMessage => guideMessage;
        public bool IsUnlocking => unlocking;
        public StageMapHUD HUD => hud;

        public void Configure(StageNode[] stageNodes, Camera camera)
        {
            nodes = stageNodes; mapCamera = camera;
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
                ShowToast($"잠긴 스테이지 · {node.Number - 1}스테이지를 먼저 클리어해 주세요.");
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
            if (target == null) { ShowToast("모든 스테이지가 클리어되었습니다."); return; }
            StageProgress.MarkClearedForPreview(target.Number);
            RefreshNodes();
            if (!PlayPendingUnlock()) ShowToast("모든 섬을 정화했어요!");
        }

        void DeveloperReset()
        {
            if (entering || unlocking) return;
            StageProgress.ResetAll(nodes.Length);
            RefreshNodes();
            ShowToast("DEV: 진행 상태 초기화");
        }

        void RefreshNodes()
        {
            recommendedStage = null;
            foreach (var node in nodes)
            {
                node.Refresh();
                if (node.State == StageState.Available && (!recommendedStage || node.Number < recommendedStage.Number)) recommendedStage = node;
            }
            guideMessage = recommendedStage ? $"{recommendedStage.Number}스테이지에 도전해서 오류를 정화해 줘!" : "모든 섬이 정화됐어! 다시 도전할 수도 있어.";
            hud.Refresh(recommendedStage,guideMessage);
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
            hud.Refresh(null,"오류를 제거했어! 섬이 깨끗하게 정화되고 있어.");
            var sourceIsland=source.GetComponent<StageIslandView>();
            if(sourceIsland)sourceIsland.BeginPurification();
            source.PurificationVisual.Begin();
            if(target)target.LockVisual.SetLocked(true);
            var island = target?target.GetComponent<StageIslandView>():null;
            if(island) island.RefreshState(StageState.Locked);
            hud.ShowToast($"STAGE {source.Number:00} · 오류 제거 완료, 정화 중",3,StageMapHUD.PurifiedColor);
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
            hud.ShowToast($"STAGE {source.Number:00} · 정화 완료",2,StageMapHUD.PurifiedColor);
            yield return new WaitForSecondsRealtime(.35f);
            if(!target)
            {
                hud.ShowToast("모든 섬의 정화가 완료됐어요!",3,StageMapHUD.PurifiedColor);
                cameraController.RestoreOverview();yield return new WaitForSecondsRealtime(1.1f);
                unlocking=false;hud.SetCinematic(false);RefreshNodes();yield break;
            }
            hud.SetCinematic(true,target);
            hud.Refresh(null,"정화된 섬의 에너지가 다음 섬의 봉인을 풀고 있어!");
            hud.ShowToast("정화 완료 → 회로 연결 → 다음 섬 봉인 해제",4,StageMapHUD.OpenColor);
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
            hud.ShowToast($"STAGE {target.Number:00} · 봉인 해제! 정화에 도전할 수 있어요.",3,StageMapHUD.OpenColor);
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

        void ShowToast(string message) { toast=message; hud.ShowToast(message); }
        // Kept in screen coordinates for input/viewport probes.
        Rect CardRect(int index) => hud.CardScreenRect(index);
    }
}
