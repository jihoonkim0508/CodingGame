using System;
using UnityEngine;

namespace CodingGame.Defense
{
    // Dust kicked up where the tanker's feet come down (시안/탱커 흙먼지): watches the pod end of each leg bone and emits a few
    // particles of the effect there as it lands. The effect simulates in world space, so the puff stays where the foot landed;
    // a tanker standing still (blocked, stunned, paused) lands no feet and raises no dust. Set up by TankerDustEffects.Attach.
    public sealed class TankerDust : MonoBehaviour
    {
        [SerializeField, Tooltip("발 디딜 때 흙먼지 (Defense/Effects/TankerDust의 시안 프리팹)")] ParticleSystem effect;
        [SerializeField] Transform[] legs = Array.Empty<Transform>();
        [SerializeField, Min(1)] int perStep = 3;
        // Each leg is one bone, its pod's foot at -X 0.892 (TankerWalk); heights in Heavy's own units (the walk lifts a pod 0.1).
        const float LegLength = .892f, Lifted = .05f, Landed = .015f;
        ParticleSystem dust;
        DefenseActorView view;
        float[] low;
        bool[] up;

        void LateUpdate() => Tick();

        // Public for the editor's recordings, which pose the walk by hand in a preview scene where LateUpdate does not run.
        public void Tick()
        {
            if (!effect) return;
            if (!dust)
            {
                dust = Instantiate(effect, transform);
                view = GetComponent<DefenseActorView>();
                low = new float[legs.Length]; up = new bool[legs.Length];
                for (int i = 0; i < low.Length; i++) low[i] = float.MaxValue;
            }
            // Paused or sped up with the battle clock, as the walk is.
            float pace = view ? view.Pace : 1;
            var main = dust.main; if (main.simulationSpeed != pace) main.simulationSpeed = pace;
            for (int i = 0; i < legs.Length; i++)
            {
                var foot = legs[i].TransformPoint(-LegLength, 0, 0);
                float y = transform.InverseTransformPoint(foot).y;
                low[i] = Mathf.Min(low[i], y);
                if (y > low[i] + Lifted) up[i] = true;
                else if (up[i] && y < low[i] + Landed)
                {
                    up[i] = false;
                    foot.y = transform.position.y + .08f;
                    dust.Emit(new ParticleSystem.EmitParams { position = foot, applyShapeToPosition = true }, perStep);
                }
            }
        }
    }
}
