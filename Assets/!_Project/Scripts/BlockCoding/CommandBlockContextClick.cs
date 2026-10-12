using UnityEngine;
using UnityEngine.EventSystems;

namespace CodingGame.BlockCoding
{
    // Input fields/dropdowns handle pointer clicks themselves; explicitly forward their context click.
    public sealed class CommandBlockContextClick : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        [SerializeField] CommandBlockView block;
        public void OnPointerDown(PointerEventData e) => block.BringToFront();
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Right || block.IsPalette) return;
            block.Panel.DeleteOne(block); e.Use();
        }
    }
}
