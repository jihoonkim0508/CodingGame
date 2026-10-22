using System;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense
{
    // On the core: glitches for a moment when an enemy gets through and breaks up when the core falls (planner: Object Glitch
    // Shader "Random Sphere Glitch" / "Random Sphere Alpha"). Each part keeps its own colour and texture on the glitch copies.
    public sealed class DefenseCoreGlitch : MonoBehaviour
    {
        [SerializeField] Material hit, death;
        [SerializeField, Min(.05f)] float hitSeconds = .6f;
        [SerializeField, Tooltip("맵이 가진 기지 모델(Stage01 Workshop의 CodeBase): 이 모델도 함께 글리치된다")] Transform model;
        Renderer[] parts;
        Material[][] original, hitLook, deathLook, shown;
        int health = -1;
        float until;

        void Awake()
        {
            if (!hit || !death) throw new InvalidOperationException(name + ": 코어 글리치 머티리얼을 연결하세요.");
            parts = GetComponentsInChildren<Renderer>(true).Concat(model ? model.GetComponentsInChildren<Renderer>(true) : Enumerable.Empty<Renderer>()).ToArray();
            original = parts.Select(r => r.sharedMaterials).ToArray();
            hitLook = Copies(hit); deathLook = Copies(death);
            // The sample's glitch sphere (radius .25) only nicks a part this size; 2 covers the whole core (checked in play).
            foreach (var copy in hitLook.SelectMany(set => set)) copy.SetFloat("_SphereRadius", 2);
            shown = original;
        }
        Material[][] Copies(Material glitch) => original.Select(set => set.Select(source => Copy(glitch, source)).ToArray()).ToArray();
        static Material Copy(Material glitch, Material source)
        {
            var copy = new Material(glitch);
            if (!source) return copy;
            if (source.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            if (source.HasProperty("_BaseMap")) copy.SetTexture("_BaseColorMap", source.GetTexture("_BaseMap"));
            return copy;
        }

        // Called every frame with the simulation's core health; a drop starts the hit glitch.
        public void Show(int baseHealth, bool defeated)
        {
            if (health >= 0 && baseHealth < health) until = Time.time + hitSeconds;
            health = baseHealth;
            var look = defeated ? deathLook : Time.time < until ? hitLook : original;
            if (look == shown) return;
            shown = look;
            for (int i = 0; i < parts.Length; i++) if (parts[i]) parts[i].sharedMaterials = look[i];
        }
    }
}
