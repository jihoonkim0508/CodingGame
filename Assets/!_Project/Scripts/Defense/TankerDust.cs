using System;
using UnityEngine;

namespace CodingGame.Defense
{
    // Dust kicked up where the tanker's feet come down (시안/탱커 흙먼지): watches each foot's bone (its sole) and emits a few
    // particles of the effect there as it lands. The effect simulates in world space, so the puff stays where the foot landed;
    // a tanker standing still (blocked, stunned, paused) lands no feet and raises no dust. Set up by TankerDustEffects.Attach.
    public sealed class TankerDust : MonoBehaviour
    {
        [SerializeField, Tooltip("발 디딜 때 흙먼지 (Defense/Effects/TankerDust의 시안 프리팹)")] ParticleSystem effect;
        [SerializeField, Tooltip("발바닥 본(Foot_L/R)")] Transform[] legs = Array.Empty<Transform>();
        [SerializeField, Min(1)] int perStep = 3;
        // Heights in Heavy's own units (the shield brute's stomp lifts a foot about 0.17).
        const float Lifted = .05f, Landed = .015f;
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
                var foot = legs[i].position;
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
