using UnityEngine;
using UnityEngine.EventSystems;

namespace CodingGame.Defense
{
    public sealed class DefenseRobotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] int definitionIndex;
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            data.eligibleForClick = false;
            battle.BeginRobotDrag(definitionIndex);
        }
        public void OnDrag(PointerEventData data) { }
        public void OnEndDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) battle.EndRobotDrag(data.position); }
    }
}
