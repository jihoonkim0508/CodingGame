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
            if(source.GetComponent<CodingGame.BlockCoding.CommandBlockView>())return BlockCopy(source,parent);
            var holder = new GameObject(source.name + " (copy)", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(parent, false);
            var copy = (RectTransform)UnityEngine.Object.Instantiate(source.gameObject, holder.transform, false).transform;
            copy.gameObject.SetActive(true);
            // Reversed: a component that requires another goes first.
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if (!(behaviour is Graphic || behaviour is BaseMeshEffect || behaviour is LayoutGroup || behaviour is ContentSizeFitter || behaviour is LayoutElement || behaviour is RectMask2D || behaviour is Mask))
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
        static RectTransform BlockCopy(RectTransform source,Transform parent)
        {
            var holder=new GameObject(source.name+" (preview)",typeof(RectTransform),typeof(CanvasGroup));holder.SetActive(false);holder.transform.SetParent(parent,false);
            var rect=(RectTransform)holder.transform;rect.sizeDelta=new Vector2(Mathf.Max(100,source.rect.width),Mathf.Max(38,source.rect.height));
            var copy=(RectTransform)UnityEngine.Object.Instantiate(source.gameObject,rect,false).transform;copy.gameObject.SetActive(true);
            copy.anchorMin=Vector2.zero;copy.anchorMax=Vector2.one;copy.offsetMin=copy.offsetMax=Vector2.zero;
            foreach(var block in copy.GetComponentsInChildren<CodingGame.BlockCoding.CommandBlockView>(true))block.PreparePreview();
            var group=holder.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            return rect;
        }
    }

    public sealed class DefenseActorView : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Renderer body;
        // Where the actor's health bar stands (a UI bar on DefenseHealthBars, 13차; the prefab's cubes are gone).
        [SerializeField] Transform healthRoot;
        [SerializeField] LineRenderer beam;
        [SerializeField, Tooltip("레이저가 나가는 곳(사격형: 포신 끝). 비우면 머리 위치")] Transform muzzle;
        [SerializeField, Tooltip("떠서 나는 몸(이속형: 모델과 발 불꽃, Tools/Develop/Runner Thrusters). 비우면 뜨지 않음")] Transform hover;
        [SerializeField] ParticleSystem[] thrusters = Array.Empty<ParticleSystem>();
        // The hovering body bobs on the battle clock (still while paused), each enemy at its own phase.
        float hoverY, hoverTime;
        const float HoverHeight = .06f, HoverHz = 1.1f;
        MaterialPropertyBlock colors;
        // The rigged models (CharacterAnimation): robots idle and play "Attack" on each attack, the monster walks on its own.
        Animator animator;
        static readonly int AttackTrigger = Animator.StringToHash("Attack"), AttackState = Animator.StringToHash("Attack");
        // The attack clip plays faster when the robot attacks more often than the clip lasts (0.8 s ranged clip);
        // a slash plays it so the sword passes straight ahead when the simulation's strike lands.
        float attackClip, attackInterval, strikeSpeed, pace = 1;
        // Melee_Attack (1 s): the sampled sword sweeps clockwise from the left (-64° at .40, -10° at .45, +22° at .50).
        const float StrikeAt = .46f;
        Color baseColor;
        float beamTime;
        public const float HealthLift = .3f;
        public Vector3 HealthAnchor => healthRoot.position;
        // The bar's width in the world: one unit at the actor's own scale (Heavy 1.3, Runner 0.8), as the cubes were.
        public float HealthWidth => transform.lossyScale.x;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Color shownTint = new Color(-1, -1, -1, -1);
        void Awake()
        {
            if (!head || !body || !healthRoot || !beam || !beam.sharedMaterial)
                throw new InvalidOperationException(name + ": ActorView Inspector 참조 누락");
            colors = new MaterialPropertyBlock(); baseColor = body.sharedMaterial.GetColor("_BaseColor");
            healthRoot.position += Vector3.up * HealthLift; // clear of the head (9차 100: the bar sat on it)
            beam.enabled = false;
            animator = GetComponentInChildren<Animator>();
            if (hover) { hoverY = hover.localPosition.y; hoverTime = UnityEngine.Random.value * 10; }
            if (animator) attackClip = animator.runtimeAnimatorController.animationClips.Where(c => c.name.Contains("Attack")).Select(c => c.length).DefaultIfEmpty(0).First();
        }
        public void SyncRobot(RobotState bot, Vector3 position, Quaternion viewRotation)
        {
            attackInterval = bot.Interval;
            Sync(position, false, false, bot.Buffs.Count > 0);
        }
        public void SyncEnemy(EnemyState enemy, double time, Vector3 position, Quaternion viewRotation)
        {
            // Faces the way it walks (the monster model has a walk cycle).
            var step = position - transform.position; step.y = 0;
            if (step.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(step);
            Sync(position, enemy.Stunned(time), enemy.Speed(time) < enemy.Spec.speed, false);
        }
        // Animation speed follows the battle clock: 0 while paused, 2-3 at 2x/3x.
        public void SetPace(float pace)
        {
            if (pace == this.pace) return;
            this.pace = pace;
            foreach (var flame in thrusters) { var main = flame.main; main.simulationSpeed = pace; }
        }
        void Sync(Vector3 position, bool stunned, bool slowed, bool buffed)
        {
            transform.position = position;
            Color tint = stunned ? new Color(.8f,.4f,1) : slowed ? new Color(.2f,.8f,1) : buffed ? Color.Lerp(baseColor, Color.white, .35f) : baseColor;
            // Set only when it changes (13차 134): this ran for every actor every frame.
            if (tint == shownTint) return;
            shownTint = tint; colors.SetColor(BaseColorId, tint); body.SetPropertyBlock(colors);
        }
        // Called on every attack (slash, launch, ranged shot); strikeSeconds: a slash's time until its damage lands.
        public void AimAt(Vector3 target, float strikeSeconds = 0)
        {
            strikeSpeed = strikeSeconds > 0 ? StrikeAt * attackClip / strikeSeconds : 0;
            if (animator) animator.SetTrigger(AttackTrigger);
            Face(target);
        }
        // Turns the head (and the model under it) toward a point, level; robots face the camera when placed.
        public void Face(Vector3 target)
        {
            Vector3 direction = target - head.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) head.rotation = Quaternion.LookRotation(direction);
        }
        // slash() cuts all the way round: the robot plays its attack and turns one full circle, ending where it started
        // (facing the nearest enemy), the turn's middle when the strike lands.
        float spinTime, spinSeconds;
        Quaternion spinFrom;
        public void Spin(Vector3 target, float strikeSeconds)
        {
            AimAt(target, strikeSeconds);
            spinFrom = head.rotation; spinSeconds = Mathf.Max(.2f, strikeSeconds * 2); spinTime = 0;
        }
        public Vector3 Muzzle => muzzle ? muzzle.position : head.position;
        public bool HasMuzzle => muzzle;
        float AttackSpeed => strikeSpeed > 0 ? strikeSpeed : attackInterval > 0 ? Mathf.Max(1, attackClip / (attackInterval * .9f)) : 1;
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
                animator.speed = pace * (attacking ? AttackSpeed : 1);
            }
            if (spinSeconds > 0)
            {
                spinTime += delta * pace;
                float k = Mathf.Clamp01(spinTime / spinSeconds);
                head.rotation = spinFrom * Quaternion.Euler(0, 360 * Mathf.SmoothStep(0, 1, k), 0);
                if (k >= 1) spinSeconds = 0;
            }
            if (hover)
            {
                hoverTime += delta * pace;
                var at = hover.localPosition; at.y = hoverY + HoverHeight * Mathf.Sin(hoverTime * HoverHz * 2 * Mathf.PI); hover.localPosition = at;
            }
            beamTime -= delta;
            if (beamTime <= 0) beam.enabled = false;
        }
        public void ClearVisuals() { beamTime = 0; beam.enabled = false; }
        // Ready to stand for another actor (the code preview reuses its views): no beam, spin or strike under way, facing
        // ahead, the animator back at its first state.
        public void Recycle()
        {
            ClearVisuals(); spinSeconds = 0; strikeSpeed = 0;
            head.localRotation = Quaternion.identity;
            if (animator) { animator.Rebind(); animator.Update(0); }
        }
    }
}
