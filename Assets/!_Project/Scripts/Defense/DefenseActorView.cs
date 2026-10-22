using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
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
        // A look-alike of a UI element (e.g. a palette block) under parent: copied while inactive so none of its scripts run,
        // then stripped to graphics and layout; the palette's stock count ("x1") is dropped. Returned inactive.
        public static RectTransform GraphicCopy(RectTransform source, Transform parent)
        {
            var holder = new GameObject(source.name + " (copy)", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(parent, false);
            var copy = (RectTransform)UnityEngine.Object.Instantiate(source.gameObject, holder.transform, false).transform;
            copy.gameObject.SetActive(true);
            // Reversed: a component that requires another goes first.
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if (!(behaviour is Graphic || behaviour is LayoutGroup || behaviour is ContentSizeFitter || behaviour is LayoutElement || behaviour is RectMask2D || behaviour is Mask))
                {
                    // A slot's size comes from its script; keep that size once the script is gone.
                    var size = behaviour is ILayoutElement e ? new Vector2(e.preferredWidth, e.preferredHeight) : Vector2.zero;
                    var target = behaviour.gameObject;
                    UnityEngine.Object.DestroyImmediate(behaviour);
                    if (size != Vector2.zero && !target.GetComponent<LayoutElement>()) { var keep = target.AddComponent<LayoutElement>(); keep.preferredWidth = size.x; keep.preferredHeight = size.y; }
                }
            foreach (var label in copy.GetComponentsInChildren<Transform>(true)) if (label && label.name == "Stock") UnityEngine.Object.DestroyImmediate(label.gameObject);
            // An item out of stock is faded in the palette; the copy shows the block itself.
            foreach (var faded in copy.GetComponentsInChildren<CanvasGroup>(true)) faded.alpha = 1;
            copy.anchorMin = Vector2.zero; copy.anchorMax = Vector2.one; copy.pivot = new Vector2(.5f, .5f);
            copy.offsetMin = copy.offsetMax = Vector2.zero; copy.localScale = Vector3.one;
            var group = holder.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            return (RectTransform)holder.transform;
        }
    }

    public sealed class DefenseActorView : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Renderer body;
        [SerializeField] Transform healthRoot;
        [SerializeField] Transform healthFill;
        [SerializeField] LineRenderer beam;
        [SerializeField, Tooltip("레이저가 나가는 곳(사격형: 포신 끝). 비우면 머리 위치")] Transform muzzle;
        MaterialPropertyBlock colors;
        // The rigged models (CharacterAnimation): robots idle and play "Attack" on each attack, the monster walks on its own.
        Animator animator;
        static readonly int AttackTrigger = Animator.StringToHash("Attack"), AttackState = Animator.StringToHash("Attack");
        // The attack clip plays faster when the robot attacks more often than the clip lasts (1.0 s melee clip, 0.8 s interval).
        float attackClip, attackInterval, pace = 1;
        Color baseColor;
        Vector3 fullHealthScale;
        float beamTime;
        // Pale copy behind the fill that lags after hits (DefenseHealthTrail).
        static readonly Color TrailColor = new Color(1, .93f, .8f);
        const float TrailDepth = .015f; // behind the fill, still in front of the backdrop
        readonly DefenseHealthTrail trail = new DefenseHealthTrail();
        Transform healthTrail;
        void Awake()
        {
            if (!head || !body || !healthRoot || !healthFill || !beam || !beam.sharedMaterial)
                throw new InvalidOperationException(name + ": ActorView Inspector 참조 누락");
            colors = new MaterialPropertyBlock(); baseColor = body.sharedMaterial.GetColor("_BaseColor");
            fullHealthScale = healthFill.localScale;
            healthTrail = Instantiate(healthFill, healthFill.parent); healthTrail.name = "Trail";
            healthTrail.localPosition = healthFill.localPosition + Vector3.forward * TrailDepth;
            var trailColors = new MaterialPropertyBlock(); trailColors.SetColor("_BaseColor", TrailColor); trailColors.SetColor("_Color", TrailColor);
            foreach (var renderer in healthTrail.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(trailColors);
            beam.enabled = false;
            animator = GetComponentInChildren<Animator>();
            if (animator) attackClip = animator.runtimeAnimatorController.animationClips.Where(c => c.name.Contains("Attack")).Select(c => c.length).DefaultIfEmpty(0).First();
        }
        public void SyncRobot(RobotState bot, Vector3 position, Quaternion viewRotation)
        {
            attackInterval = bot.Interval;
            Sync(position, bot.Health, bot.MaxHealth, false, false, bot.Buffs.Count > 0, viewRotation);
        }
        public void SyncEnemy(EnemyState enemy, double time, Vector3 position, Quaternion viewRotation)
        {
            // Faces the way it walks (the monster model has a walk cycle).
            var step = position - transform.position; step.y = 0;
            if (step.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(step);
            Sync(position, enemy.Health, enemy.Spec.health, enemy.Stunned(time), enemy.Speed(time) < enemy.Spec.speed, false, viewRotation);
        }
        // Animation speed follows the battle clock: 0 while paused, 2-3 at 2x/3x.
        public void SetPace(float pace) => this.pace = pace;
        void Sync(Vector3 position, float? health, float maximum, bool stunned, bool slowed, bool buffed, Quaternion viewRotation)
        {
            transform.position = position;
            healthRoot.gameObject.SetActive(health.HasValue);
            if (health.HasValue)
            {
                healthRoot.rotation = viewRotation;
                float ratio = maximum > 0 ? Mathf.Clamp01(health.Value / maximum) : 0;
                healthFill.localScale = new Vector3(fullHealthScale.x * ratio, fullHealthScale.y, fullHealthScale.z);
                healthTrail.localScale = new Vector3(fullHealthScale.x * trail.Step(ratio, Time.unscaledDeltaTime), fullHealthScale.y, fullHealthScale.z);
            }
            Color tint = stunned ? new Color(.8f,.4f,1) : slowed ? new Color(.2f,.8f,1) : buffed ? Color.Lerp(baseColor, Color.white, .35f) : baseColor;
            colors.SetColor("_BaseColor", tint); body.SetPropertyBlock(colors);
        }
        // Called on every attack (slash, launch, ranged shot).
        public void AimAt(Vector3 target)
        {
            if (animator) animator.SetTrigger(AttackTrigger);
            Face(target);
        }
        // Turns the head (and the model under it) toward a point, level; robots face the camera when placed.
        public void Face(Vector3 target)
        {
            Vector3 direction = target - head.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) head.rotation = Quaternion.LookRotation(direction);
        }
        public Vector3 Muzzle => muzzle ? muzzle.position : head.position;
        public void FireAt(Vector3 target)
        {
            AimAt(target);
            beam.useWorldSpace = true; beam.positionCount = 2;
            beam.SetPosition(0, Muzzle); beam.SetPosition(1, target + Vector3.up * .45f);
            beam.enabled = true; beamTime = .12f;
        }
        public void TickVisual(float delta)
        {
            if (animator)
            {
                bool attacking = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == AttackState || animator.GetNextAnimatorStateInfo(0).shortNameHash == AttackState;
                animator.speed = pace * (attacking && attackInterval > 0 ? Mathf.Max(1, attackClip / (attackInterval * .9f)) : 1);
            }
            beamTime -= delta;
            if (beamTime <= 0) beam.enabled = false;
        }
        public void ClearVisuals() { beamTime = 0; beam.enabled = false; }
    }
}
