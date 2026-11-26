using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense
{
    // 선택한 코드의 복사본을 시연합니다. 실제 전투의 인벤토리·보상·상태와 분리합니다.
    public sealed class DefenseAttackPreview : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] Transform arena;
        [SerializeField] Camera previewCamera;
        [SerializeField] RawImage previewImage;
        [SerializeField] GameObject previewPanel;
        [SerializeField] TMP_Text stats, status;
        [SerializeField] TMP_Text coinLabel;
        [SerializeField] DefenseRobotStatsCard statsCard;
        [SerializeField] DefensePreviewCameraControl cameraControl;
        [SerializeField] DefenseRangeRing effectRing;
        [SerializeField, HideInInspector] DefenseRangeRing rangeRing; // the robot's attack range, a runtime copy of effectRing so hit effects can't hide it
        [SerializeField] Transform projectilePrefab;
        [SerializeField] DefenseHitVisuals hitEffects = new DefenseHitVisuals();
        [SerializeField, Tooltip("Font for the damage numbers that pop over hit enemies in the preview.")] TMP_FontAsset damageFont;
        readonly DefenseProjectileVisuals projectileViews = new DefenseProjectileVisuals();
        readonly List<(TextMeshPro text, float age)> damagePopups = new List<(TextMeshPro, float)>();
        const float PopupSeconds = .9f;
        DefenseSimulation simulation;
        RobotState selected;
        DefenseProgram source;
        readonly Dictionary<int, DefenseActorView> views = new Dictionary<int, DefenseActorView>();
        [SerializeField, Tooltip("미리보기 그림 위 체력바 UI 층(Tools/Optimize/Unit Health Bars)")] DefenseHealthBars healthBars;
        RenderTexture previewTexture;
        float emptyElapsed;
        int sourceLevel;
        bool firstHitReported;
        int firstHitCount;
        // The shooter's aim (the user, 2026-11-19): a yellow crosshair over the enemy its last shot went at, pinching on each shot.
        RawImage crosshair;
        int aimed;
        float pinch = PinchSeconds;
        const float PinchSeconds = .2f;
        public int CrosshairTarget => crosshair && crosshair.gameObject.activeSelf ? aimed : 0;
        #if UNITY_EDITOR
        public void EditorApplyOptions(DefenseTestOptions options)
        {
            hitEffects.Clear(); hitEffects.Options=options;
            if (simulation != null) simulation.Setup.TestOptions=options;
        }
        // The live battle has the new numbers by now (robot specs, the wave's enemies): start the demo again from them.
        public void EditorApplyBalance() { if (simulation != null) ResetPreview(); }
