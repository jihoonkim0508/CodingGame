using UnityEngine;

namespace CodingGame.Defense
{
    // Kept beside Model, so replacing an FBX or its rig does not remove the effects.
    public sealed class DefenseMonsterEffects : MonoBehaviour
    {
        [SerializeField] ParticleSystem[] ambient = new ParticleSystem[0];
        [SerializeField] ParticleSystem[] movement = new ParticleSystem[0];
        bool moving, stunned;

        void Awake()
        {
            ResetParticles(ambient);
            ResetParticles(movement);
        }

        static void ResetParticles(ParticleSystem[] systems)
        {
            foreach (var system in systems)
            {
                if (!system) continue;
                system.Simulate(0, false, true, false);
                system.Pause(false);
            }
        }

        public void SyncMovement(bool moving, bool stunned)
        {
            this.moving = moving;
            this.stunned = stunned;
        }

        // delta is elapsed simulation time: pause, tutorial holds and battle speed all follow it.
        public void Tick(float delta)
        {
            if (delta <= 0) return;
            Simulate(ambient, !stunned, delta);
            Simulate(movement, moving && !stunned, delta);
        }

        static void Simulate(ParticleSystem[] systems, bool emit, float delta)
        {
            foreach (var system in systems)
            {
                if (!system) continue;
                var emission = system.emission;
                emission.enabled = emit;
                system.Simulate(delta, false, false, false);
            }
        }

        public void Clear()
        {
            moving = false;
            ResetParticles(ambient);
            ResetParticles(movement);
        }
    }
}
