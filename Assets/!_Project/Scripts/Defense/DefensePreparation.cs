using System;
using System.Linq;

namespace CodingGame.Defense
{
    public sealed partial class DefenseSimulation
    {
        DefenseSimulation preparation;
        void SavePreparation()
        {
            if (Setup.PlayerFlow && Setup.Stage == 2) preparation = new DefenseSimulation(this);
        }
        public DefenseSimulation RetryPreparation()
        {
            if (preparation == null || Setup.Stage != 2 || WaveIndex == 4 ||
                (Phase != BattlePhase.Defeat && Phase != BattlePhase.Reward)) return null;
            return new DefenseSimulation(preparation);
        }
        DefenseSimulation(DefenseSimulation source)
        {
            Setup = source.Setup; WaveIndex = source.WaveIndex; BaseHealth = source.BaseHealth;
            Coins = source.Coins; Kills = source.Kills; Leaks = source.Leaks; Time = source.Time;
            nextId = source.nextId; currentWave = source.currentWave.ToArray(); nextWave = source.nextWave.ToArray();
            groupCounts = new int[currentWave.Length]; waveStart = Time; waveLeaksAtStart = Leaks;
            LastSurvivors = source.LastSurvivors; LastWaveTimedOut = false;
            WaveSurvivors.AddRange(source.WaveSurvivors);
            foreach (var stock in source.Inventory) Inventory.Add(stock.Key, stock.Value);
            foreach (var stock in source.RobotInventory) RobotInventory.Add(stock.Key, stock.Value);
            foreach (var bot in source.Robots)
                Robots.Add(new RobotState {
                    Id = bot.Id, DefinitionIndex = bot.DefinitionIndex, PlacedWave = bot.PlacedWave,
                    Position = bot.Position, Spec = bot.Spec.Copy(), Level = bot.Level, LevelCap = bot.LevelCap,
                    UpgradeFactor = bot.UpgradeFactor, UpgradeCostPaid = bot.UpgradeCostPaid,
                    Health = bot.Health, Action = bot.Action, NextAction = Time + bot.Spec.interval,
                    BlockingEnabled = bot.BlockingEnabled, AutoExecute = bot.AutoExecute,
                    Program = bot.Program == null ? null : new DefenseProgram(bot.Program.Name, bot.Program.CopyBlocks())
                });
            foreach (var draft in source.drafts)
                drafts.Add(draft.Key, draft.Value.ToDictionary(item => item.Key, item => item.Value));
        }
    }
}
