using System;
using TMPro;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed class DefenseTooltip : MonoBehaviour
    {
        [SerializeField] RectTransform canvasRect, panelRect;
        [SerializeField] TMP_Text title, description;
        void Awake()
        {
            if (!canvasRect || !panelRect || !title || !description)
                throw new InvalidOperationException("DefenseTooltip Inspector 참조를 연결하세요.");
            Hide();
        }
        public void Show(string name, string detail, RectTransform anchor, bool rightOfAnchor)
        {
            title.text = name; description.text = detail;
            panelRect.gameObject.SetActive(true);
            var edge = anchor.TransformPoint(new Vector3(rightOfAnchor ? anchor.rect.xMax : anchor.rect.center.x, anchor.rect.yMax, 0));
            Vector2 point = canvasRect.InverseTransformPoint(edge);
            var bounds = canvasRect.rect;
            point += rightOfAnchor ? new Vector2(12, -panelRect.rect.height) : new Vector2(-panelRect.rect.width * .5f, 12);
            point.x = Mathf.Clamp(point.x, bounds.xMin + 12, bounds.xMax - panelRect.rect.width - 12);
            point.y = Mathf.Clamp(point.y, bounds.yMin + 12, bounds.yMax - panelRect.rect.height - 12);
            panelRect.anchoredPosition = point;
        }
        public void Hide() => panelRect.gameObject.SetActive(false);
    }
}
