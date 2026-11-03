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
        string guideMessage = "클리어한 섬을 골라 다시 도전할 수 있어.";
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
            if (node.State == StageState.Locked) { ShowToast($"STAGE {node.Number} - LOCKED"); return; }
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
            guideMessage = recommendedStage ? $"빛나는 {recommendedStage.Number}스테이지 섬을 클릭해 줘!" :
                nodes.Any(node => StageProgress.BestStars(node.Number) < 3) ? "클리어한 섬에 다시 도전해 봐. 코어를 더 지키면 별이 늘어나!" : "모든 섬 ★3! 다른 코드로도 막아 볼래?";
        }

        void ShowToast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 2.5f;
        }

        void EnsureStyles()
        {
            if (bodyStyle != null) return;
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 28);
            bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, normal = { textColor = new Color(.68f,.76f,.84f) } };
            toastStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.12f, 0.14f, 0.18f) } };
            guideTitleStyle = new GUIStyle(bodyStyle) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.35f, 0.9f, 1f) } };
            guideTitleStyle.fontSize = 22;
            guideBodyStyle = new GUIStyle(bodyStyle) { fontSize = 20, normal = { textColor = Color.white } };
        }

        void OnGUI()
        {
            // Absolute-position labels have no input/layout work; draw once per repaint.
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            DrawRect(new Rect(18f, 18f, 640f, 100f), new Color(0.04f, 0.09f, 0.16f, 0.92f));
            float textX = 34f;
            if (guideFace) { GUI.DrawTexture(new Rect(26f, 26f, 84f, 84f), guideFace, ScaleMode.ScaleToFit); textX = 120f; }
            GUI.Label(new Rect(textX, 28f, 520f, 32f), "디버", guideTitleStyle);
            GUI.Label(new Rect(textX, 62f, 640f - textX, 48f), GuideMessage, guideBodyStyle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUI.Label(new Rect(18f, Screen.height - 38f, 420f, 28f), "DEV  Z: 임시 클리어    Q: 초기화", bodyStyle);
#endif
            DrawNodeNumbers();
            DrawEnterHint();
            DrawIslandNames();
            if (Time.unscaledTime < toastUntil)
                GUI.Box(new Rect((Screen.width - 430f) * 0.5f, Screen.height - 80f, 430f, 45f), toast, toastStyle);
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
                if (numberStyle == null) numberStyle = new GUIStyle(guideBodyStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
                var rect = new Rect(p.x - 50, Screen.height - p.y + 30, 100, 28);
                DrawRect(rect, new Color(0.04f, 0.09f, 0.16f, 0.85f));
                GUI.Label(rect, $"STAGE {node.Number}", numberStyle);
            }
        }

        // A bouncing arrow over the first stage that can be entered, so the player knows where to click.
        GUIStyle arrowStyle, hintStyle, numberStyle;
        void DrawEnterHint()
        {
            var node = RecommendedStage;
            if (!node || !mapCamera || cameraController.IsMoving || entering) return;
            var bounds = node.VisualBounds;
            Vector3 top = mapCamera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y);
            if (top.z <= 0) return;
            if (arrowStyle == null) arrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 46, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            if (hintStyle == null) hintStyle = new GUIStyle(guideBodyStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 16;
            var old = GUI.color;
            GUI.color = new Color(1, .85f, .25f, 1);
            const float hintLift = 12;
            GUI.Label(new Rect(top.x - 40, Screen.height - top.y - 80 - bounce - hintLift, 80, 60), "▼", arrowStyle);
            GUI.Label(new Rect(top.x - 160, Screen.height - top.y - 118 - bounce - hintLift, 320, 34), $"{node.Number}스테이지 · 여기를 클릭!", hintStyle);
            GUI.color = old;
        }

        void DrawIslandNames()
        {
            if(nodes==null||!mapCamera||entering||cameraController.IsMoving)return;
            islandTitleStyle??=new GUIStyle(guideBodyStyle){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            islandNumberStyle??=new GUIStyle(islandTitleStyle){fontSize=12};
            islandStateStyle??=new GUIStyle(islandTitleStyle){fontSize=12,fontStyle=FontStyle.Normal};
            islandLessonStyle??=new GUIStyle(islandTitleStyle){fontStyle=FontStyle.Normal};
            // Sized for reading across the room on a 1080p screen: name, what it teaches, then its state, on a dark plate.
            float scale=Screen.height/1080f;
            islandTitleStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(29*scale),16,30);
            islandNumberStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(17*scale),11,18);
            islandLessonStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(18*scale),11,19);
            islandStateStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(17*scale),10,18);
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var island=islands[i];if(!island)continue;
                var p=mapCamera.WorldToScreenPoint(node.transform.position+new Vector3(0,-.8f,-3.1f));if(p.z<=0)continue;
                float y=Screen.height-p.y;var old=GUI.color;float width=Mathf.Min(360,Screen.width*.30f),left=p.x-width*.5f;
                bool locked=node.State==StageState.Locked;
                DrawRect(new Rect(left,y-18*scale,width,124*scale),new Color(.03f,.07f,.12f,.72f));
                GUI.color=island.accent;GUI.Label(new Rect(left,y-14*scale,width,26*scale),stageLabels[i],islandNumberStyle);
                GUI.color=locked?new Color(.75f,.82f,.9f):Color.white;GUI.Label(new Rect(left,y+10*scale,width,38*scale),island.stageTitle,islandTitleStyle);
                GUI.color=new Color(.81f,.91f,1f,locked?.7f:1);GUI.Label(new Rect(left,y+48*scale,width,26*scale),StageProgress.Lesson(node.Number),islandLessonStyle);
                GUI.color=locked?new Color(.6f,.72f,.83f):island.accent;
                GUI.Label(new Rect(left,y+76*scale,width,26*scale),stateLabels[i],islandStateStyle);
                GUI.color=old;
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
