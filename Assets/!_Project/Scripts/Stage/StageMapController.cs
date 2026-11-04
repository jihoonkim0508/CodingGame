using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageMapController : MonoBehaviour
    {
        StageNode[] nodes;
        Camera mapCamera;
        StageCameraController cameraController;
        GUIStyle bodyStyle, toastStyle, guideTitleStyle, guideBodyStyle;
        GUIStyle islandTitleStyle,islandNumberStyle,islandStateStyle,islandLessonStyle;
        Texture2D guideFace; // 디버, the tutorial's guide robot, so the map talks in the same voice
        string toast;
        bool entering; // a stage was chosen: the camera flies there and the scene fades, no more "click here"
        float toastUntil;
        StageNode recommendedStage;
        string guideMessage = "지켜 낸 구역에도 언제든 다시 갈 수 있어. 섬을 눌러 봐!";
        CodingGame.Art.StageSelection.StageIslandView[] islands;
        string[] stageLabels, stateLabels;
        public StageNode RecommendedStage => recommendedStage;
        public string GuideMessage => guideMessage;

        public void Configure(StageNode[] stageNodes, Camera camera)
        {
            nodes = stageNodes;
            islands = new CodingGame.Art.StageSelection.StageIslandView[nodes.Length];
            stageLabels = new string[nodes.Length]; stateLabels = new string[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                islands[i] = nodes[i].GetComponent<CodingGame.Art.StageSelection.StageIslandView>();
                stageLabels[i] = $"STAGE {nodes[i].Number:00}";
            }
            mapCamera = camera;
            guideFace = Resources.Load<Texture2D>("Guide/Guide_normal");
            cameraController = gameObject.AddComponent<StageCameraController>();
            cameraController.Configure(camera, stageNodes);
            RefreshNodes();
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.wasPressedThisFrame) DeveloperClear();
                if (keyboard.qKey.wasPressedThisFrame) DeveloperReset();
            }
