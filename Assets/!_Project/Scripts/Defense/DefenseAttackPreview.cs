using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense
{
    // An isolated copy of the selected program: no inventory, rewards or battle state is shared.
    public sealed class DefenseAttackPreview : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] Transform arena;
        [SerializeField] Camera previewCamera;
        [SerializeField] RawImage previewImage;
        [SerializeField] GameObject previewPanel;
        [SerializeField] Button previewButton;
        [SerializeField] TMP_Text previewButtonLabel;
        [SerializeField] TMP_Text stats, status;
        [SerializeField] Button upgradeButton;
        [SerializeField] TMP_Text upgradeLabel, upgradePrice, coinLabel;
        [SerializeField] DefenseRangeRing effectRing;
        [SerializeField] Transform projectilePrefab;
        readonly DefenseProjectileVisuals projectileViews = new DefenseProjectileVisuals();
        DefenseSimulation simulation;
        RobotState selected;
        DefenseProgram source;
        readonly Dictionary<int, DefenseActorView> views = new Dictionary<int, DefenseActorView>();
        RenderTexture previewTexture;
        float effectSeconds, emptyElapsed;
        int sourceLevel;
        public DefenseSimulation PreviewSimulation => simulation;
        public bool IsShowing => selected != null && previewPanel.activeSelf;
        void Start()
        {
            if (!upgradeButton || !upgradeLabel || !upgradePrice || !coinLabel || !projectilePrefab || !battle || !arena || !previewCamera || !previewImage || !previewPanel || !previewButton || !previewButtonLabel || !stats || !status || !effectRing)
                throw new InvalidOperationException("공격 미리보기 Inspector 참조를 연결하세요.");
            previewButton.onClick.AddListener(TogglePreview);
            upgradeButton.onClick.AddListener(UpgradeSelected);
            Close();
        }
        public void Open(RobotState robot)
        {
            HidePreview();
            selected = robot;
            RefreshStats();
        }
        public void TogglePreview()
        {
            if (selected == null) return;
            if (IsShowing) { HidePreview(); return; }
            if (!previewTexture)
            {
                previewTexture = new RenderTexture(960, 720, 24) { name = "Robot code preview" };
                previewCamera.targetTexture = previewTexture; previewImage.texture = previewTexture;
            }
            ResetPreview(); previewPanel.SetActive(true); previewCamera.enabled = true;
            previewButtonLabel.text = "코드로 돌아가기";
        }
        void UpgradeSelected()
        {
            if (selected == null || !battle.Simulation.UpgradeRobot(selected.Id)) return;
            if (IsShowing) ResetPreview();
            RefreshStats();
        }
        void HidePreview()
        {
            ClearActors(); simulation = null; source = null;
            previewPanel.SetActive(false); previewCamera.enabled = false;
            previewButtonLabel.text = "미리보기";
        }
        void ClearActors()
        {
            projectileViews.Clear();
            foreach (var view in views.Values) if (view) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            views.Clear(); effectRing.Hide();
        }
        void ResetPreview()
        {
            ClearActors(); source = selected.Program; emptyElapsed = 0; effectSeconds = 0;
            sourceLevel = selected.Level;
            var enemy = battle.PreviewEnemy.stats.Copy(); enemy.damage = 0;
            simulation = new DefenseSimulation(new BattleSetup {
                Min = new Point(-50,-50), Max = new Point(50,50), BaseHealth = 100,
                Routes = new[] { new Route(new[] { new Point(2,0), new Point(-20,0) }, 1.2f) },
                ActionProfiles = battle.Simulation.Setup.ActionProfiles.Select(p => p.Copy()).ToArray(),
                Permissions = battle.Simulation.Setup.Permissions,
                Waves = new[] { new[] { new SpawnGroup { Enemy = enemy, Count = 3, Interval = .7f } } }
            });
            var point = new Point(0, selected.Spec.role == RobotRole.Tank ? 0 : 1.8f);
            var bot = simulation.Place(selected.Spec.Copy(), selected.DefinitionIndex, point, out var error);
            if (bot == null) throw new InvalidOperationException(error);
            bot.Level = selected.Level;
            if (bot.Health.HasValue) bot.Health = bot.MaxHealth;
            bot.RangeSetting = selected.RangeSetting;
            if (source != null) simulation.ApplyProgram(bot.Id, source.Name, source.CopyBlocks());
            AddView(bot.Id, battle.RobotDefinition(bot.DefinitionIndex).prefab);
            simulation.Start();
        }
        void AddView(int id, DefenseActorView prefab)
        { var view = Instantiate(prefab, arena); view.Id = id; views.Add(id, view); }
        Vector3 World(Point p) => arena.position + new Vector3(p.X,0,p.Y);
        void AdvancePreview(float dt)
        {
            if (source != selected.Program || sourceLevel != selected.Level) ResetPreview();
            bool wasEmpty = simulation.RemainingEnemies == 0;
            simulation.Advance(dt);
            if (simulation.RemainingEnemies > 0) { emptyElapsed = 0; return; }
            // Count a full second after the last enemy disappears, including after the battle clock stops.
            if (wasEmpty) emptyElapsed += dt;
            if (emptyElapsed + .000001f >= 1) ResetPreview();
        }
        void Update()
        {
            if (selected == null) return;
            RefreshStats();
            if (!IsShowing) return;
            float dt = Time.unscaledDeltaTime;
            AdvancePreview(dt);
            projectileViews.Sync(simulation, projectilePrefab, arena, arena.position);
            foreach (var id in views.Keys.Where(id => !simulation.Robots.Any(r => r.Id == id) && !simulation.Enemies.Any(e => e.Id == id)).ToArray())
            { Destroy(views[id].gameObject); views.Remove(id); }
            foreach (var bot in simulation.Robots)
                views[bot.Id].Sync(World(bot.Position), bot.Health, bot.MaxHealth, false, false, bot.Buffs.Count > 0, previewCamera.transform.rotation);
            foreach (var e in simulation.Enemies)
            {
                if (!views.ContainsKey(e.Id)) AddView(e.Id, battle.PreviewEnemy.prefab);
                views[e.Id].Sync(World(e.Position),e.Health,e.Spec.health,e.Stunned(simulation.Time),e.Speed(simulation.Time)<e.Spec.speed,false,previewCamera.transform.rotation);
            }
            foreach (var view in views.Values) view.TickVisual(dt);
            foreach (var e in simulation.Events)
            {
                if (views.TryGetValue(e.Source,out var view))
                {
                    if (e.Kind == "launch") view.AimAt(World(e.Position));
                    else if (e.Kind == "hit" || e.Kind == "buff" || e.Kind == "stun") view.FireAt(World(e.Position));
                }
                if (e.Radius > 0) { effectRing.Show(World(e.Position),e.Radius,e.Kind=="buff"?Color.green:Color.yellow); effectSeconds=.3f; }
            }
            simulation.Events.Clear(); effectSeconds-=dt; if(effectSeconds<=0) effectRing.Hide();
            RefreshStatus();
        }
        void RefreshStatus()
        {
            if (simulation.RemainingEnemies == 0) { status.text = $"시연 완료 · {Math.Max(0,1-emptyElapsed):0.0}s 후 다시 시작"; return; }
            var bot = simulation.Robots.FirstOrDefault();
            if (bot == null) { status.text = "시연 종료"; return; }
            var program = bot.Program;
            string state;
            if (program == null || program.IsEmpty) state = "코드 없음 · 대기";
            else if (program.Fault != null) state = program.Fault;
            else if (program.WaitRemaining(simulation.Time) > 0)
                state = $"wait · {program.WaitRemaining(simulation.Time):0.0}s";
            else if (program.StepsLastTick == 0) state = "코드 대기";
            else switch (bot.LastResult)
            {
                case ActionResult.CoolingDown:
                    state = $"재사용 대기 · {Math.Max(0, bot.NextAction - simulation.Time):0.0}s"; break;
                case ActionResult.NoTarget: state = "대상 없음"; break;
                case ActionResult.Ineffective: state = "호환되지 않는 명령"; break;
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
            var s = selected.Spec;
            float nextMultiplier = RobotState.UpgradeMultiplier;
            stats.text = $"{battle.RobotName(selected.Id)}  Lv.{selected.Level}" + (selected.IsMaxLevel ? "" : " → Lv.2") +
                (s.role == RobotRole.Buffer ? "" : $"\n공격력  {UpgradeValue(selected.Damage,selected.Damage/selected.LevelMultiplier*nextMultiplier)}") +
                $"\n사거리  {selected.Range:0.##}\n공격 간격  {selected.Interval:0.##}s\n" +
                (selected.Health.HasValue ? (selected.IsMaxLevel ? $"체력  {selected.Health:0}/{selected.MaxHealth:0}" : $"최대 체력  {UpgradeValue(selected.MaxHealth,s.health*nextMultiplier)}") + $"\n저지  {s.blockCapacity}" : "체력 없음") +
                (s.role == RobotRole.Bomber ? $"\n폭발 반경  {s.effectRadius:0.##}" : s.role == RobotRole.Utility ? $"\n감속  {(1-s.slowMultiplier)*100:0}%" :
                s.role == RobotRole.Buffer ? $"\n버프 피해  {UpgradeValue(1+(s.buffDamage-1)*selected.LevelMultiplier,1+(s.buffDamage-1)*nextMultiplier)}배" +
                    $"\n버프 사거리  {UpgradeValue(1+(s.buffRange-1)*selected.LevelMultiplier,1+(s.buffRange-1)*nextMultiplier)}배" +
                    $"\n버프 간격  {UpgradeValue(Math.Max(.01f,1-(1-s.buffInterval)*selected.LevelMultiplier),Math.Max(.01f,1-(1-s.buffInterval)*nextMultiplier))}배" : "");
            var sim = battle.Simulation;
            coinLabel.text = $"코인  {sim.Coins:N0}";
            upgradeLabel.text = selected.IsMaxLevel ? "최고레벨" : "레벨업";
            upgradePrice.text = selected.IsMaxLevel ? "" : $"{sim.UpgradeCost:N0} 코인";
            if (!selected.IsMaxLevel && sim.Coins < sim.UpgradeCost) upgradePrice.text += $" · {sim.UpgradeCost-sim.Coins:N0} 부족";
            upgradeButton.interactable = sim.UpgradeError(selected.Id) == null;
        }
        string UpgradeValue(float current, float upgraded) => selected.IsMaxLevel || Mathf.Approximately(current, upgraded)
            ? $"{current:0.##}" : $"{current:0.##} <color=#6FFFD2>→ {upgraded:0.##}</color>";
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
