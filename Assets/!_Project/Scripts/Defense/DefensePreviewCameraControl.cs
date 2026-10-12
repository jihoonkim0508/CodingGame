using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CodingGame.Defense
{
    public sealed class DefensePreviewCameraControl : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] Camera previewCamera;
        [SerializeField] RectTransform viewport;
        Vector3 homePosition;
        float homeSize;
        bool initialized;
        void Initialize()
        {
            if (initialized) return;
            if (!previewCamera || !viewport || !previewCamera.orthographic) throw new InvalidOperationException("미리보기 직교 카메라·영역 참조를 연결하세요.");
            homePosition = previewCamera.transform.position;
            homeSize = previewCamera.orthographicSize;
            initialized = true;
        }
        public void ResetView()
        {
            Initialize(); previewCamera.transform.position = homePosition;
            previewCamera.orthographicSize = homeSize;
        }
        public void OnBeginDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) data.eligibleForClick = false; }
        public void OnDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            Initialize();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out var current) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position - data.delta, data.pressEventCamera, out var previous)) return;
            float height = previewCamera.orthographicSize * 2;
            var delta = (current - previous) * (height / Mathf.Max(1, viewport.rect.height));
            var move = previewCamera.transform.right * delta.x + previewCamera.transform.up * delta.y;
            previewCamera.transform.position = homePosition + Vector3.ClampMagnitude(previewCamera.transform.position - move - homePosition, 20);
        }
        public void OnScroll(PointerEventData data)
        {
            Initialize();
            float scale = Mathf.Pow(.85f, data.scrollDelta.y * .5f);
            previewCamera.orthographicSize = Mathf.Clamp(previewCamera.orthographicSize * scale, 2, 16);
        }
    }
}