#endif

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || cameraController.IsMoving) return;
            if (Physics.Raycast(mapCamera.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit) &&
                hit.collider.GetComponentInParent<StageNode>() is StageNode node) Enter(node);
        }

        void Enter(StageNode node)
        {
            if (node.State == StageState.Locked) { ShowToast($"STAGE {node.Number} - LOCKED"); UISound.Play("deny"); return; }
            UISound.Play("stage-select");
            StageProgress.SelectedStage = node.Number;
            entering = true;
            cameraController.Focus(node.transform);
            // The fade waits for the camera to land on the stage (planner: the click moved on too fast).
            GameFlow.LoadScene(GameFlow.DefenseScene, StageCameraController.Duration);
        }

        void DeveloperClear()
        {
            StageNode target = nodes.FirstOrDefault(node => node.State == StageState.Available);
            if (target == null) { ShowToast("모든 스테이지가 클리어되었습니다."); return; }
            StageProgress.MarkClearedForPreview(target.Number);
            ShowToast($"DEV: STAGE {target.Number} 임시 클리어");
            RefreshNodes();
        }

        void DeveloperReset()
        {
            StageProgress.ResetAll(nodes.Length);
            RefreshNodes();
            ShowToast("DEV: 진행 상태 초기화");
        }

        void RefreshNodes()
        {
            recommendedStage = null;
            for (int i = 0; i < nodes.Length; i++)
            {
                StageNode node = nodes[i]; node.Refresh();
                if (node.State == StageState.Available && (!recommendedStage || node.Number < recommendedStage.Number)) recommendedStage = node;
                int stars = StageProgress.BestStars(node.Number);
                stateLabels[i] = node.State == StageState.Locked ? "이전 스테이지 클리어 후 해금" : node.State != StageState.Cleared ? "도전 가능 · 클릭해서 시작" :
                    stars > 0 ? new string('★', stars) + new string('☆', 3 - stars) + " · 다시 플레이" : "클리어 · 다시 플레이";
            }
            guideMessage = recommendedStage ? $"오류 코드가 {StageProgress.Title(recommendedStage.Number)}에 몰려오고 있어! 빛나는 섬을 눌러 줘!" :
                nodes.Any(node => StageProgress.BestStars(node.Number) < 3) ? "다녀온 구역에 다시 가 볼까? 코어를 더 많이 지키면 별이 늘어나!" : "모든 구역에서 별 세 개! 이번엔 다른 코드로 막아 볼래?";
        }

        void ShowToast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 2.5f;
        }

        // The toy-workshop look from the battle (UITheme): paper cards and pills with an ink outline, Jalnan for names.
        Texture2D card, pill, badge;
        GUIStyle sticker, displayStyle;
        void EnsureStyles()
        {
            if (bodyStyle != null) return;
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 28);
            Font display = UITheme.Display && UITheme.Display.sourceFontFile ? UITheme.Display.sourceFontFile : font;
            card = Resources.Load<Texture2D>("UI/ui_card"); pill = Resources.Load<Texture2D>("UI/ui_pill"); badge = Resources.Load<Texture2D>("UI/ui_circle");
            sticker = new GUIStyle { normal = { background = card } };
            bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, normal = { textColor = UITheme.Ink } };
            displayStyle = new GUIStyle(bodyStyle) { font = display, wordWrap = false };
            toastStyle = new GUIStyle(displayStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            guideTitleStyle = new GUIStyle(displayStyle) { fontSize = 22, normal = { textColor = UITheme.Hex(0x0C8A73) } };
            guideBodyStyle = new GUIStyle(bodyStyle) { fontSize = 20, fontStyle = FontStyle.Bold };
            devStyle = new GUIStyle(bodyStyle) { normal = { textColor = new Color(1, 1, 1, .6f) } };
            islandLessonStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = false, normal = { textColor = UITheme.Muted } };
            islandStateStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
        }
        GUIStyle devStyle;
        // Sliced sticker art (borders as ui_sprites.py draws them) in a fill colour.
        void Sticker(Rect rect, Texture2D texture, Color fill)
        {
            if (!texture) { DrawRect(rect, fill); return; }
            sticker.normal.background = texture;
            sticker.border = texture == pill ? new RectOffset(27, 27, 27, 31) : new RectOffset(24, 24, 24, 30);
            var old = GUI.backgroundColor; GUI.backgroundColor = fill;
            GUI.Box(rect, GUIContent.none, sticker);
            GUI.backgroundColor = old;
        }

        void OnGUI()
        {
            // Absolute-position labels have no input/layout work; draw once per repaint.
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            // 디버 on a mint badge, talking from a paper card.
            Sticker(new Rect(18f, 18f, 660f, 112f), card, UITheme.Paper);
            float textX = 34f;
            if (guideFace)
            {
                if (badge) { var tint = GUI.color; GUI.color = UITheme.Mint; GUI.DrawTexture(new Rect(28f, 26f, 92f, 96f), badge, ScaleMode.ScaleToFit); GUI.color = tint; }
                GUI.DrawTexture(new Rect(32f, 30f, 84f, 84f), guideFace, ScaleMode.ScaleToFit); textX = 132f;
            }
            GUI.Label(new Rect(textX, 28f, 520f, 32f), "디버", guideTitleStyle);
            GUI.Label(new Rect(textX, 60f, 660f - textX - 16f, 56f), GuideMessage, guideBodyStyle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUI.Label(new Rect(18f, Screen.height - 38f, 420f, 28f), "DEV  Z: 임시 클리어    Q: 초기화", devStyle);
#endif
            DrawNodeNumbers();
            DrawEnterHint();
            DrawIslandNames();
            if (Time.unscaledTime < toastUntil)
            {
                var rect = new Rect((Screen.width - 460f) * 0.5f, Screen.height - 96f, 460f, 60f);
                Sticker(rect, card, UITheme.Coral);
                GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height - 6f), toast, toastStyle);
            }
        }

        // Each node's number under it, so players can tell the stages apart before clicking.
        void DrawNodeNumbers()
        {
            if (nodes == null || !mapCamera || entering) return;
            foreach (var node in nodes)
            {
                // Islands show their own STAGE number and title (DrawIslandNames).
                if (!node || node.GetComponent<CodingGame.Art.StageSelection.StageIslandView>()) continue;
                Vector3 p = mapCamera.WorldToScreenPoint(node.transform.position);
                if (p.z <= 0) continue;
                if (numberStyle == null) numberStyle = new GUIStyle(displayStyle) { alignment = TextAnchor.MiddleCenter };
                var rect = new Rect(p.x - 60, Screen.height - p.y + 30, 120, 40);
                Sticker(rect, pill, UITheme.Paper);
                GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height - 4), $"STAGE {node.Number}", numberStyle);
            }
        }

        // A bouncing arrow over the first stage that can be entered, with its name on a sunny pill, so the player knows where to click.
        GUIStyle arrowStyle, hintStyle, numberStyle;
        void DrawEnterHint()
        {
            var node = RecommendedStage;
            if (!node || !mapCamera || cameraController.IsMoving || entering) return;
            var bounds = node.VisualBounds;
            Vector3 top = mapCamera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y);
            if (top.z <= 0) return;
            if (arrowStyle == null) arrowStyle = new GUIStyle(displayStyle) { fontSize = 46, alignment = TextAnchor.MiddleCenter, normal = { textColor = UITheme.Sunny } };
            if (hintStyle == null) hintStyle = new GUIStyle(displayStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 16;
            const float hintLift = 12;
            string hint = $"{StageProgress.Title(node.Number)} · 여기야!";
            float width = hintStyle.CalcSize(new GUIContent(hint)).x + 48;
            GUI.Label(new Rect(top.x - 40, Screen.height - top.y - 80 - bounce - hintLift, 80, 60), "▼", arrowStyle);
            var plate = new Rect(top.x - width / 2, Screen.height - top.y - 126 - bounce - hintLift, width, 50);
            Sticker(plate, pill, UITheme.Primary);
            GUI.Label(new Rect(plate.x, plate.y, plate.width, plate.height - 5), hint, hintStyle);
        }

        // Each island's name, what it teaches and its state on a paper card, its number on a pill in the island's colour.
        void DrawIslandNames()
        {
            if (nodes == null || !mapCamera || entering || cameraController.IsMoving) return;
            islandTitleStyle ??= new GUIStyle(displayStyle) { alignment = TextAnchor.MiddleCenter };
            islandNumberStyle ??= new GUIStyle(displayStyle) { alignment = TextAnchor.MiddleCenter };
            // Sized for reading across the room on a 1080p screen.
            float scale = Screen.height / 1080f;
            islandTitleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(29 * scale), 16, 30);
            islandNumberStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(16 * scale), 11, 17);
            islandLessonStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(18 * scale), 11, 19);
            islandStateStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(17 * scale), 10, 18);
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var island = islands[i]; if (!island) continue;
                var p = mapCamera.WorldToScreenPoint(node.transform.position + new Vector3(0, -.8f, -3.1f)); if (p.z <= 0) continue;
                float y = Screen.height - p.y, width = Mathf.Min(360, Screen.width * .30f), left = p.x - width * .5f;
                bool locked = node.State == StageState.Locked, cleared = node.State == StageState.Cleared;
                var old = GUI.color;
                Sticker(new Rect(left, y - 22 * scale, width, 136 * scale), card, locked ? UITheme.PaperDeep : UITheme.Paper);
                var tag = new Rect(p.x - 58 * scale, y - 40 * scale, 116 * scale, 36 * scale);
                Sticker(tag, pill, locked ? UITheme.Muted : island.accent);
                GUI.Label(new Rect(tag.x, tag.y, tag.width, tag.height - 4 * scale), stageLabels[i], islandNumberStyle);
                GUI.color = locked ? new Color(1, 1, 1, .55f) : Color.white;
                GUI.Label(new Rect(left, y + 4 * scale, width, 40 * scale), island.stageTitle, islandTitleStyle);
                GUI.Label(new Rect(left, y + 44 * scale, width, 26 * scale), StageProgress.Lesson(node.Number), islandLessonStyle);
                islandStateStyle.normal.textColor = locked ? UITheme.Muted : cleared ? UITheme.Hex(0xC77700) : UITheme.Hex(0x0C8A73);
                GUI.Label(new Rect(left, y + 74 * scale, width, 26 * scale), stateLabels[i], islandStateStyle);
                GUI.color = old;
            }
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