#endif
        // The preview runs the live battle's balance so the demo matches what the robot will do.
        void CopyRules(BattleSetup setup)
        {
            var live = battle.Simulation;
            setup.ActionProfiles = live.Setup.ActionProfiles.Select(p => p.Copy()).ToArray();
            setup.RulesConfig = live.Setup.RulesConfig; setup.Permissions = live.Setup.Permissions;
        }
        public DefenseSimulation PreviewSimulation => simulation;
        public bool IsShowing => selected != null && previewPanel.activeSelf;
        // hold: seconds the restarted demo stands at its start (a lesson waits for the spotlight to reach the preview, or the
        // first shot fired before the player looked: 2-1 C02).
        public void Restart(float hold = 0) { if (IsShowing) { ResetPreview(); holdUntil = GameClock.Now + hold; } }
        float holdUntil;
        public void SetHitRangesVisible(bool visible) => hitEffects.SetHitRangesVisible(visible);
        void Start()
        {
            if (!hitEffects.IsConfigured) throw new InvalidOperationException("미리보기 피격 이펙트 프리팹을 Inspector에 연결하세요.");
            if (!cameraControl || !coinLabel || !projectilePrefab || !battle || !arena || !previewCamera || !previewImage || !previewPanel || !stats || !status || !effectRing)
                throw new InvalidOperationException("공격 미리보기 Inspector 참조를 연결하세요.");
            if (!baked) Bake();
            // An enemy near the picture's top edge had its bar drawn over the card above it: bars stay inside the picture.
            if (healthBars && !healthBars.GetComponent<RectMask2D>()) healthBars.gameObject.AddComponent<RectMask2D>();
            Close();
        }
        // The preview in a paper card like the editor's others, not a near-black frame, its top clear of the title above, and
        // the stats card in from the screen's corner, its bottom in line with the inventory's (play feedback 3).
        // Built once in the editor and saved with the scene or prefab (13차: the editor showed the old look until Play); at run
        // time only in one that was never baked.
        [SerializeField, HideInInspector] bool baked;
        public bool Baked => baked;
        public void Bake()
        {
            if (baked) return;
            rangeRing = Instantiate(effectRing, effectRing.transform.parent); rangeRing.name = "Preview range";
            Frame();
            baked = true;
        }
        void Frame()
        {
            var frame = (RectTransform)previewPanel.transform;
            if (previewPanel.TryGetComponent<Image>(out var back)) UITheme.Style(back, UITheme.Card, UITheme.Paper);
            frame.offsetMax += new Vector2(0, -14);
            foreach (var text in previewPanel.GetComponentsInChildren<TMP_Text>(true)) text.color = text == status ? UITheme.Ink : UITheme.Muted;
            var card = (RectTransform)stats.transform.parent;
            card.offsetMin = new Vector2(8, 46); card.offsetMax = new Vector2(-8, -8);
        }
        public void Open(RobotState robot)
        {
            HidePreview();
            selected = robot;
            RefreshStats();
            cameraControl.ResetView();
            if (!previewTexture)
            {
                // 2x MSAA: the preview is a 640x720 picture in a panel; 4x cost twice the memory and fill for no visible gain.
                previewTexture = new RenderTexture(640, 720, 24) { name = "Robot code preview", antiAliasing = 2 };
                previewCamera.targetTexture = previewTexture; previewImage.texture = previewTexture;
            }
            ResetPreview(); previewPanel.SetActive(true); previewCamera.enabled = true;
        }
        void HidePreview()
        {
            ClearActors(); simulation = null; source = null; if (rangeRing) rangeRing.Hide();
            previewPanel.SetActive(false); previewCamera.enabled = false;
        }
        void ClearActors()
        {
            projectileViews.Clear();
            hitEffects.Clear(); ClearDamage();
            foreach (var view in views.Values) Spare(view);
            views.Clear(); effectRing.Hide();
            aimed = 0; if (crosshair) crosshair.gameObject.SetActive(false);
            if (healthBars) healthBars.Flush();
        }
        void ResetPreview()
        {
            ClearActors(); source = selected.Program; emptyElapsed = 0; firstHitReported = false; firstHitCount = 0;
            sourceLevel = selected.Level;
            var live = battle.Simulation;
            // 2-3's distance lesson (the old 1-3's) on a straight road: one enemy at a time walks in from beyond the shooter's
            // range, so a shot fired on entry leaves it reloading while the enemy passes close, and a shot held until close fells it.
            bool approach = live.Setup.Stage == 2 && live.WaveIndex == 2;
            // One normal enemy for the shooter and for the break lessons (2-6, 2-7): in a crowd the player could not tell which
            // enemy the code picked (the user, 2026-11-19). It starts in the picture, as the crowd did (the shooter's range runs
            // past the picture's edge, so one walking in from beyond it was shot out of sight). 2-5 and 2-8 keep theirs: they
            // teach the next enemy and the enemy's kind.
            int wave = live.WaveIndex + 1; bool stageTwo = live.Setup.Stage == 2;
            bool breakLesson = stageTwo && (wave == 6 || wave == 7);
            bool single = breakLesson || selected.Spec.role == RobotRole.Ranged && !(stageTwo && (wave == 5 || wave == 8));
            // 2-6's is the wave's fast one, which outlives the range (what break is for); a normal one fell inside it.
            var kind = stageTwo && wave == 6 ? EnemyType.Fast : EnemyType.Normal;
            IReadOnlyList<SpawnGroup> groups = live.CurrentWave;
            if (single && groups.Any(g => g.Enemy.type == kind)) groups = groups.Where(g => g.Enemy.type == kind).ToArray();
            var demo = DefenseSimulation.PreviewWave(groups, approach || single ? 1 : 3);
            // The break lessons send a second one as the first is about to leave the range (8 along the road from the start, the
            // robot 1.8 off it): code that keeps aiming at the first lets the second walk past, break lets the first go and
            // shoots the second (the user, 2026-11-19: the preview did not show what break does).
            if (breakLesson) { demo[0].Count = 2; demo[0].Interval = Math.Max(1, 2 + (float)Math.Sqrt(Math.Max(0, selected.Range * selected.Range - 1.8f * 1.8f)) - 1) / Math.Max(.1f, demo[0].Enemy.speed); }
            float approachStart = Math.Max(7, selected.Range + 1);
            // 2-3's warrior: the enemy timed so `while True: slash()` lets it through and the if fells it (see TimeMeleeDemo).
            if (approach && selected.Spec.role == RobotRole.Melee)
                DefenseSimulation.TimeMeleeDemo(demo[0], selected.Range, 1.8f, approachStart, live.ActionInterval(selected, RobotAction.Slash),
                    live.ActionInterval(selected, RobotAction.Slash) * (float)selected.Spec.strikeShare, live.ActionDamage(selected, RobotAction.Slash));
            var setup = new BattleSetup {
                TestOptions = live.Options,
                Min = new Point(-50,-50), Max = new Point(50,50), BaseHealth = 100,
                Routes = new[] { new Route(new[] {
                    approach ? new Point(approachStart, 0) : new Point(2,0),
                    new Point(-20,0) }, .75f) }, // narrow, so the robot at 1.8 is clear of it under the placement rule
                Waves = new[] { demo }
            };
            CopyRules(setup);
            simulation = new DefenseSimulation(setup);
            var point = new Point(0, 1.8f);
            var bot = simulation.Place(selected.Spec.Copy(), selected.DefinitionIndex, point, out var error);
            if (bot == null) throw new InvalidOperationException(error);
            bot.Level = selected.Level;
            bot.Health = selected.Health; // a robot hurt in an earlier wave starts hurt here too
            // The team board's names too (jobs = []): a robot's code that uses them compiles and runs against the same lines.
            // A robot whose code no longer compiles (a board name it used is gone) stands still in the preview.
            try { simulation.ApplyBoard(live.Board.Blocks); if (source != null) simulation.ApplyProgram(bot.Id, source.Name, source.CopyBlocks()); }
            catch (FormatException) { }
            AddView(bot.Id, battle.RobotDefinition(bot.DefinitionIndex).prefab);
            views[bot.Id].Face(previewCamera.transform.position);
            rangeRing.Show(World(bot.Position), bot.Range, battle.RobotDefinition(bot.DefinitionIndex).color);
            simulation.Start();
        }
        // The preview restarts on every code change and made and destroyed its robot and enemies each time; their views now
        // wait switched off for the next one, a stack per prefab.
        readonly Dictionary<DefenseActorView, Stack<DefenseActorView>> spareViews = new Dictionary<DefenseActorView, Stack<DefenseActorView>>();
        readonly Dictionary<DefenseActorView, DefenseActorView> viewPrefab = new Dictionary<DefenseActorView, DefenseActorView>();
        readonly HashSet<int> liveIds = new HashSet<int>();
        readonly List<int> goneIds = new List<int>();
        void AddView(int id, DefenseActorView prefab)
        {
            DefenseActorView view;
            if (spareViews.TryGetValue(prefab, out var spare) && spare.Count > 0) { view = spare.Pop(); view.gameObject.SetActive(true); view.Recycle(); }
            else { view = Instantiate(prefab, arena); viewPrefab[view] = prefab; }
            views.Add(id, view);
        }
        void Spare(DefenseActorView view)
        {
            if (!view) return;
            view.gameObject.SetActive(false);
            var prefab = viewPrefab[view];
            if (!spareViews.TryGetValue(prefab, out var spare)) spareViews[prefab] = spare = new Stack<DefenseActorView>();
            spare.Push(view);
        }
        Vector3 World(Point p) => arena.position + DefenseViews.World(p);
        // Floating "-N" over the hit enemy so the code's damage value is visible, not just inferred from a health bar.
        // A number that has faded waits switched off for the next hit (13차 133); only the first few are ever made.
        readonly Stack<TextMeshPro> spareDamage = new Stack<TextMeshPro>();
        void PopDamage(CombatEvent e)
        {
            TextMeshPro text = null;
            while (spareDamage.Count > 0 && !text) text = spareDamage.Pop();
            if (text) text.gameObject.SetActive(true);
            else
            {
                text = new GameObject("Damage", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                text.gameObject.layer = arena.gameObject.layer; text.transform.SetParent(arena, true);
            }
            text.alpha = 1; text.transform.localScale = Vector3.one;
            text.text = "-" + e.Amount.ToString("0.##"); text.fontSize = 7;
            text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .82f, .3f);
            // The battle's floating numbers' look: Jalnan with the shared thin ink outline.
            if (UITheme.Display && UITheme.Label) { text.font = UITheme.Display; text.fontSharedMaterial = UITheme.Label; }
            else { if (damageFont) text.font = damageFont; text.fontStyle = FontStyles.Bold; text.outlineWidth = .25f; text.outlineColor = new Color32(40, 10, 0, 255); }
            text.rectTransform.sizeDelta = new Vector2(6, 2);
            text.transform.position = World(e.Position) + Vector3.up * 1.6f + DefenseHitVisuals.Scatter(); text.transform.rotation = previewCamera.transform.rotation;
            damagePopups.Add((text, 0));
        }
        void TickDamage(float dt)
        {
            for (int i = damagePopups.Count - 1; i >= 0; i--)
            {
                var (text, age) = damagePopups[i]; age += dt;
                if (!text || age >= PopupSeconds) { Spare(text); damagePopups.RemoveAt(i); continue; }
                float t = age / PopupSeconds;
                text.transform.position += Vector3.up * dt * 1.4f;
                text.transform.localScale = Vector3.one * (t < .15f ? Mathf.Lerp(1.6f, 1, t / .15f) : 1);
                text.alpha = 1 - Mathf.Clamp01((t - .6f) / .4f);
                damagePopups[i] = (text, age);
            }
        }
        void ClearDamage() { foreach (var (text, _) in damagePopups) Spare(text); damagePopups.Clear(); }
        void Spare(TextMeshPro text) { if (text) { text.gameObject.SetActive(false); spareDamage.Push(text); } }
        void AdvancePreview(float dt)
        {
            // A new level or new code starts the demo over with a clean robot. The function is called again whenever it ends (the
            // user's choice, 2026-11-19), so the preview keeps showing what the code does instead of standing still.
            if (sourceLevel != selected.Level || source != selected.Program) ResetPreview();
            if (simulation.Robots.FirstOrDefault() is RobotState demo) simulation.Invoke(demo.Id);
            bool wasOver = DemoOver;
            simulation.Advance(dt);
            if (!DemoOver) { emptyElapsed = 0; return; }
            // 때릴 수 있는 적이 없어진 뒤 실제 시간으로 1초를 기다립니다. 전투 시계가 멈춰도 재시작합니다.
            if (wasOver) emptyElapsed += dt;
            if (emptyElapsed + .000001f >= 1) ResetPreview();
        }
        // Nothing left to hit: every enemy has spawned and none is in range or still ahead of the robot
        // (the demo route runs from +x to -x, so an enemy past the robot's x can never come back into range).
        bool DemoOver
        {
            get
            {
                var bot = simulation.Robots.FirstOrDefault();
                if (bot == null || simulation.RemainingEnemies == 0) return true;
                if (simulation.RemainingEnemies > simulation.Enemies.Count(e => e.Active)) return false;
                return !simulation.Enemies.Any(e => e.Active &&
                    (e.Position.X >= bot.Position.X || Point.DistanceSquared(e.Position, bot.Position) <= bot.Range * bot.Range));
            }
        }
        void Update()
        {
            if (selected == null) return;
            RefreshStats();
            if (!IsShowing) return;
            var rect = previewImage.rectTransform.rect;
            if (rect.width > 0 && rect.height > 0) previewCamera.aspect = rect.width / rect.height;
            // The demo's clock stands still while a lesson holds the preview and behind the Esc menu (GameClock); its robot
            // and enemies are still drawn where they stand (a new view starts at the arena's origin, on the road).
            float dt = battle.Simulation.Options.preview && !battle.TutorialBlocksPreview && GameClock.Now >= holdUntil ? GameClock.Delta : 0;
            AdvancePreview(dt);
            hitEffects.Tick(dt);
            if (simulation.Options.projectiles) projectileViews.Sync(simulation, projectilePrefab, arena, arena.position); else projectileViews.Clear();
            liveIds.Clear(); foreach (var r in simulation.Robots) liveIds.Add(r.Id); foreach (var e in simulation.Enemies) liveIds.Add(e.Id);
            goneIds.Clear(); foreach (var id in views.Keys) if (!liveIds.Contains(id)) goneIds.Add(id);
            foreach (var id in goneIds) { Spare(views[id]); views.Remove(id); }
            foreach (var bot in simulation.Robots)
            {
                var view = views[bot.Id];
                view.SyncRobot(bot, World(bot.Position), previewCamera.transform.rotation);
                if (healthBars && bot.Health.HasValue)
                    healthBars.Show(DefenseHealthBars.RobotKey(bot.Id), previewCamera, view.HealthAnchor, view.HealthWidth, bot.Health.Value, bot.MaxHealth);
                if (bot.Spec.role == RobotRole.Tank) view.ShowTank(simulation, bot, previewCamera, healthBars);
            }
            foreach (var e in simulation.Enemies)
            {
                if (!views.ContainsKey(e.Id)) AddView(e.Id, battle.EnemyAt(e.DefinitionIndex).prefab);
                var view = views[e.Id];
                view.SyncEnemy(e, simulation.Time, World(e.Position), previewCamera.transform.rotation);
                if (healthBars) healthBars.Show(e.Id, previewCamera, view.HealthAnchor, view.HealthWidth, e.Health, e.Spec.health);
            }
            if (healthBars) healthBars.Flush();
            foreach (var view in views.Values) view.TickVisual(dt);
            foreach (var e in simulation.Events)
            {
                views.TryGetValue(e.Source, out var view);
                #if UNITY_EDITOR
                battle.EditorObserve("Preview",simulation,e);
#endif
                hitEffects.Show(e, arena, arena.position, previewCamera.transform.rotation, view, effectRing);
                if (e.Kind == "shot") { aimed = e.Target; pinch = 0; }
                if ((e.Kind == "hit" || e.Kind == "projectile-hit") && e.Amount > 0)
                { PopDamage(e); battle.ReportTutorialAction(TutorialKeys.Preview, TutorialSignals.PreviewHit); }
            }
            if (!firstHitReported)
            {
                var firstHits = simulation.Events.Where(e => e.Kind == "hit" && e.Amount > 0).Select(e => e.Target).Distinct().Count();
                if (firstHits > 0)
                {
                    firstHitReported = true;
                    firstHitCount = firstHits;
                    battle.ReportTutorialAction(TutorialKeys.Preview,
                        firstHits > 1 ? TutorialSignals.PreviewMultiHit : TutorialSignals.PreviewSingleHit);
                }
            }
            simulation.Events.Clear();
            ShowCrosshair(dt);
            TickDamage(dt);
            RefreshStatus();
        }
        void ShowCrosshair(float dt)
        {
            EnemyState enemy = null;
            if (aimed != 0) foreach (var e in simulation.Enemies) if (e.Id == aimed && e.Active) enemy = e;
            if (enemy == null || !views.TryGetValue(aimed, out var view)) { aimed = 0; if (crosshair) crosshair.gameObject.SetActive(false); return; }
            if (!crosshair)
            {
                crosshair = new GameObject("Preview crosshair", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                // On the bars' layer, which keeps them inside the picture, so one on an enemy walking out is cut at the edge too.
                crosshair.rectTransform.SetParent(healthBars ? healthBars.transform : previewImage.rectTransform, false);
                crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax = Vector2.zero;
                crosshair.texture = CrosshairTexture(); crosshair.raycastTarget = false;
            }
            crosshair.gameObject.SetActive(true);
            // Over the enemy's middle, the ring about its width (the texture's ring is .6 of its size), as the health bar is placed.
            var area = ((RectTransform)crosshair.rectTransform.parent).rect;
            var center = (view.transform.position + view.HealthAnchor) * .5f;
            Vector3 at = previewCamera.WorldToViewportPoint(center);
            Vector3 edge = previewCamera.WorldToViewportPoint(center + previewCamera.transform.right * (view.HealthWidth * .5f));
            pinch += dt;
            float scale = pinch < PinchSeconds ? 1 - .2f * Mathf.Sin(Mathf.PI * pinch / PinchSeconds) : 1;
            float size = Mathf.Abs(edge.x - at.x) * area.width * 2 / .6f * scale;
            crosshair.rectTransform.anchoredPosition = new Vector2(at.x * area.width, at.y * area.height);
            crosshair.rectTransform.sizeDelta = new Vector2(size, size);
        }
        static Texture2D crosshairTexture;
        // Kenney's thin crosshair: a ring, four ticks across it and a centre dot, yellow with an ink outline dropped down-right.
        static Texture2D CrosshairTexture()
        {
            if (crosshairTexture) return crosshairTexture;
            const int n = 128; crosshairTexture = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
            const float pixel = 2f / n;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1;
                float yellow = Mathf.Clamp01(.5f - Shape(dx, dy) / pixel);
                float ink = Mathf.Clamp01(.5f - (Shape(dx - .025f, dy + .035f) - .045f) / pixel);
                float a = yellow + ink * (1 - yellow);
                var c = a > 0 ? (UITheme.Sunny * yellow + UITheme.Ink * ink * (1 - yellow)) / a : Color.clear;
                c.a = a; crosshairTexture.SetPixel(x, y, c);
            }
            crosshairTexture.Apply();
            return crosshairTexture;
        }
        // Distance outside the crosshair's shape (negative inside), the texture spanning -1..1.
        static float Shape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y), ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float ring = Mathf.Abs(r - .6f) - .045f, dot = r - .11f;
            float across = Mathf.Max(Mathf.Abs(ax - .6f) - .2f, ay - .045f), down = Mathf.Max(Mathf.Abs(ay - .6f) - .2f, ax - .045f);
            return Mathf.Min(Mathf.Min(ring, dot), Mathf.Min(across, down));
        }
        void RefreshStatus()
        {
            if (DemoOver) { status.text = $"시연 완료 · {Math.Max(0,1-emptyElapsed):0.0}초 뒤 다시 시작"; return; }
            var bot = simulation.Robots.FirstOrDefault();
            if (bot == null) { status.text = "시연 종료"; return; }
            var program = bot.Program;
            string state;
            if (program == null || program.IsEmpty) state = "코드 없음 · 대기";
            else if (program.Fault != null) state = program.Fault;
            // Explain a wasted command before "completed", or a one-run function hides why nothing happened.
            else if (bot.LastResult == ActionResult.Ineffective) state = "호환되지 않는 명령 · 이 로봇에서는 동작하지 않아요";
            else if (program.WaitRemaining(simulation.Time) is double wait && wait > 0) state = $"wait() · {wait:0.0}초 기다리는 중";
            // The function has ended and, as in a wave, is not called again: say so, or the robot just seems to stop.
            else if (!program.Running) state = "함수 끝 · " + (bot.LastResult == ActionResult.NoTarget ? "대상이 사거리 밖 · 빗나감" : $"명령 {bot.Executions}회 실행");
            else switch (bot.LastResult)
            {
                case ActionResult.CoolingDown:
                    state = $"재사용 대기 · {Math.Max(0, bot.NextAction - simulation.Time):0.0}초"; break;
                case ActionResult.NoTarget: state = "대상이 사거리 밖 · 빗나감"; break;
                case ActionResult.Undecided: state = "호환 미정 명령"; break;
                default: state = $"코드 시연 · {bot.Executions}회"; break;
            }
            status.text = state;
        }
        void RefreshStats()
        {
            if(statsCard){statsCard.Show(battle,selected);return;}
            var s = selected.Spec;
            float nextMultiplier = (float)Math.Pow(selected.UpgradeFactor, selected.Level);
            // Dim names, bright values 15% larger: the same reading pattern as the battle's top bar (not <b>: the text is already Pretendard Bold).
            stats.text = $"<size=115%>{battle.RobotName(selected.Id)}  Lv.{selected.Level}</size>" + (selected.IsMaxLevel ? "" : $" <color={UITheme.GoodHex}>→ Lv.{selected.Level + 1}</color>") +
                $"\n{Stat("공격력")}{UpgradeValue(selected.Damage,selected.Damage/selected.LevelMultiplier*nextMultiplier)}" +
                $"\n{Stat("사거리")}<size=115%>{selected.Range:0.#}</size>\n{Stat("공격 간격")}<size=115%>{selected.Interval:0.##}초</size>\n" +
                (selected.IsMaxLevel ? $"{Stat("체력")}<size=115%>{selected.Health:0}/{selected.MaxHealth:0}</size>" : $"{Stat("최대 체력")}{UpgradeValue(selected.MaxHealth,s.health*nextMultiplier)}") +
                $"\n{Stat("계열")}{DefenseRobotStatsCard.ClassNote(s.role, battle.Simulation.Setup.RulesConfig)}" +
                (s.role == RobotRole.Utility ? $"\n{Stat("감속")}<size=115%>속도 {s.slowMultiplier:0%} · {s.duration:0.#}초</size>" : "") +
                (s.role == RobotRole.Tank ? $"\n{Stat("가드")}{UpgradeValue(selected.MaxGuard,s.guard*nextMultiplier)}  {Stat("길막기")}<size=115%>{s.blockRadius:0.#}</size>" : "");
            coinLabel.text = $"<color={UITheme.MutedHex}>코인</color>  <size=115%><color={UITheme.GoldHex}>{battle.Simulation.Coins:N0}</color></size>";
        }
        static string Stat(string name) => $"<color={UITheme.MutedHex}>{name}</color>  ";
        string UpgradeValue(float current, float upgraded) => selected.IsMaxLevel || Mathf.Approximately(current, upgraded)
            ? $"<size=115%>{current:0.##}</size>" : $"<size=115%>{current:0.##}</size> <color={UITheme.GoodHex}>→ {upgraded:0.##}</color>";
        public void Close()
        {
            selected = null; HidePreview();
        }
        void OnDestroy()
        {
            if (previewTexture) { previewTexture.Release(); Destroy(previewTexture); }
        }
    }
}
