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
        EnemySpec stats; // the wave's numbers for this enemy, shown under its description
        bool hovered;
        void Awake()
        {
            if (!tooltip || !descriptionAnchor || (!robot && !enemy)) throw new InvalidOperationException("DefenseHoverInfo Inspector 참조를 연결하세요.");
        }
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = true;
            tooltip.Show(robot ? robot.displayName : enemy.displayName, robot ? robot.description : enemy.description + Stats, descriptionAnchor, rightOfAnchor);
        }
        public void OnPointerExit(PointerEventData data) => Clear();
        public void OnPointerDown(PointerEventData data) => Clear();
        void OnDisable() => Clear();
        // Light red and blue: the tooltip is dark teal (the theme's DangerHex and CodeHex were hard to read on it).
        string Stats => stats == null ? "" : $"\n<b><color=#FF8A80>체력 {stats.health:0}</color>   <color=#74C0FC>속도 {stats.speed:0.#}</color></b>";
        public void SetEnemy(EnemyDefinition definition, EnemySpec spec = null) { if (enemy != definition || stats != spec) { Clear(); enemy = definition; stats = spec; } }
        void Clear() { if (hovered) { hovered = false; tooltip.Hide(); } }
    }
}
