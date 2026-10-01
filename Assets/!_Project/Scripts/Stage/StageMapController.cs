using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageMapController : MonoBehaviour
    {
        [SerializeField] StageDefinition[] stages;
        [SerializeField] StageNode[] nodes;
        [SerializeField] Camera mapCamera;
        StageCameraController cameraController;
        StageNode selected;
        GUIStyle titleStyle, bodyStyle, buttonStyle, badgeStyle;
        Font koreanFont;
        Rect panelRect;
        string toast;
        float toastUntil;

        public void Configure(StageDefinition[] definitions, StageNode[] stageNodes, Camera camera)
        {
            stages = definitions;
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
                if (keyboard.escapeKey.wasPressedThisFrame) CancelSelection();
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || cameraController.IsMoving) return;
            Vector2 point = mouse.position.ReadValue();
            point.y = Screen.height - point.y;
            if (selected != null && panelRect.Contains(point)) return;

            Ray ray = mapCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                StageNode node = hit.collider.GetComponentInParent<StageNode>();
                if (node != null) Select(node);
                else if (selected != null) CancelSelection();
            }
            else if (selected != null) CancelSelection();
        }

        void Select(StageNode node)
        {
            if (node.State == StageState.Locked)
            {
                ShowToast($"STAGE {node.Definition.Number} - LOCKED");
                return;
            }
            selected = node;
            RefreshNodes();
            cameraController.Focus(node.transform);
        }

        void CancelSelection()
        {
            if (selected == null) return;
            selected = null;
            RefreshNodes();
            cameraController.Restore();
        }

        void EnterSelected()
        {
            if (selected == null) return;
            Debug.Log($"[StageMap] 진입 선택 - STAGE {selected.Definition.Number}: {selected.Definition.Title} ({selected.Definition.SceneName})");
            ShowToast($"STAGE {selected.Definition.Number} 진입 선택 로그를 기록했습니다.");
        }

        void DeveloperClear()
        {
            StageNode target = selected;
            if (target == null || target.State == StageState.Locked)
                target = nodes.FirstOrDefault(node => node.State == StageState.Available);
            if (target == null) { ShowToast("모든 스테이지가 클리어되었습니다."); return; }
            StageProgress.MarkCleared(target.Definition.Id);
            ShowToast($"DEV: STAGE {target.Definition.Number} 클리어");
            RefreshNodes();
        }

        void DeveloperReset()
        {
            StageProgress.ResetAll(stages);
            CancelSelection();
            RefreshNodes();
            ShowToast("DEV: 진행 상태 초기화");
        }

        void RefreshNodes()
        {
            for (int i = 0; i < nodes.Length; i++)
                nodes[i].SetState(StageProgress.GetState(stages, i), nodes[i] == selected);
        }

        void ShowToast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 2.5f;
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 28);
            titleStyle = new GUIStyle(GUI.skin.label) { font = koreanFont, fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.08f, 0.1f, 0.14f) } };
            bodyStyle = new GUIStyle(GUI.skin.label) { font = koreanFont, fontSize = 18, wordWrap = true, normal = { textColor = new Color(0.25f, 0.28f, 0.34f) } };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = koreanFont,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.12f, 0.14f, 0.18f) },
                hover = { textColor = new Color(0.05f, 0.32f, 0.62f) }
            };
            badgeStyle = new GUIStyle(titleStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            EnsureStyles();
            if (selected != null && !cameraController.IsMoving)
            {
                float width = Mathf.Min(650f, Screen.width - 32f);
                Vector3 screenPoint = mapCamera.WorldToScreenPoint(selected.transform.position + Vector3.up * 1.05f);
                float x = Mathf.Clamp(screenPoint.x - width * 0.5f, 16f, Screen.width - width - 16f);
                float y = Mathf.Clamp(Screen.height - screenPoint.y - 190f, 18f, Screen.height - 205f);
                panelRect = new Rect(x, y, width, 168f);
                float anchorX = Mathf.Clamp(screenPoint.x, panelRect.x + 24f, panelRect.xMax - 24f);
                float anchorY = Screen.height - screenPoint.y;
                if (anchorY > panelRect.yMax)
                    DrawRect(new Rect(anchorX - 1f, panelRect.yMax, 2f, anchorY - panelRect.yMax), new Color(0.72f, 0.74f, 0.78f, 0.9f));
                DrawRect(new Rect(panelRect.x - 1f, panelRect.y - 1f, panelRect.width + 2f, panelRect.height + 2f), new Color(0.76f, 0.78f, 0.82f, 0.98f));
                DrawRect(panelRect, new Color(1f, 1f, 1f, 0.98f));
                DrawRect(new Rect(panelRect.x, panelRect.y, panelRect.width, 3f), new Color(0.2f, 0.55f, 0.85f, 0.9f));

                Rect preview = new Rect(panelRect.x + 16f, panelRect.y + 16f, 145f, 96f);
                if (selected.Definition.Preview) GUI.DrawTexture(preview, selected.Definition.Preview, ScaleMode.ScaleAndCrop);
                else
                {
                    Color old = GUI.color;
                    GUI.color = selected.Definition.ClearedColor;
                    GUI.Box(preview, $"STAGE {selected.Definition.Number}", badgeStyle);
                    GUI.color = old;
                }

                GUI.Label(new Rect(preview.xMax + 18f, panelRect.y + 13f, width - 195f, 40f), $"STAGE {selected.Definition.Number}  {selected.Definition.Title}", titleStyle);
                GUI.Label(new Rect(preview.xMax + 18f, panelRect.y + 55f, width - 195f, 58f), selected.Definition.Description, bodyStyle);
                float buttonY = panelRect.yMax - 45f;
                if (GUI.Button(new Rect(panelRect.xMax - 264f, buttonY, 116f, 34f), "진입하기", buttonStyle)) EnterSelected();
                if (GUI.Button(new Rect(panelRect.xMax - 138f, buttonY, 116f, 34f), "취소하기", buttonStyle)) CancelSelection();
            }

            GUI.Label(new Rect(18f, Screen.height - 38f, 420f, 28f), "DEV  Z: 클리어    Q: 초기화    ESC: 취소", bodyStyle);
            if (Time.unscaledTime < toastUntil)
                GUI.Box(new Rect((Screen.width - 430f) * 0.5f, Screen.height - 80f, 430f, 45f), toast, buttonStyle);
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
