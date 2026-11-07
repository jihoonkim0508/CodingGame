using UnityEngine;
namespace CodingGame.Defense
{
    // Fill the old HUD gutters with the same world view, retaining the original screen-space composition.
    [DefaultExecutionOrder(1000), RequireComponent(typeof(Camera))]
    public sealed class DefenseCameraLensOffset : MonoBehaviour
    {
        [SerializeField] Vector2 centerOffset;
        Camera target;
        public void Configure(Vector2 value) { centerOffset=value; }
        void Awake() => target=GetComponent<Camera>();
        void LateUpdate()
        {
            target.ResetProjectionMatrix();
            var projection=target.projectionMatrix;
            projection.m02-=centerOffset.x*2;
            projection.m12-=centerOffset.y*2;
            target.projectionMatrix=projection;
        }
        void OnDisable() { if(target) target.ResetProjectionMatrix(); }
    }
}
