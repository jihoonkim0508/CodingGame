using System;
using UnityEngine;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense
{
    // Shared conversions for views that mirror the simulation's XZ plane.
    static class DefenseViews
    {
        public static Vector3 World(Point p, float y = 0) => new Vector3(p.X, y, p.Y);
        // Deactivate first so the object stops rendering and raycasting this frame, before Destroy runs.
        public static void Discard(Component view)
        {
            if (!view) return;
            view.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(view.gameObject);
        }
    }

    public sealed class DefenseActorView : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Renderer body;
        [SerializeField] Transform healthRoot;
        [SerializeField] Transform healthFill;
        [SerializeField] LineRenderer beam;
        MaterialPropertyBlock colors;
        Color baseColor;
        Vector3 fullHealthScale;
        float beamTime;
        void Awake()
        {
            if (!head || !body || !healthRoot || !healthFill || !beam || !beam.sharedMaterial)
                throw new InvalidOperationException(name + ": ActorView Inspector 참조 누락");
            colors = new MaterialPropertyBlock(); baseColor = body.sharedMaterial.GetColor("_BaseColor");
            fullHealthScale = healthFill.localScale;
            beam.enabled = false;
        }
        public void SyncRobot(RobotState bot, Vector3 position, Quaternion viewRotation) =>
            Sync(position, bot.Health, bot.MaxHealth, false, false, bot.Buffs.Count > 0, viewRotation);
        public void SyncEnemy(EnemyState enemy, double time, Vector3 position, Quaternion viewRotation) =>
            Sync(position, enemy.Health, enemy.Spec.health, enemy.Stunned(time), enemy.Speed(time) < enemy.Spec.speed, false, viewRotation);
        void Sync(Vector3 position, float? health, float maximum, bool stunned, bool slowed, bool buffed, Quaternion viewRotation)
        {
            transform.position = position;
            healthRoot.gameObject.SetActive(health.HasValue);
            if (health.HasValue)
            {
                healthRoot.rotation = viewRotation;
                float ratio = maximum > 0 ? Mathf.Clamp01(health.Value / maximum) : 0;
                healthFill.localScale = new Vector3(fullHealthScale.x * ratio, fullHealthScale.y, fullHealthScale.z);
            }
            Color tint = stunned ? new Color(.8f,.4f,1) : slowed ? new Color(.2f,.8f,1) : buffed ? Color.Lerp(baseColor, Color.white, .35f) : baseColor;
            colors.SetColor("_BaseColor", tint); body.SetPropertyBlock(colors);
        }
        public void AimAt(Vector3 target)
        {
            Vector3 direction = target - head.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) head.rotation = Quaternion.LookRotation(direction);
        }
        public void FireAt(Vector3 target)
        {
            AimAt(target);
            Vector3 from = head.position;
            beam.useWorldSpace = true; beam.positionCount = 2;
            beam.SetPosition(0, from); beam.SetPosition(1, target + Vector3.up * .45f);
            beam.enabled = true; beamTime = .12f;
        }
        public void TickVisual(float delta)
        {
            beamTime -= delta;
            if (beamTime <= 0) beam.enabled = false;
        }
        public void ClearVisuals() { beamTime = 0; beam.enabled = false; }
    }
}
