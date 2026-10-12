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
        string toast;
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
            cameraController.Focus(node.transform);
            GameFlow.LoadScene(GameFlow.DefenseScene);
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
            bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, normal = { textColor = new Color(0.25f, 0.28f, 0.34f) } };
            toastStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.12f, 0.14f, 0.18f) } };
            guideTitleStyle = new GUIStyle(bodyStyle) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.35f, 0.9f, 1f) } };
            guideBodyStyle = new GUIStyle(bodyStyle) { normal = { textColor = Color.white } };
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawRect(new Rect(18f, 18f, 510f, 76f), new Color(0.04f, 0.09f, 0.16f, 0.92f));
            GUI.Label(new Rect(34f, 25f, 480f, 30f), "도우미 로봇", guideTitleStyle);
            GUI.Label(new Rect(34f, 53f, 480f, 28f), "빛나는 1스테이지를 클릭해! 바로 작전지로 이동할게.", guideBodyStyle);
            GUI.Label(new Rect(18f, Screen.height - 38f, 420f, 28f), "DEV  Z: 클리어    Q: 초기화", bodyStyle);
            if (Time.unscaledTime < toastUntil)
                GUI.Box(new Rect((Screen.width - 430f) * 0.5f, Screen.height - 80f, 430f, 45f), toast, toastStyle);
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
