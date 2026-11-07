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
        GUIStyle bodyStyle, guideTitleStyle, guideBodyStyle;
        Texture2D guideFace; // 디버, the tutorial's guide robot, so the map talks in the same voice
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
            guideFace = Resources.Load<Texture2D>("Guide/Guide_normal");
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
            bool busy = entering || unlocking || cameraController.IsMoving;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.wasPressedThisFrame) { DeveloperClear(); return; }
                if (keyboard.qKey.wasPressedThisFrame) { DeveloperReset(); return; }
            }
#endif
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
            entering = true; enteredAt = Time.unscaledTime; hud.SetEntering();
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
            guideMessage = recommendedStage ? $"오류 코드가 {StageProgress.Title(recommendedStage.Number)}에 몰려오고 있어! 빛나는 섬을 눌러 줘!" :
                nodes.Any(node => StageProgress.BestStars(node.Number) < 3) ? "다녀온 구역에 다시 가 볼까? 코어를 더 많이 지키면 별이 늘어나!" : "모든 구역에서 별 세 개! 이번엔 다른 코드로 막아 볼래?";
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

        // Kept in screen coordinates for input/viewport probes.
        Rect CardRect(int index) => hud.CardScreenRect(index);

        // The toy-workshop look from the battle (UITheme): paper cards and pills with an ink outline, Jalnan for names.
        // The island cards, locks and seals are StageMapHUD's (uGUI); IMGUI here draws only 디버's card and the "여기야!" pointer.
        Texture2D card, pill, badge, arrow;
        GUIStyle sticker, displayStyle, devStyle, hintStyle;
        void EnsureStyles()
        {
            if (bodyStyle != null) return;
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 28);
            Font display = UITheme.Display && UITheme.Display.sourceFontFile ? UITheme.Display.sourceFontFile : font;
            Font bold = UITheme.Bold && UITheme.Bold.sourceFontFile ? UITheme.Bold.sourceFontFile : font;
            card = Resources.Load<Texture2D>("UI/ui_card"); pill = Resources.Load<Texture2D>("UI/ui_pill"); badge = Resources.Load<Texture2D>("UI/ui_circle"); arrow = Resources.Load<Texture2D>("UI/ui_arrow");
            sticker = new GUIStyle { normal = { background = card } };
            bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, normal = { textColor = UITheme.Ink } };
            displayStyle = new GUIStyle(bodyStyle) { font = display, wordWrap = false };
            guideTitleStyle = new GUIStyle(displayStyle) { normal = { textColor = UITheme.Hex(0x0C8A73) } };
            guideBodyStyle = new GUIStyle(bodyStyle) { font = bold, fontStyle = bold == font ? FontStyle.Bold : FontStyle.Normal };
            devStyle = new GUIStyle(bodyStyle) { normal = { textColor = new Color(1, 1, 1, .6f) } };
            hintStyle = new GUIStyle(displayStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        }
        // Sliced sticker art (borders as ui_sprites.py draws them) in a fill colour.
        void Sticker(Rect rect, Texture2D texture, Color fill)
        {
            if (!texture) { DrawRect(rect, fill); return; }
            sticker.normal.background = texture;
            sticker.border = texture == pill ? new RectOffset(27, 27, 27, 31) : new RectOffset(24, 24, 24, 30);
            // Shorter than its slice borders the corners overlap (the island's STAGE pill looked cut on top): draw it at
            // the borders' height and scale it down.
            float k = Mathf.Min(1, rect.height / sticker.border.vertical);
            var matrix = GUI.matrix;
            if (k < 1)
            {
                GUIUtility.ScaleAroundPivot(Vector2.one * k, rect.center);
                rect = new Rect(rect.center.x - rect.width / k * .5f, rect.center.y - sticker.border.vertical * .5f, rect.width / k, sticker.border.vertical);
            }
            var old = GUI.backgroundColor; GUI.backgroundColor = fill;
            GUI.Box(rect, GUIContent.none, sticker);
            GUI.backgroundColor = old; GUI.matrix = matrix;
        }

        void OnGUI()
        {
            // Absolute-position labels have no input/layout work; draw once per repaint.
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            // 디버's card fades out once a stage is chosen, while the camera flies there, and sits out the unlock cinematic.
            float guide = entering ? 1 - Mathf.Clamp01((Time.unscaledTime - enteredAt) / GuideFadeSeconds) : unlocking ? 0 : 1;
            if (guide > 0) DrawGuide(guide);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Text(new Rect(18f, Screen.height - 38f, 420f, 28f), "DEV  Z: 임시 클리어    Q: 초기화", devStyle);
#endif
            DrawEnterHint();
        }

        // 디버 on a mint badge, talking from a paper card at the top middle, sized for a 1080p screen.
        void DrawGuide(float alpha)
        {
            var faded = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            float s = Screen.height / 1080f, cardWidth = Mathf.Min(1000 * s, Screen.width - 36), cardX = (Screen.width - cardWidth) * .5f, cardY = 18 * s;
            Sticker(new Rect(cardX, cardY, cardWidth, 138 * s), card, UITheme.Paper);
            float textX = cardX + 22 * s;
            if (guideFace)
            {
                if (badge) { var tint = GUI.color; GUI.color = tint * UITheme.Mint; GUI.DrawTexture(new Rect(cardX + 14 * s, cardY + 10 * s, 112 * s, 116 * s), badge, ScaleMode.ScaleToFit); GUI.color = tint; }
                GUI.DrawTexture(new Rect(cardX + 19 * s, cardY + 15 * s, 102 * s, 102 * s), guideFace, ScaleMode.ScaleToFit); textX = cardX + 142 * s;
            }
            guideTitleStyle.fontSize = Mathf.RoundToInt(26 * s); guideBodyStyle.fontSize = Mathf.RoundToInt(25 * s);
            Text(new Rect(textX, cardY + 12 * s, cardWidth, 36 * s), "디버", guideTitleStyle);
            Text(new Rect(textX, cardY + 50 * s, cardX + cardWidth - textX - 20 * s, 76 * s), GuideMessage, guideBodyStyle);
            GUI.color = faded;
        }

        // A bouncing arrow over the first stage that can be entered, with its name on a sunny pill, so the player knows where to click.
        // The HUD decides when it shows (hidden while the camera moves, during the unlock cinematic and after a stage is chosen).
        void DrawEnterHint()
        {
            var node = RecommendedStage;
            if (!node || !mapCamera || !hud.IsHintVisible) return;
            var bounds = node.VisualBounds;
            Vector3 top = mapCamera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y);
            if (top.z <= 0) return;
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 16;
            const float hintLift = 12;
            string hint = $"{StageProgress.Title(node.Number)} · 여기야!";
            float width = hintStyle.CalcSize(new GUIContent(hint)).x + 48;
            if (arrow) { var tint = GUI.color; GUI.color = UITheme.Primary; GUI.DrawTexture(new Rect(top.x - 30, Screen.height - top.y - 78 - bounce - hintLift, 60, 63), arrow, ScaleMode.ScaleToFit); GUI.color = tint; }
            var plate = new Rect(top.x - width / 2, Screen.height - top.y - 126 - bounce - hintLift, width, 50);
            Sticker(plate, pill, UITheme.Primary);
            Text(new Rect(plate.x, plate.y, plate.width, plate.height - 5), hint, hintStyle);
        }

        // A label that never takes its hover look: IMGUI labels under the mouse switch to the skin's near-white hover
        // text colour, which washed out whichever island card the cursor rested on.
        static void Text(Rect rect, string text, GUIStyle style) => style.Draw(rect, new GUIContent(text), false, false, false, false);

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
