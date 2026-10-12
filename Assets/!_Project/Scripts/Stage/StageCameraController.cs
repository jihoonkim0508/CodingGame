using UnityEngine;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageCameraController : MonoBehaviour
    {
        Camera controlledCamera;
        Vector3 overviewPosition, startPosition, controlPosition, targetPosition;
        Quaternion overviewRotation, startRotation, targetRotation;
        float overviewFov, startFov, targetFov;
        float elapsed;
        const float Duration = 1.15f;

        public bool IsMoving => elapsed < Duration;

        public void Configure(Camera camera, StageNode[] nodes)
        {
            controlledCamera = camera;
            controlledCamera.rect = new Rect(0f, 0f, 1f, 1f);
            if (nodes != null && nodes.Length > 0)
            {
                Bounds bounds = new Bounds(nodes[0].transform.position, Vector3.zero);
                foreach (StageNode node in nodes) bounds.Encapsulate(node.transform.position);
                Quaternion overviewView = Quaternion.Euler(55f, 0f, 0f);
                float distance = Mathf.Max(13f, bounds.size.x * 0.78f);
                Vector3 focus = bounds.center;
                Vector3 position = focus - overviewView * Vector3.forward * distance;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
            }
            overviewPosition = camera.transform.position;
            overviewRotation = camera.transform.rotation;
            overviewFov = camera.fieldOfView;
            targetPosition = overviewPosition;
            targetRotation = overviewRotation;
            targetFov = overviewFov;
            elapsed = Duration;
        }

        public void Focus(Transform stage)
        {
            Quaternion rotation = Quaternion.Euler(23f, -27f, 0f);
            Vector3 position = stage.position + Vector3.up * 2.05f - rotation * Vector3.forward * 3.8f;
            Begin(position, Quaternion.LookRotation(stage.position + Vector3.up * 0.35f - position), 42f);
        }

        public void Restore() => Begin(overviewPosition, overviewRotation, overviewFov);

        void Begin(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            startPosition = controlledCamera.transform.position;
            startRotation = controlledCamera.transform.rotation;
            startFov = controlledCamera.fieldOfView;
            targetPosition = position;
            targetRotation = rotation;
            targetFov = fieldOfView;
            Vector3 travel = targetPosition - startPosition;
            Vector3 side = Vector3.Cross(Vector3.up, travel.normalized);
            controlPosition = Vector3.Lerp(startPosition, targetPosition, 0.48f)
                              + Vector3.up * Mathf.Min(2.2f, travel.magnitude * 0.16f)
                              + side * Mathf.Min(0.8f, travel.magnitude * 0.05f);
            elapsed = 0f;
        }

        void LateUpdate()
        {
            if (!controlledCamera || elapsed >= Duration) return;
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / Duration);
            float t = normalized * normalized * normalized * (normalized * (6f * normalized - 15f) + 10f);
            Vector3 first = Vector3.Lerp(startPosition, controlPosition, t);
            Vector3 second = Vector3.Lerp(controlPosition, targetPosition, t);
            controlledCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(first, second, t),
                Quaternion.Slerp(startRotation, targetRotation, t));
            controlledCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
        }
    }
}
