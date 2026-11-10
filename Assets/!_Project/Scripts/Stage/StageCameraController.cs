using UnityEngine;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageCameraController : MonoBehaviour
    {
        public const float Duration = 1.5f;
        Camera controlledCamera;
        Vector3 startPosition, controlPosition, targetPosition;
        Quaternion startRotation, targetRotation;
        float startFov, targetFov;
        float elapsed = Duration;
        float activeDuration = Duration;
        Vector3 startLook, targetLook;
        StageNode[] framedNodes;
        float framedAspect;
        bool focusing;
        float overviewFov;
        bool followingRoute;

        public bool IsMoving => followingRoute || elapsed < activeDuration;

        // A short settling move reveals the whole map before selection begins.
        public void Configure(Camera camera, StageNode[] nodes)
        {
            controlledCamera = camera;
            followingRoute = false;
            overviewFov = camera.fieldOfView;
            framedNodes = nodes;
            focusing = false;
            framedAspect = camera.aspect;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            FrameOverview(true);
        }

        void FrameOverview(bool animate)
        {
            var camera = controlledCamera;
            var nodes = framedNodes;
            var bounds = nodes[0].VisualBounds;
            foreach (StageNode node in nodes) bounds.Encapsulate(node.VisualBounds);
            Quaternion view = Quaternion.Euler(42f, 0f, 0f);
            float distance=FrameDistance(camera,bounds,view);
            targetLook = bounds.center;
            targetPosition = targetLook - view * Vector3.forward * distance;
            targetRotation = view;
            targetFov = startFov = camera.fieldOfView;
            startLook = targetLook;
            startPosition = targetPosition - view * Vector3.forward * distance * .06f + Vector3.up * .35f;
            startRotation = Quaternion.LookRotation(startLook - startPosition);
            controlPosition = Vector3.Lerp(startPosition, targetPosition, .5f);
            activeDuration = animate ? .85f : 0;
            elapsed = 0;
            camera.transform.SetPositionAndRotation(animate ? startPosition : targetPosition, animate ? startRotation : targetRotation);
        }

        public static float FrameDistance(Camera camera,Bounds bounds,Quaternion rotation, float fieldOfView = 0)
        {
            float tanY=Mathf.Tan((fieldOfView > 0 ? fieldOfView : camera.fieldOfView)*Mathf.Deg2Rad*.5f),tanX=tanY*Mathf.Max(.5f,camera.aspect);
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
            followingRoute = false;
            focusing = true;
            activeDuration = Duration;
            Quaternion rotation = Quaternion.Euler(28f, 4f, 0f);
            var node = stage.GetComponent<StageNode>();
            var bounds = node ? DetailBounds(node) : new Bounds(stage.position, Vector3.one * 3);
            targetLook = bounds.center;
            targetFov = 34f;
            Vector3 position = targetLook - rotation * Vector3.forward * FrameDistance(controlledCamera, bounds, rotation, targetFov);
            startPosition = controlledCamera.transform.position;
            startRotation = controlledCamera.transform.rotation;
            startFov = controlledCamera.fieldOfView;
            targetPosition = position;
            targetRotation = rotation;
            // Interpolate the point being watched, so the island stays in frame throughout the flight.
            float depth = Mathf.Max(1f, Vector3.Dot(targetLook - startPosition, controlledCamera.transform.forward));
            startLook = startPosition + controlledCamera.transform.forward * depth;
            Vector3 travel = targetPosition - startPosition;
            controlPosition = Vector3.Lerp(startPosition, targetPosition, 0.48f)
                              + Vector3.up * Mathf.Min(.65f, travel.magnitude * .04f);
            elapsed = 0f;
        }

        public void ShowUnlockRoute(StageNode from, StageNode to)
        {
            var bounds=from.VisualBounds; bounds.Encapsulate(to.VisualBounds);
            BeginShot(bounds,Quaternion.Euler(58,0,0),44,.8f);
        }

        public static Bounds DetailBounds(StageNode node)
        {
            var bounds=node.VisualBounds;
            bounds.center+=Vector3.up*.18f;
            bounds.extents=Vector3.Scale(bounds.extents,new Vector3(.72f,.86f,.72f));
            return bounds;
        }

        public void ShowPurificationIsland(StageNode node) => BeginShot(DetailBounds(node),Quaternion.Euler(28,14,0),34,.85f);
        public void RiseOverPurification(StageNode node) => BeginShot(DetailBounds(node),Quaternion.Euler(46,5,0),32,1.25f);
        public void ShowUnlockIsland(StageNode node) => BeginShot(DetailBounds(node),Quaternion.Euler(26,-9,0),32,1.05f);
        public void ShowOpenedIsland(StageNode node) => BeginShot(DetailBounds(node),Quaternion.Euler(40,-5,0),34,.6f);

        public void FollowUnlockPulse(Vector3 point)
        {
            followingRoute=true;focusing=true;
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*5);
            var rotation=Quaternion.Euler(34,5,0);
            var look=point+Vector3.up*.35f;
            var position=look-rotation*Vector3.forward*8.4f;
            var cameraTransform=controlledCamera.transform;
            Vector3 next=Vector3.Lerp(cameraTransform.position,position,blend);
            cameraTransform.SetPositionAndRotation(next,Quaternion.Slerp(cameraTransform.rotation,Quaternion.LookRotation(look-next),blend));
            controlledCamera.fieldOfView=Mathf.Lerp(controlledCamera.fieldOfView,38,blend);
        }

        public void RestoreOverview()
        {
            var bounds=framedNodes[0].VisualBounds;
            foreach(var node in framedNodes) bounds.Encapsulate(node.VisualBounds);
            BeginShot(bounds,Quaternion.Euler(42,0,0),overviewFov,1.05f);
            focusing=false; framedAspect=controlledCamera.aspect;
        }

        void BeginShot(Bounds bounds,Quaternion rotation,float fov,float seconds)
        {
            followingRoute=false;
            focusing=true; activeDuration=seconds; elapsed=0;
            startPosition=controlledCamera.transform.position; startFov=controlledCamera.fieldOfView;
            targetLook=bounds.center; targetFov=fov;
            targetPosition=targetLook-rotation*Vector3.forward*FrameDistance(controlledCamera,bounds,rotation,fov);
            float depth=Mathf.Max(1,Vector3.Dot(targetLook-startPosition,controlledCamera.transform.forward));
            startLook=startPosition+controlledCamera.transform.forward*depth;
            controlPosition=Vector3.Lerp(startPosition,targetPosition,.5f)+Vector3.up*.45f;
        }

        void LateUpdate()
        {
            if (!controlledCamera) return;
            if (followingRoute) return;
            if (!focusing && !Mathf.Approximately(framedAspect, controlledCamera.aspect))
            {
                framedAspect = controlledCamera.aspect;
                FrameOverview(false);
            }
            if (!IsMoving) return;
            elapsed += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(elapsed / activeDuration);
            float t = n * n * n * (n * (6f * n - 15f) + 10f);
            Vector3 first = Vector3.Lerp(startPosition, controlPosition, t);
            Vector3 second = Vector3.Lerp(controlPosition, targetPosition, t);
            Vector3 position = Vector3.Lerp(first, second, t);
            controlledCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.Lerp(startLook, targetLook, t) - position));
            controlledCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
        }
    }
}
