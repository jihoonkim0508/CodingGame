using UnityEngine;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageCameraController : MonoBehaviour
    {
        public const float Duration = 1.15f;
        Camera controlledCamera;
        Vector3 startPosition, controlPosition, targetPosition;
        Quaternion startRotation, targetRotation;
        float startFov, targetFov;
        float elapsed = Duration;

        public bool IsMoving => elapsed < Duration;

        // Frames every node from a fixed 55° overview.
        public void Configure(Camera camera, StageNode[] nodes)
        {
            controlledCamera = camera;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            var bounds = nodes[0].VisualBounds;
            foreach (StageNode node in nodes) bounds.Encapsulate(node.VisualBounds);
            Quaternion view = Quaternion.Euler(42f, 0f, 0f);
            float distance=FrameDistance(camera,bounds,view);
            camera.transform.SetPositionAndRotation(bounds.center - view * Vector3.forward * distance, view);
        }

        public static float FrameDistance(Camera camera,Bounds bounds,Quaternion rotation)
        {
            float tanY=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),tanX=tanY*Mathf.Max(.5f,camera.aspect);
            float distance=1;var inverse=Quaternion.Inverse(rotation);
            for(int i=0;i<8;i++)
            {
                var p=inverse*Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                distance=Mathf.Max(distance,Mathf.Abs(p.x)/tanX-p.z,Mathf.Abs(p.y)/tanY-p.z);
            }
            return distance*1.17f;
        }

        // Arcs toward the chosen stage; the scene fades out once it arrives.
        public void Focus(Transform stage)
        {
            Quaternion rotation = Quaternion.Euler(23f, -27f, 0f);
            Vector3 position = stage.position + Vector3.up * 2.05f - rotation * Vector3.forward * 3.8f;
            startPosition = controlledCamera.transform.position;
            startRotation = controlledCamera.transform.rotation;
            startFov = controlledCamera.fieldOfView;
            targetPosition = position;
            targetRotation = Quaternion.LookRotation(stage.position + Vector3.up * 0.35f - position);
            targetFov = 42f;
            Vector3 travel = targetPosition - startPosition;
            Vector3 side = Vector3.Cross(Vector3.up, travel.normalized);
            controlPosition = Vector3.Lerp(startPosition, targetPosition, 0.48f)
                              + Vector3.up * Mathf.Min(2.2f, travel.magnitude * 0.16f)
                              + side * Mathf.Min(0.8f, travel.magnitude * 0.05f);
            elapsed = 0f;
        }

        void LateUpdate()
        {
            if (!IsMoving) return;
            elapsed += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(elapsed / Duration);
            float t = n * n * n * (n * (6f * n - 15f) + 10f);
            Vector3 first = Vector3.Lerp(startPosition, controlPosition, t);
            Vector3 second = Vector3.Lerp(controlPosition, targetPosition, t);
            controlledCamera.transform.SetPositionAndRotation(Vector3.Lerp(first, second, t), Quaternion.Slerp(startRotation, targetRotation, t));
            controlledCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
        }
    }
}
