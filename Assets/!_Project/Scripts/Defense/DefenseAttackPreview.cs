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
        [SerializeField] DefenseRangeRing effectRing;
        [SerializeField] Transform projectilePrefab;
        [SerializeField, Min(.5f)] float loopSeconds = 8;
        readonly DefenseProjectileVisuals projectileViews = new DefenseProjectileVisuals();
        DefenseSimulation simulation;
        RobotState selected;
        DefenseProgram source;
        readonly Dictionary<int, DefenseActorView> views = new Dictionary<int, DefenseActorView>();
        RenderTexture previewTexture;
        float effectSeconds, loopElapsed;
        public DefenseSimulation PreviewSimulation => simulation;
        public bool IsShowing => selected != null && previewPanel.activeSelf;
        void Start()
        {
            if (!projectilePrefab || !battle || !arena || !previewCamera || !previewImage || !previewPanel || !previewButton || !previewButtonLabel || !stats || !status || !effectRing)
                throw new InvalidOperationException("공격 미리보기 Inspector 참조를 연결하세요.");
            if (loopSeconds < .5f) throw new InvalidOperationException("미리보기 반복 시간은 0.5초 이상이어야 합니다.");
            previewButton.onClick.AddListener(TogglePreview);
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
            ClearActors(); source = selected.Program; loopElapsed = 0; effectSeconds = 0;
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
            bot.RangeSetting = selected.RangeSetting;
            if (source != null) simulation.ApplyProgram(bot.Id, source.Name, source.CopyBlocks());
            AddView(bot.Id, battle.RobotDefinition(bot.DefinitionIndex).prefab);
            simulation.Start();
        }
        void AddView(int id, DefenseActorView prefab)
        { var view = Instantiate(prefab, arena); view.Id = id; views.Add(id, view); }
        Vector3 World(Point p) => arena.position + new Vector3(p.X,0,p.Y);
        void Update()
        {
            if (selected == null) return;
            RefreshStats();
            if (!IsShowing) return;
            float dt = Time.unscaledDeltaTime;
            loopElapsed += dt;
            if (source != selected.Program || loopElapsed >= loopSeconds ||
                simulation.Phase == BattlePhase.Victory || simulation.Phase == BattlePhase.Defeat || simulation.Phase == BattlePhase.Reward)
                ResetPreview();
            simulation.Advance(dt);
            projectileViews.Sync(simulation, projectilePrefab, arena, arena.position);
            foreach (var id in views.Keys.Where(id => !simulation.Robots.Any(r => r.Id == id) && !simulation.Enemies.Any(e => e.Id == id)).ToArray())
            { Destroy(views[id].gameObject); views.Remove(id); }
            foreach (var bot in simulation.Robots)
                views[bot.Id].Sync(World(bot.Position), bot.Health, bot.Spec.health, false, false, bot.Buffs.Count > 0, previewCamera.transform.rotation);
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
            var previewBot = simulation.Robots[0];
            status.text = source == null || source.CopyBlocks().Count == 0 ? "코드 없음 · 대기" : previewBot.Program?.Fault ?? $"코드 시연 · {previewBot.Executions}회";
        }
        void RefreshStats()
        {
            var s = selected.Spec;
            stats.text = $"{battle.RobotName(selected.Id)}\n\n공격력  {selected.Damage:0.##}\n사거리  {selected.Range:0.##}\n공격 간격  {selected.Interval:0.##}s\n" +
                (selected.Health.HasValue ? $"체력  {selected.Health:0}/{s.health:0}\n저지  {s.blockCapacity}" : "체력 없음") +
                (s.role == RobotRole.Bomber ? $"\n폭발 반경  {s.effectRadius:0.##}" : s.role == RobotRole.Utility ? $"\n감속  {(1-s.slowMultiplier)*100:0}%" :
                s.role == RobotRole.Buffer ? $"\n버프  피해 ×{s.buffDamage:0.##}\n사거리 ×{s.buffRange:0.##} · 간격 ×{s.buffInterval:0.##}" : "");
        }
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
