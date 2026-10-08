#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed partial class DefenseBattle
    {
        public DefenseTestOptions EditorOptions { get; private set; } = new DefenseTestOptions();
        public DefenseCampaignDefinition EditorCampaign => campaign;
        public DefenseCodeEditor EditorCode => codeEditor;
        public DefenseAttackPreview EditorPreview => attackPreview;
        public bool EditorManualClock { get; set; }
        public event Action<string, DefenseSimulation, CombatEvent> EditorCombatEvent;
        public void EditorObserve(string origin, DefenseSimulation sim, CombatEvent evt)
            => EditorCombatEvent?.Invoke(origin, sim, evt);
        public BattleSetup EditorReadSetup() => MakeSetup();
        public DefenseCampaignDefinition EditorExportCampaign()
        {
            if (campaign) return Instantiate(campaign);
            var result = ScriptableObject.CreateInstance<DefenseCampaignDefinition>();
            result.robots = robots.ToArray(); result.enemies = enemies.ToArray();
            result.baseHealth = baseHealth; result.robotLimit = robotLimit;
            result.progression = progression.Copy();
            result.permissions = approvedCrossRoleActions.Select(p => new ActionPermission {role=p.role,action=p.action,strength=p.strength}).ToArray();
            result.stages = Enumerable.Range(1,DefenseCurriculum.StageCount).Select(_ => new DefenseCampaignStage {
                waveSeconds=waveSeconds,
                waves=waves.Select(w=>new Wave {groups=w.groups.Select(g=>new SpawnEntry {
                    enemy=g.enemy,route=g.route,count=g.count,delay=g.delay,interval=g.interval}).ToArray()}).ToArray()
            }).ToArray();
            return result;
        }
        public void EditorStartSession(int testStage, int wave, bool previousStageRewards, DefenseTestOptions options,
            DefenseCampaignDefinition overrideCampaign = null)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode 필요");
            tutorial?.EditorStop();
            EditorOptions = options.Copy();
            // Resolve content into a copy before altering the live session.
            var setup = overrideCampaign ? MakeCampaignSetup(overrideCampaign,testStage) : campaign ? MakeCampaignSetup(campaign,testStage) : MakeSetup();
            setup.StageOneLearning=tutorial && tutorial.StageOneEnabled && testStage==1;
            setup.Stage=testStage; setup.InitialWave=wave-1; setup.PreviousStageRewards=previousStageRewards;
            setup.TestOptions=EditorOptions; setup.Validate();
            if (wave < 1 || wave > setup.Waves.Length) throw new ArgumentException("초기 웨이브 범위 오류");
            stage=testStage;
            if (overrideCampaign) { robots=overrideCampaign.robots; enemies=overrideCampaign.enemies; }
            ResetSession(setup);
            EditorSetOptions(options);
            RefreshHUD();
        }
        public void EditorSetOptions(DefenseTestOptions options)
        {
            EditorOptions=options.Copy();
            if (Simulation != null) Simulation.Setup.TestOptions=EditorOptions;
            if (!EditorOptions.tutorial) tutorial?.EditorStop();
            tutorial?.EditorBridge.SetInputLimited(EditorOptions.tutorialInput);
            hitEffects.Clear();
            hitEffects.SetHitRangesVisible(EditorOptions.hitRanges);
            attackPreview.SetHitRangesVisible(EditorOptions.hitRanges);
            attackPreview.EditorApplyOptions(EditorOptions);
            if (!EditorOptions.projectiles) projectileViews.Clear();
            RefreshHUD();
        }
        public void EditorEnableDeveloper()
        {
            inventoryUI.Close();codeEditor.Close();CancelPlacement();DeveloperMode=true;
            developerRoot.SetActive(true);playerHUD.SetVisible(false);
        }
        public void EditorApplyBalance(DefenseCampaignDefinition content)
        {
            if(!Application.isPlaying||Simulation==null)throw new InvalidOperationException("Play Mode 전투 초기화 필요");
            var setup=MakeCampaignSetup(content,stage);setup.InitialWave=Simulation.WaveIndex;
            Simulation.ApplyBalance(setup,content.robots.Select(r=>r.stats.Copy()).ToArray(),content.enemies.Select(e=>e.stats.Copy()).ToArray());
            robots=content.robots;enemies=content.enemies;
            projectileViews.Clear();hitEffects.Clear();foreach(var view in robotViews.Values)view.ClearVisuals();
            attackPreview.EditorApplyBalance();SyncViews(0);RefreshHUD();
        }
        public void EditorStep()
        {
            Simulation.StepOnce();
            SyncViews((float)DefenseSimulation.TickDuration); RefreshHUD();
        }
        public void EditorGrantAll() { Simulation.GrantAll(); RefreshHUD(); }
        public void EditorSetCoins(int value) { Simulation.SetCoins(value); RefreshHUD(); }
        public void EditorRestoreRobotState(int id,int level,float health)
        {
            var bot=Simulation.Robots.Single(r=>r.Id==id);
            if(level<1||level>bot.LevelCap||!Rules.Finite(health))throw new ArgumentException("시나리오 레벨·체력 오류");
            bot.Level=level;if(bot.Health.HasValue&&health>=0)bot.Health=Math.Min(health,bot.MaxHealth);
            SyncViews(0);RefreshHUD();
        }
    }
}
#endif
