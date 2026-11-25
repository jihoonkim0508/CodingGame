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
        // The 2-4 / 3-4 monsters (SupportMonsterArt): the part that swells on each heal/shield cast (healer's cross, shielder's
        // dome); a centipede's head/body/tail models or a pile's walking base and its layer colours, one shown per segment;
        // how high a pile's layers above the base sit; the Resources/Effects/Monsters effect played where a segment ends.
        [SerializeField] Transform pulsePart;
        [SerializeField] GameObject[] parts = Array.Empty<GameObject>();
        [SerializeField] float pileBase, pileStep;
        [SerializeField] string deathEffect = "";
        // The hovering body bobs on the battle clock (still while paused), each enemy at its own phase.
        float hoverY, hoverTime;
        const float HoverHeight = .06f, HoverHz = 1.1f;
        MaterialPropertyBlock colors;
        // The rigged models (CharacterAnimation): robots idle and play "Attack" on each attack, the monster walks on its own.
        Animator animator;
        static readonly int AttackTrigger = Animator.StringToHash("Attack"), AttackState = Animator.StringToHash("Attack");
        // The attack clip plays faster when the robot attacks more often than the clip lasts (0.8 s ranged clip);
        // a slash plays it so the sword passes straight ahead when the simulation's strike lands.
        float attackClip, attackInterval, strikeSpeed, pace = 1, walkRate = 1;
        // The tanker's walk (MonsterTankerWalk.anim, baked by TankerWalk) keeps its feet planted at this speed in world units a
        // second at Heavy's scale 1.3; the view plays it faster or slower with the enemy's own speed.
        public const float TankerWalkSpeed = 1.6f;
        // Melee_Attack (1 s): the sampled sword sweeps clockwise from the left (-64° at .40, -10° at .45, +22° at .50).
        const float StrikeAt = .46f;
        Color baseColor, materialColor;
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
            colors = new MaterialPropertyBlock(); materialColor = baseColor = body.sharedMaterial.GetColor("_BaseColor");
            healthRoot.position += Vector3.up * HealthLift; // clear of the head (9차 100: the bar sat on it)
            beam.enabled = false;
            animator = GetComponentInChildren<Animator>();
            partScales = parts.Select(p => p.transform.localScale).ToArray();
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
            // The tanker's planted feet stay put at any speed (slowed, 1.0-1.95 by stage, the boss 1.65 times as big) and stand
            // still while it is stunned or blocked.
            if (enemy.Spec.type == EnemyType.Tank)
                walkRate = enemy.BlockedBy != 0 || enemy.Stunned(time) ? 0 : enemy.Speed(time) / (TankerWalkSpeed * transform.lossyScale.z / 1.3f);
            baseColor = materialColor;
            sinceCast = (float)(time - enemy.LastAbility);
            if (bornAt < 0) bornAt = time;
            float lift = 0;
            if (enemy.Chain != null)
            {
                int at = ShowPart(enemy);
                float fall = Mathf.Clamp01((float)(time - bornAt) / .3f);
                if (enemy.Chain.Stack && at > 0)
                {
                    // A layer falls onto the pile from above, faster and faster, lands with a thump (squashed flat for a
                    // moment, a dust ring) and sits pileStep over the one below; higher layers a little narrower.
                    lift = pileBase + (at - 1) * pileStep + 1.8f * (1 - fall * fall);
                    if (fall >= 1 && !landed) { landed = true; Burst("StackPush", transform.position); }
                    float since = (float)(time - bornAt) - .3f, thump = since > 0 && since < .22f ? Mathf.Sin(Mathf.PI * since / .22f) : 0;
                    shape = new Vector3(1 + .18f * thump, 1 - .32f * thump, 1 + .18f * thump);
                    shape.x = shape.z *= 1 - .035f * (at - 1);
                    popScale = 1;
                }
                // A centipede segment (and a pile's walking base) pops in.
                else popScale = Mathf.Min(1, .3f + .7f * Mathf.Sin(Mathf.PI * .5f * fall) + .15f * Mathf.Sin(Mathf.PI * fall));
            }
            position += Vector3.up * lift;
            ShowBubble(enemy.Shield > 0);
            bool open = enemy.Chain != null && enemy.Chain.Hittable == enemy;
            ShowsHealth = enemy.Chain == null || !enemy.Chain.Stack || open; // a pile shows one bar, its top layer's
            ShowOpenEnd(open, open ? enemy.Chain.Segments.Count : 0, viewRotation, (float)time);
            Sync(position, enemy.Stunned(time), enemy.Speed(time) < enemy.Spec.speed, false);
        }
        public bool ShowsHealth { get; private set; } = true;
        double bornAt = -1; bool landed; float popScale = 1, sinceCast = float.PositiveInfinity;
        Vector3 shape = Vector3.one; // a pile layer's squash on landing and its taper
        int part = -1;
        // Which model a segment shows: a queue's head (front), tail (last) or body; a pile's walking base or a layer colour. Each starts its walk at its own phase, so a centipede ripples and a pile sways.
        int ShowPart(EnemyState enemy)
        {
            int at = enemy.Chain.Segments.IndexOf(enemy);
            if (parts.Length < 2 || at < 0) return Math.Max(0, at);
            int want = enemy.Chain.Stack ? (at == 0 ? 0 : 1 + (at - 1) % (parts.Length - 1)) // the colours alternate up the pile
                : at == 0 ? 0 : at == enemy.Chain.Segments.Count - 1 ? 2 : 1;
            if (want == part) return at;
            part = want;
            for (int i = 0; i < parts.Length; i++) parts[i].SetActive(i == want);
            body = parts[want].GetComponentInChildren<Renderer>(); shownTint = new Color(-1, -1, -1, -1);
            animator = parts[want].GetComponent<Animator>();
            if (animator) animator.Play(0, 0, enemy.Id * .37f % 1);
            // A pile's layers sit a little askew, alternately, so their edges show.
            if (enemy.Chain.Stack && at > 0) parts[want].transform.localRotation = Quaternion.Euler(0, 180 + (at % 2 == 0 ? 4 : -4), 0);
            return at;
        }
        static readonly System.Collections.Generic.Dictionary<string, GameObject> effects = new System.Collections.Generic.Dictionary<string, GameObject>();
        internal static GameObject Effect(string name)
        {
            if (!effects.TryGetValue(name, out var effect)) effects[name] = effect = Resources.Load<GameObject>("Effects/Monsters/" + name);
            return effect;
        }
        // A one-off effect where this actor is, left in the world (it outlives the actor) and gone after 2 s.
        void Burst(string name, Vector3 at)
        {
            var prefab = Effect(name); if (!prefab) return;
            var effect = Instantiate(prefab, at, Quaternion.identity, transform.parent);
            foreach (var p in effect.GetComponentsInChildren<ParticleSystem>()) { var main = p.main; main.simulationSpeed = Mathf.Max(pace, .01f); }
            var system = effect.GetComponent<ParticleSystem>(); if (system) system.Play(true);
            Destroy(effect, 2);
        }
        // The segment's end: a pop where it was (its view goes with it).
        public void Die() { if (!string.IsNullOrEmpty(deathEffect)) Burst(deathEffect, body.bounds.center); }
        GameObject bubble, marker;
        TMPro.TextMeshPro segmentCount;
        void ShowBubble(bool shown)
        {
            if (!shown)
            {
                // Broken (break_shield, or used up by damage): it shatters.
                if (bubble && bubble.activeSelf) { bubble.SetActive(false); Burst("ShieldShatter", bubble.transform.position); }
                return;
            }
            if (!bubble)
            {
                bubble = Instantiate(Effect("ShieldBubble"), transform);
                // Round the model as it stands (its renderer's bounds), a little larger than it.
                var bounds = body.bounds;
                bubble.transform.position = bounds.center;
                bubble.transform.localScale = Vector3.one * (2.3f * Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) / Mathf.Max(.01f, transform.lossyScale.x));
            }
            bubble.SetActive(true);
        }
        // The one segment that takes damage: a yellow arrow bobbing over it, a glowing ring at its feet, the segment count over
        // the arrow, and the segment a little bigger.
        void ShowOpenEnd(bool shown, int count, Quaternion viewRotation, float time)
        {
            if (!shown) { if (marker) { marker.SetActive(false); segmentCount.enabled = false; } return; }
            if (!marker)
            {
                marker = Instantiate(Effect("OpenEndMarker"), transform);
                marker.transform.Find("Arrow").localScale = Vector3.one * 1.5f;
                marker.transform.localScale = Vector3.one / Mathf.Max(.01f, transform.lossyScale.x);
                segmentCount = new GameObject("Segment count", typeof(TMPro.TextMeshPro)).GetComponent<TMPro.TextMeshPro>();
                segmentCount.gameObject.layer = gameObject.layer; segmentCount.transform.SetParent(transform, false);
                segmentCount.transform.localScale = Vector3.one / Mathf.Max(.01f, transform.lossyScale.x);
                if (UITheme.Display && UITheme.Label) { segmentCount.font = UITheme.Display; segmentCount.fontSharedMaterial = UITheme.Label; }
                segmentCount.fontSize = 5; segmentCount.alignment = TMPro.TextAlignmentOptions.Center; segmentCount.color = new Color(1, .93f, .45f);
                segmentCount.rectTransform.sizeDelta = new Vector2(4, 1.5f);
            }
            marker.SetActive(true); segmentCount.enabled = true;
            var arrow = marker.transform.Find("Arrow");
            arrow.position = healthRoot.position + Vector3.up * (.35f + .12f * Mathf.Abs(Mathf.Sin(time * 4)));
            arrow.rotation = Quaternion.Euler(0, time * 90, 0);
            marker.transform.Find("Ring").localScale = Vector3.one * (2.2f + .25f * Mathf.Sin(time * 8));
            segmentCount.text = count.ToString();
            segmentCount.transform.SetPositionAndRotation(arrow.position + Vector3.up * .85f, viewRotation);
        }
        void LateUpdate()
        {
            // After the walk clip has posed the bones: the cast pulse swells its part, a new segment pops in, the open end stands bigger.
            if (pulsePart && sinceCast < .5f) pulsePart.localScale *= 1 + .7f * Mathf.Sin(Mathf.PI * sinceCast / .5f);
            if (parts.Length > 0 && part >= 0) parts[part].transform.localScale = Vector3.Scale(partScales[part], shape) * popScale * (marker && marker.activeSelf ? 1.12f : 1);
        }
        Vector3[] partScales;
        // Animation speed follows the battle clock: 0 while paused, 2-3 at 2x/3x.
        public float Pace => pace;
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
                animator.speed = pace * (attacking ? AttackSpeed : walkRate);
            }
            if (spinSeconds > 0)
            {
                spinTime += delta; // delta is already the battle clock's (scaled by battle speed)
                float k = Mathf.Clamp01(spinTime / spinSeconds);
                head.rotation = spinFrom * Quaternion.Euler(0, 360 * Mathf.SmoothStep(0, 1, k), 0);
                if (k >= 1) spinSeconds = 0;
            }
            if (hover)
            {
                hoverTime += delta;
                var at = hover.localPosition; at.y = hoverY + HoverHeight * Mathf.Sin(hoverTime * HoverHz * 2 * Mathf.PI); hover.localPosition = at;
            }
            beamTime -= delta;
            if (beamTime <= 0) beam.enabled = false;
        }
        // A wave ending mid-spin would leave the model turned: the clock stops with the wave, so put it back at its facing.
        public void ClearVisuals()
        {
            beamTime = 0; beam.enabled = false;
            if (spinSeconds > 0) { head.rotation = spinFrom; spinSeconds = 0; }
        }
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
