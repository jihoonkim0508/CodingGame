using UnityEngine;
namespace CodingGame.Defense
{
    // Fill the old HUD gutters with the same world view, retaining the original screen-space composition.
    [DefaultExecutionOrder(1000), RequireComponent(typeof(Camera))]
    public sealed class DefenseCameraLensOffset : MonoBehaviour
    {
        [SerializeField] Vector2 centerOffset;
        Camera target;
        // A screen shake on top of the lens (DefenseBattleFx: big moments, the boss's footsteps): set every frame, never moves the camera.
        public static Vector2 Shake;
        public void Configure(Vector2 value) { centerOffset=value; }
        void Awake() => target=GetComponent<Camera>();
        void LateUpdate() => Apply();
        // Also for the editor, which places screen UI over the world with this lens (the core bar).
        public void Apply()
        {
            if (!target) target = GetComponent<Camera>();
            target.ResetProjectionMatrix();
            var projection=target.projectionMatrix;
            projection.m02-=(centerOffset.x+Shake.x)*2;
            projection.m12-=(centerOffset.y+Shake.y)*2;
            target.projectionMatrix=projection;
        }
        void OnDisable() { if(target) target.ResetProjectionMatrix(); }
    }
}
