using UnityEngine;
using UnityEngine.EventSystems;

namespace CodingGame.BlockCoding
{
    // Input fields/dropdowns handle pointer events themselves: forward the context click, and forward drags so a
    // placed block can still be picked up by its name or number field (otherwise the field eats the drag for text selection).
    public sealed class CommandBlockContextClick : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] CommandBlockView block;
        public void OnPointerDown(PointerEventData e) => block.BringToFront();
        public void OnPointerClick(PointerEventData e)
        {
            // In the palette the field is only a picture: a left click on it adds the block like a click anywhere else on it.
            if (block.IsPalette) { block.OnPointerClick(e); return; }
            if (e.button != PointerEventData.InputButton.Right) return;
            block.Panel.DeleteOne(block); e.Use();
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            EventSystem.current?.SetSelectedGameObject(null); // drop the caret the press gave the field
            block.OnBeginDrag(e);
        }
        public void OnDrag(PointerEventData e) { if (block.Panel.Dragged) block.OnDrag(e); }
        public void OnEndDrag(PointerEventData e) => block.OnEndDrag(e);
    }
}
