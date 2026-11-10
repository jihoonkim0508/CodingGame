using System.Collections.Generic;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // Snapshot before UI/game input, commit after UI clicks have finished for this frame.
    [DefaultExecutionOrder(-200)]
    public sealed class TutorialDialogueInput : MonoBehaviour
    {
        TutorialDirector director;
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        Vector2 pressPosition;
        int pressRevision, queuedRevision, consumedFrame = -1;
        bool pressed, queued;
        public bool ConsumedThisFrame => consumedFrame == Time.frameCount;
        public void Initialize(TutorialDirector owner) { director = owner; }

        void Update()
        {
            queued = false;
            if(director && director.HasVisibleDialogue && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                consumedFrame=Time.frameCount;
            if (!director || !director.CanUseDialogueShortcut) { pressed = false; return; }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                pressPosition = mouse.position.ReadValue(); pressRevision = director.DialogueRevision;
                pressed = !PointerBlocked(pressPosition) && !EditingText();
                if (pressed) consumedFrame = Time.frameCount;
            }
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
            {
                var position = mouse.position.ReadValue();
                if (pressed && (position - pressPosition).sqrMagnitude <= 144 && !PointerBlocked(position))
                { queued = true; queuedRevision = pressRevision; consumedFrame = Time.frameCount; }
                pressed = false;
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !KeyboardBlocked())
            { queued = true; queuedRevision = director.DialogueRevision; consumedFrame = Time.frameCount; pressed = false; }
        }
        void LateUpdate()
        {
            if (queued && director && queuedRevision == director.DialogueRevision && director.CanUseDialogueShortcut)
                director.AdvanceDialogueShortcut();
            queued = false;
        }
        bool PointerBlocked(Vector2 position)
        {
            var system = EventSystem.current;
            if (!system) return false;
            hits.Clear(); system.RaycastAll(new PointerEventData(system) { position = position }, hits);
            if (hits.Count == 0) return false;
            var hit = hits[0].gameObject;
            return hit.GetComponentInParent<Selectable>() || hit.GetComponentInParent<ScrollRect>() ||
                hit.GetComponentInParent<CommandBlockView>() || hit.GetComponentInParent<CommandDropZone>() ||
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) || ExecuteEvents.GetEventHandler<IDragHandler>(hit);
        }
        static bool EditingText()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (!selected) return false;
            var tmp = selected.GetComponentInParent<TMP_InputField>();
            var legacy = selected.GetComponentInParent<InputField>();
            return tmp && tmp.isFocused || legacy && legacy.isFocused;
        }
        static bool KeyboardBlocked()
        {
            if (EditingText()) return true;
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if(!selected)return false;
            var dropdown=selected.GetComponentInParent<TMP_Dropdown>();
            if(dropdown&&dropdown.IsExpanded)return true;
            var module=EventSystem.current.currentInputModule as InputSystemUIInputModule;
            // Merely retaining button focus must not disable Space. Defer only when the UI
            // actually uses the same key as its submit action (including custom bindings).
            return selected.GetComponentInParent<Selectable>() &&
                (!module || module.submit && module.submit.action!=null && module.submit.action.triggered);
        }
        void OnDisable() { pressed = queued = false; }
    }
}
