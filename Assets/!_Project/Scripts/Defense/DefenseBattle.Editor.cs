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
        public bool EditorManualClock { get; set; }
        public event Action<string, DefenseSimulation, CombatEvent> EditorCombatEvent;
        public void EditorObserve(string origin, DefenseSimulation sim, CombatEvent evt)
            => EditorCombatEvent?.Invoke(origin, sim, evt);
        public BattleSetup EditorReadSetup() => MakeSetup(campaign, stage);
        public void EditorStartSession(int testStage, int wave, bool previousStageRewards, DefenseTestOptions options,
            DefenseCampaignDefinition overrideCampaign = null)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode 필요");
            tutorial?.EditorStop();
            EditorOptions = options.Copy();
            // Resolve content into a copy before altering the live session.
            var setup = MakeSetup(overrideCampaign ? overrideCampaign : campaign, testStage);
            setup.InitialWave=wave-1; setup.PreviousStageRewards=previousStageRewards;
            setup.TestOptions=EditorOptions; setup.Validate();
            stage=testStage;
            if (overrideCampaign) { robots=overrideCampaign.robots; enemies=overrideCampaign.enemies; }
            ResetSession(setup);
            EditorSetOptions(options);
        }
        public void EditorSetOptions(DefenseTestOptions options)
        {
            EditorOptions=options.Copy();
            if (Simulation != null) Simulation.Setup.TestOptions=EditorOptions;
            if (!EditorOptions.tutorial) tutorial?.EditorStop();
            tutorial?.EditorBridge.SetInputLimited(EditorOptions.tutorialInput);
            hitEffects.Clear(); hitEffects.Options=EditorOptions;
            hitEffects.SetHitRangesVisible(EditorOptions.hitRanges);
            attackPreview.SetHitRangesVisible(EditorOptions.hitRanges);
            attackPreview.EditorApplyOptions(EditorOptions);
            if (!EditorOptions.projectiles) projectileViews.Clear();
            RefreshHUD();
        }
        public void EditorApplyBalance(DefenseCampaignDefinition content)
        {
            if(!Application.isPlaying||Simulation==null)throw new InvalidOperationException("Play Mode 전투 초기화 필요");
            var setup=MakeSetup(content,stage);setup.InitialWave=Simulation.WaveIndex;
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
