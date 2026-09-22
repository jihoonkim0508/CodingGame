using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CodingGame.Defense
{
    public sealed class DefenseHoverInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField] DefenseTooltip tooltip;
        [SerializeField] RobotDefinition robot;
        [SerializeField] EnemyDefinition enemy;
        [SerializeField] RectTransform descriptionAnchor;
        [SerializeField] bool rightOfAnchor;
        bool hovered;
        void Awake()
        {
            if (!tooltip || !descriptionAnchor || (!robot && !enemy)) throw new InvalidOperationException("DefenseHoverInfo Inspector 참조를 연결하세요.");
        }
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = true;
            tooltip.Show(robot ? robot.displayName : enemy.displayName, robot ? robot.description : enemy.description, descriptionAnchor, rightOfAnchor);
        }
        public void OnPointerExit(PointerEventData data) => Clear();
        public void OnPointerDown(PointerEventData data) => Clear();
        void OnDisable() => Clear();
        void Clear() { if (hovered) { hovered = false; tooltip.Hide(); } }
    }
}
