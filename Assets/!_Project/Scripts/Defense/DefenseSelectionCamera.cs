using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CodingGame.Defense
{
    public sealed class DefenseSelectionCamera : MonoBehaviour
    {
        [SerializeField] Camera battleCamera;
        [SerializeField] float transitionSeconds = .3f;
        Vector3 homePosition, fromPosition, destination;
        Quaternion homeRotation, fromRotation, rotation;
        Rect homeRect, fromRect, viewport;
        float homeFov, fromFov, fov, elapsed;
        float activeSeconds;
        bool initialized, focused;
        void Initialize()
        {
            if (initialized) return;
            if (!battleCamera) throw new InvalidOperationException("선택 카메라 참조를 연결하세요.");
            homePosition = battleCamera.transform.position; homeRotation = battleCamera.transform.rotation;
            homeRect = battleCamera.rect; homeFov = battleCamera.fieldOfView; initialized = true;
            destination = homePosition; rotation = homeRotation; viewport = homeRect; fov = homeFov; elapsed = transitionSeconds;
            activeSeconds = transitionSeconds;
        }
        // The battle's own view changes with the map (Stage 1's short map is framed closer): the camera goes there at once,
        // and the player's pan and zoom start over from it.
        public void SetHome(Vector3 position, Quaternion orientation)
        {
            Initialize();
            homePosition = destination = position; homeRotation = rotation = orientation; elapsed = activeSeconds; focused = false;
            pan = panTarget = panVelocity = Vector3.zero; dolly = dollyTarget = dollyVelocity = 0;
            battleCamera.transform.SetPositionAndRotation(position, orientation);
        }
        // Back to the player's view (the home view moved by their pan and zoom).
        public void Restore()
        {
            if (!initialized) return;
            focused = false;
            Begin(transitionSeconds); destination = UserPosition; rotation = homeRotation; viewport = homeRect; fov = homeFov;
        }
        public void FocusBoss(Vector3 target)
        {
            Initialize(); Begin(.75f); focused = true;
            destination = target - homeRotation * Vector3.forward * 13f;
            rotation = Quaternion.LookRotation(target - destination, Vector3.up);
            viewport = homeRect;
            fov = Mathf.Min(homeFov, 48f);
        }
        void Begin(float seconds)
        {
            fromPosition = battleCamera.transform.position; fromRotation = battleCamera.transform.rotation;
            fromRect = battleCamera.rect; fromFov = battleCamera.fieldOfView; elapsed = 0; activeSeconds = seconds;
        }
        void LateUpdate()
        {
            Initialize();
            if (elapsed >= activeSeconds) { if (!focused) Steer(); return; }
            elapsed += GameClock.Delta; // stands still behind the Esc menu, like the entrance it serves
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, activeSeconds)));
            battleCamera.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, destination, t), Quaternion.Slerp(fromRotation, rotation, t));
            battleCamera.rect = new Rect(Mathf.Lerp(fromRect.x, viewport.x, t), Mathf.Lerp(fromRect.y, viewport.y, t),
                Mathf.Lerp(fromRect.width, viewport.width, t), Mathf.Lerp(fromRect.height, viewport.height, t));
            battleCamera.fieldOfView = Mathf.Lerp(fromFov, fov, t);
        }

        // The player's camera (2026-11-25): the mouse wheel dollies toward the cursor, WASD, the arrows and the screen's edges
        // pan over the ground. Targets move at once; the camera follows them a hair behind (SmoothDamp .1 s) on unscaled time,
        // so pause and 2x/3x do not change it. Zoomed all the way out it is the authored view, which frames the whole map; the
        // centre of the view may wander as far over the map as the zoom shows less of it.
        // Gate: whether the keys and the pointer may move the camera this frame (DefenseBattle.CameraInput: the code panel,
        // text fields, menus, the lesson's locks and the pointer over UI).
        public Func<(bool keys, bool wheel, bool edge)> Gate;
        // Recordings (ScreenTour) lock out the real mouse and keyboard, whose pointer may rest on the Game view's edge.
        public static bool InputLocked;
        public const float MinDistance = .42f, MaxDistance = 1f; // of the home view's distance to the ground
        const float SmoothSeconds = .1f, EdgePixels = 16, WheelStep = .88f, PanSpeed = .85f;
        Vector3 pan, panTarget, panVelocity;
        float dolly, dollyTarget, dollyVelocity;
        Bounds area = new Bounds(Vector3.zero, Vector3.one * 30);
        public void SetArea(Bounds ground) => area = ground;
        // The home view's distance to the ground along its view (where its centre ray meets y = 0).
        float HomeDistance => Mathf.Max(1, homePosition.y / Mathf.Max(.05f, -(homeRotation * Vector3.forward).y));
        Vector3 UserPosition => homePosition + pan + homeRotation * Vector3.forward * dolly;
        public float Zoom => (HomeDistance - dolly) / HomeDistance; // 1 = home, MinDistance = closest
        public bool Panned => pan.sqrMagnitude > .01f || dolly > .05f;
        void Steer()
        {
            var (keys, wheelOn, edge) = Gate != null && !InputLocked ? Gate() : (!InputLocked, !InputLocked, !InputLocked);
            float dt = Time.unscaledDeltaTime;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            Vector2 move = Vector2.zero;
            if (keys && keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1;
            }
            if (mouse != null)
            {
                var at = mouse.position.ReadValue();
                if (at.x >= 0 && at.y >= 0 && at.x <= Screen.width && at.y <= Screen.height)
                {
                    if (edge && Application.isFocused) // a pointer left on the edge while the game is in the background stays put
                    {
                        if (at.x <= EdgePixels) move.x -= 1; else if (at.x >= Screen.width - EdgePixels) move.x += 1;
                        if (at.y <= EdgePixels) move.y -= 1; else if (at.y >= Screen.height - EdgePixels) move.y += 1;
                    }
                    float wheel = mouse.scroll.ReadValue().y;
                    if (wheelOn && Mathf.Abs(wheel) > .01f) ZoomAt(at, wheel > 0 ? WheelStep : 1 / WheelStep); // a notch a step, whatever its units
                }
            }
            if (move != Vector2.zero)
            {
                var forward = homeRotation * Vector3.forward; forward.y = 0; forward.Normalize();
                var right = homeRotation * Vector3.right; right.y = 0; right.Normalize();
                panTarget += (right * move.x + forward * move.y).normalized * (PanSpeed * (HomeDistance - dollyTarget) * dt);
            }
            panTarget = ClampPan(panTarget, dollyTarget);
            pan = Vector3.SmoothDamp(pan, panTarget, ref panVelocity, SmoothSeconds, Mathf.Infinity, dt);
            dolly = Mathf.SmoothDamp(dolly, dollyTarget, ref dollyVelocity, SmoothSeconds, Mathf.Infinity, dt);
            var position = UserPosition;
            if ((battleCamera.transform.position - position).sqrMagnitude > 1e-8f || battleCamera.transform.rotation != homeRotation)
                battleCamera.transform.SetPositionAndRotation(position, homeRotation);
        }
        // Zoom by factor (of the distance to the ground) keeping the ground under the screen point where it is.
        public void ZoomAt(Vector2 screen, float factor)
        {
            float distance = HomeDistance;
            float next = Mathf.Clamp(distance - dollyTarget, distance * MinDistance, distance * MaxDistance);
            next = Mathf.Clamp(next * factor, distance * MinDistance, distance * MaxDistance);
            float newDolly = distance - next;
            // The ray through the point keeps its direction whatever the camera's position (rotation and lens stay).
            var direction = battleCamera.ScreenPointToRay(screen).direction;
            if (direction.y > -.01f) { dollyTarget = newDolly; return; }
            Vector3 Ground(Vector3 from) => from + direction * (-from.y / direction.y);
            var before = Ground(homePosition + panTarget + homeRotation * Vector3.forward * dollyTarget);
            var after = Ground(homePosition + panTarget + homeRotation * Vector3.forward * newDolly);
            dollyTarget = newDolly;
            var shift = before - after; shift.y = 0;
            panTarget = ClampPan(panTarget + shift, dollyTarget);
        }
        public void Pan(Vector3 worldShift) { panTarget = ClampPan(panTarget + new Vector3(worldShift.x, 0, worldShift.z), dollyTarget); }
        // The view's centre (where the home view's centre ray meets the ground, moved by the pan) stays over the map: at home it
        // barely moves (4% of the map), and the closer the zoom, the further it may go, up to the map's edges.
        Vector3 ClampPan(Vector3 value, float dollyAt)
        {
            float closer = Mathf.InverseLerp(MaxDistance, MinDistance, (HomeDistance - dollyAt) / HomeDistance);
            var half = area.extents * Mathf.Lerp(.04f, 1f, closer);
            var centre = homePosition + homeRotation * Vector3.forward * HomeDistance;
            float Axis(float c, float v, float h, float min, float max)
            {
                float lo = Mathf.Max(min, c - h), hi = Mathf.Min(max, c + h);
                return lo > hi ? 0 : Mathf.Clamp(c + v, lo, hi) - c;
            }
            return new Vector3(Axis(centre.x, value.x, half.x, area.min.x, area.max.x), 0, Axis(centre.z, value.z, half.z, area.min.z, area.max.z));
        }
        // UI the pointer is over that takes the pointer itself (buttons, fields, scroll views, the code area): the wheel and
        // the edge pan leave it alone. Plain HUD plates (the top bar along the screen's edge) do not count for the edge.
        static readonly System.Collections.Generic.List<RaycastResult> hits = new System.Collections.Generic.List<RaycastResult>();
        public static bool PointerOnInteractiveUI(Vector2 screen)
        {
            var events = EventSystem.current; if (!events) return false;
            hits.Clear(); events.RaycastAll(new PointerEventData(events) { position = screen }, hits);
            foreach (var hit in hits)
                if (hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() || hit.gameObject.GetComponentInParent<UnityEngine.UI.ScrollRect>() ||
                    hit.gameObject.GetComponentInParent<IScrollHandler>() != null || hit.gameObject.GetComponentInParent<IDragHandler>() != null) return true;
            return false;
        }
    }
}
