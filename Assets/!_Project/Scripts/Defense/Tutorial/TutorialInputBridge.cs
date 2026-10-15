using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    [Serializable]
    public sealed class TutorialTargetBinding
    {
        public string key;
        public RectTransform uiTarget;
        public Collider worldTarget;
        public bool allowRuntimeTarget;
    }

    public sealed class TutorialInputBridge : MonoBehaviour
    {
        sealed class RuntimeTarget
        {
            public GameObject owner;
            // Several parts (e.g. each road segment) cut several holes; round parts are ground circles (attack range);
            // box parts are drawn as the one screen box around them (the floor cells).
            public Bounds[] parts;
            public bool round, box;
        }

        [SerializeField] Canvas canvas;
        [SerializeField] Camera worldCamera;
        [SerializeField] TutorialSpotlight spotlight;
        [SerializeField] TutorialTargetBinding[] targets = Array.Empty<TutorialTargetBinding>();
        readonly List<Rect> screenAreas = new List<Rect>();
        readonly List<Rect> visibleAreas = new List<Rect>();
        readonly List<Rect> roundAreas = new List<Rect>();
        // The road: lit boxes and the dim inside them off the road (see AddRoad).
        readonly List<Rect> roadBoxes = new List<Rect>();
        readonly List<Vector2[]> roadDim = new List<Vector2[]>();
        const float RoadCorner = .6f; // world units
        readonly List<Rect> outlinedAreas = new List<Rect>();
        readonly Dictionary<string, TutorialTargetBinding> byKey = new Dictionary<string, TutorialTargetBinding>();
        readonly Dictionary<string, RuntimeTarget> runtimeTargets = new Dictionary<string, RuntimeTarget>();
        readonly Vector3[] corners = new Vector3[4];
        TutorialDirector director;
        TutorialStep currentStep;
        bool allowActions;
        string[] focus = Array.Empty<string>();
        // World corners of an Editor.Code.* / Editor.Palette.* key; the director points this at DefenseCodeEditor.
        Func<string, Vector3[], bool> editorArea;
        public event Action<TutorialStep> Completed;
        public bool IsGuiding => director && director.IsActive;
        public bool PauseBattle => IsGuiding && currentStep != null && currentStep.pauseBattle;
        public bool AllowPreview => IsGuiding && currentStep != null && currentStep.allowPreview;
        Camera CanvasCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        bool Acting => allowActions && currentStep != null && currentStep.kind == TutorialStepKind.ActionGuide;
        public void SetInputLimited(bool value) { if (spotlight) spotlight.raycastTarget = currentStep != null && currentStep.blockInput && value; }

        public void Initialize(TutorialDirector owner)
        {
            director = owner;
            if (!canvas || !spotlight) throw new InvalidOperationException(name + ": Canvas/Spotlight 참조 누락");
            byKey.Clear(); runtimeTargets.Clear();
            foreach (var target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.key) ||
                    (!target.allowRuntimeTarget && !target.uiTarget && !target.worldTarget) ||
                    target.uiTarget && target.worldTarget || byKey.ContainsKey(target.key))
                    throw new InvalidOperationException(name + ": TutorialTargetBinding key/참조가 잘못됐습니다.");
                byKey.Add(target.key, target);
            }
            if (!worldCamera) throw new InvalidOperationException(name + ": 월드 대상용 Camera 참조가 누락됐습니다.");
            SetStep(null);
        }

        public void SetStep(TutorialStep step, bool enableActions = false)
        {
            // Only new targets animate; the next line keeping the same targets (and focus, see SetFocus) just stays.
            if (spotlight && step != null && (currentStep == null || !step.targetKeys.SequenceEqual(currentStep.targetKeys))) spotlight.Pulse();
            currentStep = step;
            allowActions = enableActions;
            if (step == null) focus = Array.Empty<string>();
            if (!spotlight) return;
            spotlight.enabled = step != null;
            spotlight.raycastTarget = step != null && step.blockInput;
            RefreshAreas();
        }

        public void SetEditorAreas(Func<string, Vector3[], bool> resolver) => editorArea = resolver;
        // Outlined, not clickable: the dialogue's focus targets. Clicks still pass the game's TutorialAllows checks.
        public void SetFocus(string[] keys)
        {
            keys = keys ?? Array.Empty<string>();
            if (spotlight && !focus.SequenceEqual(keys)) spotlight.Pulse();
            focus = keys; RefreshAreas();
        }

        void LateUpdate() => RefreshIfActing();
        void RefreshIfActing() { if (currentStep != null && (allowActions || focus.Length > 0)) RefreshAreas(); }

        void RefreshAreas()
        {
            if (!spotlight) return;
            screenAreas.Clear(); outlinedAreas.Clear(); visibleAreas.Clear(); roundAreas.Clear(); roadBoxes.Clear(); roadDim.Clear();
            if (allowActions && currentStep != null)
                foreach (var key in currentStep.targetKeys)
                {
                    bool outlined = currentStep.outline;
                    int start = screenAreas.Count;
                    // The placement area is the whole map: clickable (AddArea adds it to the clicks), but drawing it would hide the focus.
                    if (AddRoad(key) || AddArea(key) == 0 || key == TutorialKeys.Deployment) continue;
                    // With a focus on screen the focus carries the box; the action targets only stay lit.
                    Show(start, outlined && focus.Length == 0);
                }
            if (currentStep != null)
                foreach (var key in focus)
                    // "a|b": the first one on screen (e.g. the empty slot once the block is placed, else the place to drop it).
                    foreach (var option in key.Split('|'))
                    {
                        int start = screenAreas.Count;
                        if (AddRoad(option.Trim())) break;
                        if (AddArea(option.Trim()) > 0) { Show(start, true); break; }
                    }
            // Focus uses the same spotlight as action steps: dim everything but the holes.
            bool dim = currentStep == null || !currentStep.undimmed && (currentStep.dim || focus.Length > 0 || Acting);
            spotlight.SetRoad(roadBoxes, roadDim);
            spotlight.SetHoles(screenAreas, visibleAreas, outlinedAreas, roundAreas, currentStep?.highlightPadding ?? 0, dim);
        }

        // Areas added since start become visible holes; a single rect gets the outline.
        void Show(int start, bool outlined)
        {
            for (int i = start; i < screenAreas.Count; i++) visibleAreas.Add(screenAreas[i]);
            if (outlined && screenAreas.Count - start == 1) outlinedAreas.Add(screenAreas[start]);
        }

        // A multi-part ground target (the road) is shaped on the ground and then projected, so its edges follow the
        // perspective instead of stepping like screen rects: its screen box is lit, and the ground off the road inside
        // that box is dimmed again, with the road's outer corners rounded. False when key is not such a target.
        bool AddRoad(string key)
        {
            if (!TryGetParts(key, out var parts, out bool round) || round || parts.Length < 2 || IsBox(key)) return false;
            float y = parts[0].center.y;
            var box = Rect.zero;
            for (int i = 0; i < parts.Length * 4; i++)
            {
                var b = parts[i / 4];
                if (!ToLocal(new Vector3(i % 2 == 0 ? b.min.x : b.max.x, y, i % 4 < 2 ? b.min.z : b.max.z), out var p)) return false;
                box = i == 0 ? new Rect(p, Vector2.zero) : Rect.MinMaxRect(Mathf.Min(box.xMin, p.x), Mathf.Min(box.yMin, p.y), Mathf.Max(box.xMax, p.x), Mathf.Max(box.yMax, p.y));
            }
            // The ground under the box, as a convex quad on the XZ plane.
            var ground = TutorialSpotlight.Corners(box);
            for (int i = 0; i < 4; i++) if (!ToGround(ground[i], y, out var g)) return false; else ground[i] = g;
            void Dim(List<Vector2> shape)
            {
                shape = TutorialSpotlight.ClipInside(shape, ground);
                if (shape.Count < 3) return;
                var local = new Vector2[shape.Count];
                for (int i = 0; i < shape.Count; i++) if (!ToLocal(new Vector3(shape[i].x, y, shape[i].y), out local[i])) return;
                roadDim.Add(local);
            }
            // Ground grid cells off the road.
            var xs = new List<float>(); var zs = new List<float>();
            foreach (var b in parts) { xs.Add(b.min.x); xs.Add(b.max.x); zs.Add(b.min.z); zs.Add(b.max.z); }
            foreach (var g in ground) { xs.Add(g.x); zs.Add(g.y); }
            xs.Sort(); zs.Sort();
            for (int x = 0; x < xs.Count - 1; x++)
            for (int z = 0; z < zs.Count - 1; z++)
            {
                float x0 = xs[x], x1 = xs[x + 1], z0 = zs[z], z1 = zs[z + 1];
                if (x1 <= x0 || z1 <= z0 || OnRoad(parts, new Vector2((x0 + x1) * .5f, (z0 + z1) * .5f))) continue;
                Dim(new List<Vector2> { new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0) });
            }
            // Outer corners of the whole road (nothing just beside, above or past them) get the gap outside a quarter circle dimmed.
            var done = new List<Vector2>();
            foreach (var b in parts)
                for (int q = 0; q < 4; q++)
                {
                    float sx = q == 0 || q == 3 ? 1 : -1, sz = q < 2 ? 1 : -1, step = RoadCorner * .5f;
                    var corner = new Vector2(sx > 0 ? b.max.x : b.min.x, sz > 0 ? b.max.z : b.min.z);
                    if (done.Exists(c => (c - corner).sqrMagnitude < RoadCorner * RoadCorner) || OnRoad(parts, corner + new Vector2(sx, -sz) * step) ||
                        OnRoad(parts, corner + new Vector2(-sx, sz) * step) || OnRoad(parts, corner + new Vector2(sx, sz) * step)) continue;
                    done.Add(corner);
                    var center = corner - new Vector2(sx, sz) * RoadCorner;
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = (q + i / 8f) * Mathf.PI * .5f, a1 = (q + (i + 1) / 8f) * Mathf.PI * .5f;
                        Dim(new List<Vector2> { corner, center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * RoadCorner, center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * RoadCorner });
                    }
                }
            roadBoxes.Add(box);
            return true;
        }
        static bool OnRoad(Bounds[] parts, Vector2 ground) => Array.Exists(parts, b => ground.x >= b.min.x && ground.x <= b.max.x && ground.y >= b.min.z && ground.y <= b.max.z);
        bool ToLocal(Vector3 world, out Vector2 local)
        {
            var p = worldCamera.WorldToScreenPoint(world); local = default;
            return p.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(spotlight.rectTransform, p, CanvasCamera, out local);
        }
        bool ToGround(Vector2 local, float y, out Vector2 ground)
        {
            var ray = worldCamera.ScreenPointToRay(RectTransformUtility.WorldToScreenPoint(CanvasCamera, spotlight.rectTransform.TransformPoint(local)));
            bool hit = new Plane(Vector3.up, new Vector3(0, y, 0)).Raycast(ray, out float d);
            var p = ray.GetPoint(d); ground = new Vector2(p.x, p.z);
            return hit;
        }

        // Adds the key's spotlight-local rects and returns how many; 0 when the key is unknown, hidden or off screen.
        int AddArea(string key)
        {
            if (TutorialKeys.IsEditorArea(key)) return editorArea != null && editorArea(key, corners) && AddCorners(CanvasCamera) ? 1 : 0;
            if (byKey.TryGetValue(key, out var binding) && binding.uiTarget)
            {
                if (!binding.uiTarget.gameObject.activeInHierarchy) return 0;
                // Scroll content (the code area) is drawn as the viewport the player sees, not its content size.
                var scroll = binding.uiTarget.GetComponentInParent<ScrollRect>();
                // A world-space canvas (e.g. the core health bar) projects through the battle camera.
                var owner = binding.uiTarget.GetComponentInParent<Canvas>();
                var camera = owner && owner.renderMode == RenderMode.WorldSpace ? worldCamera : CanvasCamera;
                // A bare holder of cards (no graphic of its own, e.g. the next-wave cards) is drawn around the children it shows.
                if (camera == CanvasCamera && !binding.uiTarget.GetComponent<Graphic>() && binding.uiTarget.childCount > 0 && !scroll)
                {
                    int start = screenAreas.Count;
                    foreach (RectTransform child in binding.uiTarget)
                        if (child.gameObject.activeSelf) { child.GetWorldCorners(corners); AddCorners(camera); }
                    if (screenAreas.Count == start) return 0;
                    var box = screenAreas[start];
                    for (int i = start + 1; i < screenAreas.Count; i++) box = Rect.MinMaxRect(Mathf.Min(box.xMin, screenAreas[i].xMin), Mathf.Min(box.yMin, screenAreas[i].yMin), Mathf.Max(box.xMax, screenAreas[i].xMax), Mathf.Max(box.yMax, screenAreas[i].yMax));
                    screenAreas.RemoveRange(start, screenAreas.Count - start); screenAreas.Add(box);
                    return 1;
                }
                (scroll && scroll.content == binding.uiTarget && scroll.viewport ? scroll.viewport : binding.uiTarget).GetWorldCorners(corners);
                return AddCorners(camera) ? 1 : 0;
            }
            if (!TryGetParts(key, out var parts, out bool round)) return 0;
            int added = 0;
            foreach (var bounds in parts)
            {
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                void Project(Vector3 world)
                {
                    var p = worldCamera.WorldToScreenPoint(world);
                    if (p.z <= 0) return;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(spotlight.rectTransform, p, CanvasCamera, out var local);
                    min = Vector2.Min(min, local); max = Vector2.Max(max, local);
                }
                if (round)
                    for (int i = 0; i < 32; i++)
                    {
                        float a = i * Mathf.PI / 16;
                        Project(bounds.center + new Vector3(Mathf.Cos(a) * bounds.extents.x, 0, Mathf.Sin(a) * bounds.extents.z));
                    }
                else
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        Project(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                if (!(min.x <= max.x && min.y <= max.y)) continue;
                screenAreas.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y)); added++;
                if (round) roundAreas.Add(screenAreas[screenAreas.Count - 1]);
            }
            if (added > 1 && IsBox(key))
            {
                int first = screenAreas.Count - added; var box = screenAreas[first];
                for (int i = first + 1; i < screenAreas.Count; i++) box = Rect.MinMaxRect(Mathf.Min(box.xMin, screenAreas[i].xMin), Mathf.Min(box.yMin, screenAreas[i].yMin), Mathf.Max(box.xMax, screenAreas[i].xMax), Mathf.Max(box.yMax, screenAreas[i].yMax));
                screenAreas.RemoveRange(first, added); screenAreas.Add(box); added = 1;
            }
            return added;
        }

        // UI world corners (in `corners`), seen through camera, to a spotlight-local rect.
        bool AddCorners(Camera camera)
        {
            var area = Rect.zero;
            for (int i = 0; i < corners.Length; i++)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(spotlight.rectTransform, p, CanvasCamera, out var local);
                area = i == 0 ? new Rect(local, Vector2.zero) : Rect.MinMaxRect(
                    Mathf.Min(area.xMin, local.x), Mathf.Min(area.yMin, local.y),
                    Mathf.Max(area.xMax, local.x), Mathf.Max(area.yMax, local.y));
            }
            screenAreas.Add(area);
            return true;
        }

        bool IsBox(string key) => runtimeTargets.TryGetValue(key, out var runtime) && runtime.box;
        bool TryGetParts(string key, out Bounds[] parts, out bool round)
        {
            round = false;
            if (runtimeTargets.TryGetValue(key, out var runtime) && runtime.owner && runtime.owner.activeInHierarchy)
            { parts = runtime.parts; round = runtime.round; return true; }
            if (byKey.TryGetValue(key, out var binding) && binding.worldTarget && binding.worldTarget.gameObject.activeInHierarchy)
            { parts = new[] { binding.worldTarget.bounds }; return true; }
            parts = null; return false;
        }

        public bool CanPerformAction(string key, GameObject target = null)
        {
            if (!IsGuiding) return true;
            if (!Acting || !ContainsKey(key)) return false;
            if (target && runtimeTargets.TryGetValue(key, out var runtime) && runtime.owner) return runtime.owner == target;
            if (target && byKey.TryGetValue(key, out var binding))
            {
                var expected = binding.uiTarget ? binding.uiTarget.gameObject : binding.worldTarget ? binding.worldTarget.gameObject : null;
                if (expected) return expected == target;
                if (binding.allowRuntimeTarget) return false;
            }
            return currentStep.completionMode != TutorialCompletionMode.Unconfigured;
        }

        public bool AllowsWorldPoint(Vector3 point)
        {
            if (!IsGuiding) return true;
            return Acting && Array.Exists(currentStep.targetKeys, key => Hits(key, point));
        }

        public bool AllowsRobotChoice(int index)
        {
            if (!IsGuiding) return true;
            if (!Acting) return false;
            bool hasRobotTarget = Array.Exists(currentStep.targetKeys, key =>
                !string.IsNullOrEmpty(key) && key.StartsWith(TutorialKeys.RobotButtonPrefix, StringComparison.Ordinal));
            return !hasRobotTarget || ContainsKey(TutorialKeys.RobotButton(index));
        }

        public bool TargetsAvailable(TutorialStep step)
        {
            if (step == null || step.kind != TutorialStepKind.ActionGuide) return true;
            foreach (var key in step.targetKeys)
            {
                // Code positions move while blocks are dragged, so they never count as lost.
                if (TutorialKeys.IsEditorArea(key)) continue;
                if (runtimeTargets.TryGetValue(key, out var runtime))
                { if (!runtime.owner || !runtime.owner.activeInHierarchy) return false; continue; }
                if (!byKey.TryGetValue(key, out var binding)) return false;
                if (binding.uiTarget && !binding.uiTarget.gameObject.activeInHierarchy) return false;
                if (binding.worldTarget && (!binding.worldTarget.enabled || !binding.worldTarget.gameObject.activeInHierarchy)) return false;
                if (!binding.uiTarget && !binding.worldTarget) return false;
            }
            return true;
        }

        public void RegisterWorldTarget(string key, GameObject owner, Bounds[] parts, bool round = false, bool box = false)
        {
            if (!owner || parts == null || parts.Length == 0 || !HasRuntimeTarget(key))
                throw new InvalidOperationException(name + ": 런타임 대상 키가 등록되지 않았습니다: " + key);
            // LateUpdate redraws; the game re-registers its targets every frame.
            runtimeTargets[key] = new RuntimeTarget { owner = owner, parts = parts, round = round, box = box };
        }

        public void UnregisterRuntimeTarget(string key, GameObject owner)
        {
            if (runtimeTargets.TryGetValue(key, out var target) && (!owner || target.owner == owner))
            { runtimeTargets.Remove(key); RefreshIfActing(); }
        }

        public void ReportWorldSuccess(Vector3 point, string signal)
        {
            if (!IsGuiding || !allowActions || currentStep == null) return;
            foreach (var key in currentStep.targetKeys) if (Hits(key, point)) ReportSuccess(key, signal);
        }

        public void ReportDrop(RectTransform source, RectTransform destination, string signal)
        {
            if (!IsGuiding || !allowActions || currentStep == null || !source || !destination) return;
            if (currentStep.completionMode == TutorialCompletionMode.DragDrop)
            {
                if (TargetContains(currentStep.sourceKey, source) && TargetMatches(currentStep.destinationKey, destination))
                    Completed?.Invoke(currentStep);
                return;
            }
            if (currentStep.completionMode != TutorialCompletionMode.ExternalSignal) return;
            foreach (var key in currentStep.targetKeys)
                if (TargetMatches(key, destination)) ReportSuccess(key, signal);
        }

        // The point lies over the key's world target footprint (height ignored).
        bool Hits(string key, Vector3 point) => TryGetParts(key, out var parts, out _) &&
            Array.Exists(parts, b => b.Contains(new Vector3(point.x, b.center.y, point.z)));
        bool TargetContains(string key, RectTransform actual)
        {
            if (!byKey.TryGetValue(key, out var binding) || !binding.uiTarget) return false;
            return actual == binding.uiTarget || actual.IsChildOf(binding.uiTarget);
        }

        bool TargetMatches(string key, RectTransform actual) =>
            byKey.TryGetValue(key, out var binding) && binding.uiTarget && actual == binding.uiTarget;

        public void ReportSuccess(string targetKey, string signal, GameObject target = null)
        {
            if (!IsGuiding || currentStep == null) return;
            if (!CanPerformAction(targetKey, target)) return;
            bool complete = currentStep.completionMode == TutorialCompletionMode.TargetClick && signal == TutorialSignals.TargetClicked ||
                currentStep.completionMode == TutorialCompletionMode.DragDrop && signal == "DropAccepted" ||
                currentStep.completionMode == TutorialCompletionMode.ExternalSignal && signal == currentStep.completionSignal;
            if (complete) Completed?.Invoke(currentStep);
        }

        // Spotlight-local rect for TutorialPointer. "a|b" tries each key in order; false when none is on screen.
        public bool TryGetArea(string key, out Rect area) => TryGetArea(key, out area, out _);
        public bool TryGetArea(string key, out Rect area, out string chosen)
        {
            area = default; chosen = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            foreach (var option in key.Split('|'))
            {
                int before = screenAreas.Count, rounds = roundAreas.Count;
                if (AddArea(option.Trim()) == 0) continue;
                area = screenAreas[before];
                for (int i = before + 1; i < screenAreas.Count; i++)
                    area = Rect.MinMaxRect(Mathf.Min(area.xMin, screenAreas[i].xMin), Mathf.Min(area.yMin, screenAreas[i].yMin),
                        Mathf.Max(area.xMax, screenAreas[i].xMax), Mathf.Max(area.yMax, screenAreas[i].yMax));
                // Only measured: the drawn areas come from RefreshAreas.
                screenAreas.RemoveRange(before, screenAreas.Count - before); roundAreas.RemoveRange(rounds, roundAreas.Count - rounds);
                chosen = option.Trim();
                return true;
            }
            return false;
        }

        public bool HasTarget(string key) => !string.IsNullOrWhiteSpace(key) && (TutorialKeys.IsRuntime(key) || TutorialKeys.IsEditorArea(key) ||
            Array.Exists(targets, binding => binding != null && binding.key == key && (binding.allowRuntimeTarget || binding.uiTarget || binding.worldTarget)));
        public bool HasRuntimeTarget(string key) => !string.IsNullOrWhiteSpace(key) &&
            (TutorialKeys.IsRuntime(key) || byKey.TryGetValue(key, out var binding) && binding.allowRuntimeTarget);

        bool ContainsKey(string key) => Array.Exists(currentStep.targetKeys, k => k == key || k == "*");
    }
}
