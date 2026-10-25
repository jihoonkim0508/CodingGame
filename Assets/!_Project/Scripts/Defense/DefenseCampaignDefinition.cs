using System;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense
{
    [Serializable]
    public sealed class DefenseCampaignStage
    {
        public DefenseBattle.Wave[] waves = Array.Empty<DefenseBattle.Wave>();
        public float waveSeconds = 35;
    }

    [CreateAssetMenu(menuName = "CodingGame/Defense/Campaign")]
    public sealed class DefenseCampaignDefinition : ScriptableObject
    {
        public RobotDefinition[] robots = Array.Empty<RobotDefinition>();
        public EnemyDefinition[] enemies = Array.Empty<EnemyDefinition>();
        public int baseHealth = 12, robotLimit = 18;
        public DefenseBalanceRules rules = new DefenseBalanceRules();
        public DefenseProgression progression = new DefenseProgression();
        public ActionPermission[] permissions = Array.Empty<ActionPermission>();
        public DefenseCampaignStage[] stages = Array.Empty<DefenseCampaignStage>();

        public SpawnGroup[][] ReadWaves(int stage)
        {
            if (stage < 1 || stage > stages.Length) throw new ArgumentException("스테이지 범위를 확인하세요.");
            return stages[stage - 1].waves.Select(w => w.groups.Select(g => {
                var spec = g.enemy ? g.enemy.stats.Copy() : throw new ArgumentException("스폰 적 참조 누락");
                if (g.healthOverride > 0) spec.health = g.healthOverride;
                if (g.speedOverride > 0) spec.speed = g.speedOverride;
                return new SpawnGroup { Enemy = spec, DefinitionIndex = Array.IndexOf(enemies, g.enemy),
                    RouteIndex = g.route, Count = g.count, Delay = g.delay, Interval = g.interval };
            }).ToArray()).ToArray();
        }
        public void ValidateContent()
        {
            if (rules == null || progression == null || permissions == null) throw new ArgumentException("캠페인 규칙·보급 설정 누락");
            rules.Validate(); progression.Validate();
            if (baseHealth < 1 || robotLimit < 1 || robots == null || enemies == null || stages == null || stages.Length == 0 ||
                robots.Length != 6 || robots.Any(r => !r || !r.prefab) || robots.Select(r => r.stats.role).Distinct().Count() != 6 ||
                enemies.Length == 0 || enemies.Any(e => !e || !e.prefab)) throw new ArgumentException("캠페인 참조·기본 수치 확인 필요");
            foreach (var r in robots) r.stats.Validate();
            foreach (var e in enemies) e.stats.Validate();
            foreach (var s in stages)
                if (s == null || !Rules.Positive(s.waveSeconds) || s.waves == null || s.waves.Length == 0 ||
                    s.waves.Any(w => w == null || w.groups == null || w.groups.Length == 0 ||
                        w.groups.Any(g => g == null || !g.enemy || !enemies.Contains(g.enemy) || g.count < 1 || g.route < 0 ||
                            !Rules.Nonnegative(g.delay) || !Rules.Positive(g.interval) ||
                            !Rules.Nonnegative(g.healthOverride) || !Rules.Nonnegative(g.speedOverride)))) throw new ArgumentException("웨이브 설정 확인 필요");
        }
    }
}
