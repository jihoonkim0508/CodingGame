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
        #if UNITY_EDITOR
        public void EditorApplyOptions(DefenseTestOptions options)
        {
            hitEffects.Clear(); hitEffects.Options=options;
            if (simulation != null) simulation.Setup.TestOptions=options;
        }
        public void EditorApplyBalance()
        {
            if(simulation==null)return;
            var setup=simulation.Setup;
            CopyRules(setup);
            var enemy=HarmlessEnemy();setup.Waves[0][0].Enemy=enemy;
            simulation.ApplyBalance(setup,Enumerable.Range(0,6).Select(i=>battle.RobotDefinition(i).stats.Copy()).ToArray(),new[]{enemy});
            projectileViews.Clear();hitEffects.Clear();foreach(var view in views.Values)view.ClearVisuals();RefreshStats();
        }
#endif
        EnemySpec HarmlessEnemy() => Harmless(battle.PreviewEnemy.stats);
        static EnemySpec Harmless(EnemySpec spec) { var enemy = spec.Copy(); enemy.damage = 0; return enemy; }
        // The preview runs the live battle's balance so the demo matches what the robot will do.
        void CopyRules(BattleSetup setup)
        {
            var live = battle.Simulation;
            setup.ActionProfiles = live.Setup.ActionProfiles.Select(p => p.Copy()).ToArray();
            setup.RulesConfig = live.Setup.RulesConfig; setup.Permissions = live.Setup.Permissions;
        }
        public DefenseSimulation PreviewSimulation => simulation;
        public bool IsShowing => selected != null && previewPanel.activeSelf;
        public void Restart() { if (IsShowing) ResetPreview(); }
        public void SetHitRangesVisible(bool visible) => hitEffects.SetHitRangesVisible(visible);
        void Start()
        {
            if (!hitEffects.IsConfigured) throw new InvalidOperationException("미리보기 피격 이펙트 프리팹을 Inspector에 연결하세요.");
            if (!cameraControl || !coinLabel || !projectilePrefab || !battle || !arena || !previewCamera || !previewImage || !previewPanel || !stats || !status || !effectRing)
                throw new InvalidOperationException("공격 미리보기 Inspector 참조를 연결하세요.");
            if (!baked) Bake();
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
            if (healthBars) healthBars.Flush();
        }
        void ResetPreview()
        {
            ClearActors(); source = selected.Program; emptyElapsed = 0; firstHitReported = false; firstHitCount = 0;
            sourceLevel = selected.Level;
            // 1-3's distance lesson on a straight road: one of the wave's own enemies at a time walks in from beyond the shooter's
            // range, so a shot fired on entry leaves it reloading while the enemy passes close, and a shot held until close fells it.
            bool approach = battle.Simulation.Setup.Stage == 1 && battle.Simulation.WaveIndex == 2;
            var lessonEnemy = approach ? battle.Simulation.Setup.Waves[2].FirstOrDefault()?.Enemy : null;
            var setup = new BattleSetup {
                TestOptions = battle.Simulation.Options,
                Min = new Point(-50,-50), Max = new Point(50,50), BaseHealth = 100,
                Routes = new[] { new Route(new[] {
                    approach ? new Point(Math.Max(7, selected.Range + 1), 0) : new Point(2,0),
                    new Point(-20,0) }, .75f) }, // narrow, so the robot at 1.8 is clear of it under the placement rule
                Waves = new[] { new[] { new SpawnGroup { Enemy = lessonEnemy != null ? Harmless(lessonEnemy) : HarmlessEnemy(),
                    Count = approach ? 1 : 3, Interval = 1.4f } } }
            };
            CopyRules(setup);
            simulation = new DefenseSimulation(setup);
            var point = new Point(0, selected.Spec.role == RobotRole.Tank ? 0 : 1.8f);
            var bot = simulation.Place(selected.Spec.Copy(), selected.DefinitionIndex, point, out var error);
            if (bot == null) throw new InvalidOperationException(error);
            bot.Level = selected.Level;
            if (bot.Health.HasValue) bot.Health = bot.MaxHealth;
            if (source != null) simulation.ApplyProgram(bot.Id, source.Name, source.CopyBlocks());
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
            text.text = "-" + e.Amount.ToString("0.#"); text.fontSize = 7;
            text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .82f, .3f);
            // The battle's floating numbers' look: Jalnan with the shared thin ink outline.
            if (UITheme.Display && UITheme.Label) { text.font = UITheme.Display; text.fontSharedMaterial = UITheme.Label; }
            else { if (damageFont) text.font = damageFont; text.fontStyle = FontStyles.Bold; text.outlineWidth = .25f; text.outlineColor = new Color32(40, 10, 0, 255); }
            text.rectTransform.sizeDelta = new Vector2(6, 2);
            text.transform.position = World(e.Position) + Vector3.up * 1.6f; text.transform.rotation = previewCamera.transform.rotation;
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
            if (sourceLevel != selected.Level) ResetPreview();
            else if (source != selected.Program)
            {
                source = selected.Program;
                var bot = simulation.Robots.FirstOrDefault();
                if (bot != null)
                {
                    bot.Program = source == null ? null : new DefenseProgram(source.Name, source.CopyBlocks());
                    bot.AutoExecute = false;
                }
            }
            // The demo calls the function again as soon as it ends, as if Play were pressed each time.
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
            if (battle.TutorialBlocksPreview) return;
            RefreshStats();
            if (!IsShowing) return;
            var rect = previewImage.rectTransform.rect;
            if (rect.width > 0 && rect.height > 0) previewCamera.aspect = rect.width / rect.height;
            float dt = battle.Simulation.Options.preview ? Time.unscaledDeltaTime : 0;
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
            }
            foreach (var e in simulation.Enemies)
            {
                if (!views.ContainsKey(e.Id)) AddView(e.Id, battle.PreviewEnemy.prefab);
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
            TickDamage(dt);
            RefreshStatus();
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
            else if (program.StepsLastTick == 0) state = "코드 대기";
            else switch (bot.LastResult)
            {
                case ActionResult.CoolingDown:
                    state = $"재사용 대기 · {Math.Max(0, bot.NextAction - simulation.Time):0.0}초"; break;
                case ActionResult.NoTarget: state = "대상이 사거리 밖 · 빗나감"; break;
                case ActionResult.Undecided: state = "호환 미정 명령"; break;
                default: state = $"코드 시연 · {bot.Executions}회"; break;
            }
            if (bot.Spec.role == RobotRole.Tank)
            {
                int blocked = simulation.Enemies.Count(e => e.Active && e.BlockedBy == bot.Id);
                string blocking = !bot.BlockingEnabled ? "저지 비활성" :
                    $"저지 {blocked}/{bot.Spec.blockCapacity}" + (blocked >= bot.Spec.blockCapacity ? " · 한도 도달" : "");
                state += "  |  " + blocking;
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
                (s.role == RobotRole.Buffer ? "" : $"\n{Stat("공격력")}{UpgradeValue(selected.Damage,selected.Damage/selected.LevelMultiplier*nextMultiplier)}") +
                $"\n{Stat("사거리")}<size=115%>{selected.Range:0.#}</size>\n{Stat("공격 간격")}<size=115%>{selected.Interval:0.##}초</size>\n" +
                (selected.Health.HasValue ? (selected.IsMaxLevel ? $"{Stat("체력")}<size=115%>{selected.Health:0}/{selected.MaxHealth:0}</size>" : $"{Stat("최대 체력")}{UpgradeValue(selected.MaxHealth,s.health*nextMultiplier)}") + $"\n{Stat("저지")}<size=115%>{s.blockCapacity}마리</size>" : $"{Stat("체력")}공격받지 않음") +
                (s.role == RobotRole.Bomber ? $"\n{Stat("폭발 반경")}<size=115%>{s.effectRadius:0.#}</size>" : s.role == RobotRole.Utility ? $"\n{Stat("감속")}<size=115%>{(1-s.slowMultiplier)*100:0}%</size>" :
                s.role == RobotRole.Buffer ? $"\n{Stat("버프 피해")}{UpgradeValue(1+(s.buffDamage-1)*selected.LevelMultiplier,1+(s.buffDamage-1)*nextMultiplier)}배" +
                    $"\n{Stat("버프 사거리")}{UpgradeValue(1+(s.buffRange-1)*selected.LevelMultiplier,1+(s.buffRange-1)*nextMultiplier)}배" +
                    $"\n{Stat("버프 간격")}{UpgradeValue(Math.Max(.01f,1-(1-s.buffInterval)*selected.LevelMultiplier),Math.Max(.01f,1-(1-s.buffInterval)*nextMultiplier))}배" : "");
            coinLabel.text = $"<color={UITheme.MutedHex}>코인</color>  <size=115%><color={UITheme.GoldHex}>{battle.Simulation.Coins:N0}</color></size>";
        }
        static string Stat(string name) => $"<color={UITheme.MutedHex}>{name}</color>  ";
        string UpgradeValue(float current, float upgraded) => selected.IsMaxLevel || Mathf.Approximately(current, upgraded)
            ? $"<size=115%>{current:0.#}</size>" : $"<size=115%>{current:0.#}</size> <color={UITheme.GoodHex}>→ {upgraded:0.#}</color>";
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
