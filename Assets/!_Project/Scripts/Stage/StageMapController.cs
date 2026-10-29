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
        GUIStyle islandTitleStyle,islandNumberStyle,islandStateStyle;
        string toast;
        bool entering; // a stage was chosen: the camera flies there and the scene fades, no more "click here"
        float toastUntil;

        public void Configure(StageNode[] stageNodes, Camera camera)
        {
            nodes = stageNodes;
            mapCamera = camera;
            cameraController = gameObject.AddComponent<StageCameraController>();
            cameraController.Configure(camera, stageNodes);
            RefreshNodes();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.wasPressedThisFrame) DeveloperClear();
                if (keyboard.qKey.wasPressedThisFrame) DeveloperReset();
            }

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
            StageProgress.MarkCleared(target.Number);
            ShowToast($"DEV: STAGE {target.Number} 클리어");
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
            foreach (StageNode node in nodes) node.Refresh();
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
            guideBodyStyle = new GUIStyle(bodyStyle) { normal = { textColor = Color.white } };
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawRect(new Rect(18f, 18f, 510f, 76f), new Color(0.04f, 0.09f, 0.16f, 0.92f));
            GUI.Label(new Rect(34f, 25f, 480f, 30f), "도우미 로봇", guideTitleStyle);
            var available = nodes?.FirstOrDefault(n => n && n.State == StageState.Available);
            GUI.Label(new Rect(34f, 53f, 480f, 28f), available?$"빛나는 {available.Number}스테이지 섬을 클릭해 줘!":"클리어한 섬을 골라 다시 도전할 수 있어.", guideBodyStyle);
            GUI.Label(new Rect(18f, Screen.height - 38f, 420f, 28f), "DEV  Z: 클리어    Q: 초기화", bodyStyle);
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
            var node = nodes?.FirstOrDefault(n => n && n.State == StageState.Available);
            if (!node || !mapCamera || cameraController.IsMoving || entering) return;
            var bounds = node.VisualBounds;
            Vector3 top = mapCamera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y);
            if (top.z <= 0) return;
            if (arrowStyle == null) arrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 46, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            if (hintStyle == null) hintStyle = new GUIStyle(guideBodyStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 16;
            var old = GUI.color;
            GUI.color = new Color(1, .85f, .25f, 1);
            GUI.Label(new Rect(top.x - 40, Screen.height - top.y - 80 - bounce, 80, 60), "▼", arrowStyle);
            GUI.Label(new Rect(top.x - 160, Screen.height - top.y - 118 - bounce, 320, 34), $"{node.Number}스테이지 · 여기를 클릭!", hintStyle);
            GUI.color = old;
        }

        void DrawIslandNames()
        {
            if(nodes==null||!mapCamera||entering||cameraController.IsMoving)return;
            islandTitleStyle??=new GUIStyle(guideBodyStyle){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            islandNumberStyle??=new GUIStyle(islandTitleStyle){fontSize=12};
            islandStateStyle??=new GUIStyle(islandTitleStyle){fontSize=12,fontStyle=FontStyle.Normal};
            islandTitleStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.021f),14,22);
            islandNumberStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.014f),10,14);
            islandStateStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.012f),9,12);
            foreach(var node in nodes)
            {
                var island=node.GetComponent<CodingGame.Art.StageSelection.StageIslandView>();if(!island)continue;
                var p=mapCamera.WorldToScreenPoint(node.transform.position+new Vector3(0,-.8f,-3.1f));if(p.z<=0)continue;
                float y=Screen.height-p.y;var old=GUI.color;float width=Mathf.Min(340,Screen.width*.30f);
                GUI.color=island.accent;GUI.Label(new Rect(p.x-width*.5f,y-9,width,22),$"STAGE {node.Number:00}",islandNumberStyle);
                GUI.color=Color.white;GUI.Label(new Rect(p.x-width*.5f,y+13,width,30),island.stageTitle,islandTitleStyle);
                GUI.color=node.State==StageState.Locked?new Color(.6f,.72f,.83f):island.accent;
                GUI.Label(new Rect(p.x-width*.5f,y+43,width,22),node.State==StageState.Locked?"이전 스테이지 클리어 후 해금":node.State==StageState.Cleared?"CLEARED · 다시 플레이":"READY · 클릭하여 시작",islandStateStyle);
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
