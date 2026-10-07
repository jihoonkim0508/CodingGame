using System;
using System.Collections.Generic;
using UnityEngine;

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
            public Bounds bounds;
        }

        [SerializeField] Canvas canvas;
        [SerializeField] Camera worldCamera;
        [SerializeField] TutorialSpotlight spotlight;
        [SerializeField] TutorialTargetBinding[] targets = Array.Empty<TutorialTargetBinding>();
        readonly List<Rect> screenAreas = new List<Rect>();
        readonly Dictionary<string, TutorialTargetBinding> byKey = new Dictionary<string, TutorialTargetBinding>();
        readonly Dictionary<string, RuntimeTarget> runtimeTargets = new Dictionary<string, RuntimeTarget>();
        TutorialDirector director;
        TutorialStep currentStep;
        bool allowActions;
        public event Action<TutorialStep> Completed;
        public bool IsGuiding => director && director.IsActive;
        public bool PauseBattle => IsGuiding && currentStep != null && currentStep.pauseBattle;
        public bool AllowPreview => IsGuiding && currentStep != null && currentStep.allowPreview;

        public void Initialize(TutorialDirector owner)
        {
            director = owner;
            if (!canvas || !spotlight) throw new InvalidOperationException(name + ": Canvas/Spotlight 참조 누락");
            if (targets == null) targets = Array.Empty<TutorialTargetBinding>();
            byKey.Clear(); runtimeTargets.Clear();
            foreach (var target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.key) ||
                    (!target.allowRuntimeTarget && !target.uiTarget && !target.worldTarget) ||
                    target.uiTarget && target.worldTarget || byKey.ContainsKey(target.key))
                    throw new InvalidOperationException(name + ": TutorialTargetBinding key/참조가 잘못됐습니다.");
                byKey.Add(target.key, target);
            }
            if (Array.Exists(targets, target => target != null && target.worldTarget) && !worldCamera)
                throw new InvalidOperationException(name + ": 월드 대상용 Camera 참조가 누락됐습니다.");
            SetStep(null);
        }

        public void SetStep(TutorialStep step, bool enableActions = false)
        {
            currentStep = step;
            allowActions = enableActions;
            if (!spotlight) return;
            spotlight.enabled = step != null;
            spotlight.raycastTarget = step != null;
            RefreshAreas();
        }

        void LateUpdate()
        {
            if (currentStep != null && allowActions) RefreshAreas();
        }

        void RefreshAreas()
        {
            if (!spotlight) return;
            screenAreas.Clear();
            if (allowActions && currentStep != null)
                foreach (var key in currentStep.targetKeys)
                    if (byKey.TryGetValue(key, out var binding)) AddArea(key, binding);
            spotlight.SetHoles(screenAreas, currentStep?.highlightPadding ?? 0);
        }

        void AddArea(string key, TutorialTargetBinding binding)
        {
            if (TryGetUI(key, binding, out var uiTarget))
            {
                var corners = new Vector3[4]; uiTarget.GetWorldCorners(corners);
                var area = Rect.zero;
                for (int i = 0; i < corners.Length; i++)
                {
                    var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    var p = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(spotlight.rectTransform, p, camera, out var local);
                    area = i == 0 ? new Rect(local, Vector2.zero) : Rect.MinMaxRect(
                        Mathf.Min(area.xMin, local.x), Mathf.Min(area.yMin, local.y),
                        Mathf.Max(area.xMax, local.x), Mathf.Max(area.yMax, local.y));
                }
                screenAreas.Add(area);
                return;
            }
            if (!TryGetBounds(key, binding, out var bounds)) return;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                var p = worldCamera.WorldToScreenPoint(world);
                if (p.z <= 0) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(spotlight.rectTransform, p,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            if (min.x <= max.x && min.y <= max.y) screenAreas.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        bool TryGetUI(string key, TutorialTargetBinding binding, out RectTransform target)
        {
            target = binding.uiTarget;
            return target;
        }

        bool TryGetBounds(string key, TutorialTargetBinding binding, out Bounds bounds)
        {
            if (runtimeTargets.TryGetValue(key, out var runtime) && runtime.owner)
            { bounds = runtime.bounds; return true; }
            if (binding.worldTarget)
            { bounds = binding.worldTarget.bounds; return true; }
            bounds = default; return false;
        }

        public bool CanPerformAction(string key, GameObject target = null)
        {
            if (!IsGuiding) return true;
            if (!allowActions || currentStep == null || currentStep.kind != TutorialStepKind.ActionGuide || !ContainsKey(key)) return false;
            if (target && byKey.TryGetValue(key, out var binding))
            {
                if (runtimeTargets.TryGetValue(key, out var runtime) && runtime.owner) return runtime.owner == target;
                var expected = binding.uiTarget ? binding.uiTarget.gameObject : binding.worldTarget ? binding.worldTarget.gameObject : null;
                if (expected) return expected == target;
                if (binding.allowRuntimeTarget) return false;
            }
            return currentStep.completionMode != TutorialCompletionMode.Unconfigured;
        }

        public bool AllowsWorldPoint(Vector3 point)
        {
            if (!IsGuiding) return true;
            if (!allowActions || currentStep == null || currentStep.kind != TutorialStepKind.ActionGuide) return false;
            foreach (var key in currentStep.targetKeys)
                if (byKey.TryGetValue(key, out var binding) && TryGetBounds(key, binding, out var bounds) &&
                    bounds.Contains(new Vector3(point.x, bounds.center.y, point.z))) return true;
            return false;
        }

        public bool AllowsRobotChoice(int index)
        {
            if (!IsGuiding) return true;
            if (!allowActions || currentStep == null || currentStep.kind != TutorialStepKind.ActionGuide) return false;
            bool hasRobotTarget = Array.Exists(currentStep.targetKeys ?? Array.Empty<string>(), key =>
                !string.IsNullOrEmpty(key) && key.StartsWith("Battle.Robot.", StringComparison.Ordinal));
            return !hasRobotTarget || ContainsKey("Battle.Robot." + index);
        }

        public bool TargetsAvailable(TutorialStep step)
        {
            if (step == null || step.kind != TutorialStepKind.ActionGuide) return true;
            foreach (var key in step.targetKeys)
            {
                if (!byKey.TryGetValue(key, out var binding)) return false;
                if (runtimeTargets.TryGetValue(key, out var runtime))
                { if (!runtime.owner || !runtime.owner.activeInHierarchy) return false; continue; }
                if (binding.uiTarget && !binding.uiTarget.gameObject.activeInHierarchy) return false;
                if (binding.worldTarget && (!binding.worldTarget.enabled || !binding.worldTarget.gameObject.activeInHierarchy)) return false;
                if (!binding.uiTarget && !binding.worldTarget) return false;
            }
            return true;
        }

        public void RegisterWorldTarget(string key, GameObject owner, Bounds bounds)
        {
            if (!owner || !byKey.TryGetValue(key, out var binding) || !binding.allowRuntimeTarget)
                throw new InvalidOperationException(name + ": 런타임 대상 키가 등록되지 않았습니다: " + key);
            runtimeTargets[key] = new RuntimeTarget { owner = owner, bounds = bounds };
            if (currentStep != null && allowActions) RefreshAreas();
        }

        public void UnregisterRuntimeTarget(string key, GameObject owner)
        {
            if (runtimeTargets.TryGetValue(key, out var target) && (!owner || target.owner == owner))
            { runtimeTargets.Remove(key); if (currentStep != null && allowActions) RefreshAreas(); }
        }

        public void ReportWorldSuccess(Vector3 point, string signal)
        {
            if (!IsGuiding || !allowActions || currentStep == null) return;
            foreach (var key in currentStep.targetKeys)
                if (byKey.TryGetValue(key, out var binding) && TryGetBounds(key, binding, out var bounds) &&
                    bounds.Contains(new Vector3(point.x, bounds.center.y, point.z))) ReportSuccess(key, signal);
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
            bool complete = currentStep.completionMode == TutorialCompletionMode.TargetClick && signal == "TargetClicked" ||
                currentStep.completionMode == TutorialCompletionMode.DragDrop && signal == "DropAccepted" ||
                currentStep.completionMode == TutorialCompletionMode.ExternalSignal && signal == currentStep.completionSignal;
            if (complete) Completed?.Invoke(currentStep);
        }

        public bool HasTarget(string key) => !string.IsNullOrWhiteSpace(key) && byKey.TryGetValue(key, out var binding) &&
            (binding.allowRuntimeTarget || binding.uiTarget || binding.worldTarget);
        public bool HasRuntimeTarget(string key) => !string.IsNullOrWhiteSpace(key) && byKey.TryGetValue(key, out var binding) && binding.allowRuntimeTarget;

        bool ContainsKey(string key) => Array.Exists(currentStep.targetKeys ?? Array.Empty<string>(), k => k == key || k == "*");
    }

    public sealed class TutorialTarget : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        [SerializeField] TutorialInputBridge bridge;
        [SerializeField] string targetKey;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                bridge.ReportSuccess(targetKey, "TargetClicked", gameObject);
        }
    }
}
