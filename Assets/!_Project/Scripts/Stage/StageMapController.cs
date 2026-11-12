using System.Collections;
using System.Linq;
using CodingGame.Art.StageSelection;
using CodingGame.Defense;
using TMPro;
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
        StageMapMenu menu;
        // 디버 talks in the battle's dialogue box (11차 116), copied by UIRequests9.BuildGuideDialogue.
        public const string GuideDialogue = "UI/GuideDialogue", GuideSpeaker = "디버";
        CanvasGroup guideGroup;
        TMP_Text guideLine;
        bool entering, unlocking; // entering: a stage was chosen, the camera flies there and the scene fades
        float enteredAt;
        const float GuideFadeSeconds = .35f;
        StageNode recommendedStage, hovered;
        string guideMessage = "지켜 낸 구역에도 언제든 다시 갈 수 있어. 섬을 눌러 봐!";
        StageIslandView[] islands;
        public StageNode RecommendedStage => recommendedStage;
        public string GuideMessage => guideMessage;
        public bool IsUnlocking => unlocking;
        public StageMapHUD HUD => hud;

        public void Configure(StageNode[] stageNodes, Camera camera)
        {
            nodes = stageNodes; mapCamera = camera;
            islands = nodes.Select(node => node.GetComponent<StageIslandView>()).ToArray();
            var cameraData=camera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality=AntialiasingQuality.High;
            cameraData.renderPostProcessing=true;
            cameraController = gameObject.AddComponent<StageCameraController>();
            cameraController.Configure(camera, nodes);
            // The map UI is saved in Stage.unity (13차 125); a scene without it gets one built here.
            hud = FindFirstObjectByType<StageMapHUD>(FindObjectsInactive.Include);
            if (!hud) hud = gameObject.AddComponent<StageMapHUD>();
            hud.Configure(nodes, camera, cameraController, Enter);
            var guide = hud.GuideDialogue;
            if (guide)
            {
                guideGroup = guide.GetComponent<CanvasGroup>();
                guideLine = guide.transform.Find("DialoguePanel/Dialogue").GetComponent<TMP_Text>();
                guide.transform.Find("DialoguePanel/SpeakerName").GetComponent<TMP_Text>().text = GuideSpeaker;
            }
            menu = gameObject.AddComponent<StageMapMenu>();
            menu.Configure(hud.Canvas, v => { if (voice) voice.volume = v; });
            RefreshNodes();
            PlayPendingUnlock();
        }

        void Update()
        {
            // 디버's box fades out once a stage is chosen, while the camera flies there, and sits out the unlock cinematic.
            if (guideGroup)
            {
                guideGroup.alpha = entering ? 1 - Mathf.Clamp01((GameClock.Now - enteredAt) / GuideFadeSeconds) : unlocking ? 0 : 1;
                if (guideLine.text != guideMessage) guideLine.text = guideMessage;
                SpeakGuide();
                if (entering && voice && voice.isPlaying) voice.volume = TutorialDialogueView.VoiceVolume * guideGroup.alpha; // fades with the box
            }
            // Esc opens and closes the menu (not once a stage is chosen or during an unlock); while it is open the map waits.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !entering && !unlocking) menu.Toggle();
            bool busy = entering || unlocking || cameraController.IsMoving || menu.Open;
            Mouse mouse = Mouse.current;
            StageNode over = null;
            // Native Canvas buttons handle cards. Only unobstructed map points raycast into the world.
            if (!busy && mouse != null && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()) &&
                Physics.Raycast(mapCamera.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit))
                over = hit.collider.GetComponentInParent<StageNode>();
            if (over != hovered)
            {
                hovered = over;
                if (over) UISound.Play("hover", .6f);
                for (int i = 0; i < nodes.Length; i++) if (islands[i]) islands[i].SetHovered(nodes[i] == over);
            }
            if (busy) return;
            if (GameFlow.Developer && keyboard != null) // developer mode only (opened on the title)
            {
                if (keyboard.zKey.wasPressedThisFrame) { DeveloperClear(); return; }
                if (keyboard.qKey.wasPressedThisFrame) { DeveloperReset(); return; }
            }
            if (over && mouse.leftButton.wasPressedThisFrame) Enter(over);
        }

        void Enter(StageNode node)
        {
            if (entering || unlocking || cameraController.IsMoving) return;
            if (node.State == StageState.Locked)
            {
                node.LockVisual.Reject();
                UISound.Play("deny");
                return;
            }
            UISound.Play("stage-select");
            StageProgress.SelectedStage = node.Number;
            entering = true; enteredAt = GameClock.Now; hud.SetEntering();
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
            guideMessage = recommendedStage ? Attack(recommendedStage.Number) :
                nodes.Any(node => StageProgress.BestStars(node.Number) < 3) ? Revisit : AllStars;
            hud.Refresh(recommendedStage);
        }

        static string Attack(int stage) => $"오류 코드가 {StageProgress.Title(stage)}에 몰려오고 있어! 빛나는 섬을 눌러 줘!";
        const string Revisit = "다녀온 구역에 다시 가 볼까? 코어를 더 많이 지키면 별이 늘어나!", AllStars = "모든 구역에서 별 세 개! 이번엔 다른 코드로 막아 볼래?";
        // Every line 디버 can say on the map, for the voice recorder (GuideVoice records them with the lessons' lines).
        public static string[] GuideLines => new[] { Attack(1), Attack(2), Attack(3), Revisit, AllStars };

        // 디버 reads the box aloud when its line changes, once the box is up (not while a stage is chosen or a cinematic runs).
        AudioSource voice;
        string spoken;
        void SpeakGuide()
        {
            if (entering || unlocking || spoken == guideMessage) return;
            spoken = guideMessage;
            if (!voice) { voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0; }
            voice.Stop();
            var clip = TutorialDialogueView.VoiceOn ? Resources.Load<AudioClip>("Voice/" + GuideSpeech.Key(guideMessage)) : null;
            if (clip) { voice.clip = clip; voice.volume = TutorialDialogueView.VoiceVolume; voice.Play(); }
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
            for(float elapsed=0;elapsed<1.25f;elapsed+=GameClock.Delta)
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
            for(float elapsed=0;elapsed<1.5f;elapsed+=GameClock.Delta)
            {
                float t = Mathf.Clamp01(elapsed/1.5f);
                effect.Show(Mathf.SmoothStep(0,1,t));
                if(t>.15f&&!approaching)cameraController.FollowUnlockPulse(effect.HeadPosition);
                if(!approaching && t>.68f) { approaching=true; cameraController.ShowUnlockIsland(target); }
                yield return null;
            }
            effect.Show(1); effect.Burst();
            for(float elapsed=0;elapsed<1f;elapsed+=GameClock.Delta)
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

        // Kept in screen coordinates for input/viewport probes.
        Rect CardRect(int index) => hud.CardScreenRect(index);
    }
}
