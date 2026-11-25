using System;
using System.Linq;

namespace CodingGame.Defense
{
    [Serializable]
    public sealed class DefenseTestOptions
    {
        public bool tutorial = true, tutorialInput = true, robotCode = true;
        public bool enemyMovement = true, enemyAttack = true, enemySpawn = true;
        public bool baseDamage = true, robotDamage = true, attackDamage = true;
        public bool deploymentLimit = true, inventoryLimit = true;
        public bool unlockAll;
        public bool supply = true, rewards = true, drops = true;
        public bool ranges = true, hitRanges, launchEffects = true, hitEffects = true, slashEffects = true;
        public bool projectiles = true, preview = true, detailedLog;
        public DefenseTestOptions Copy() => (DefenseTestOptions)MemberwiseClone();
    }

    [Serializable]
    public sealed class DefenseBalanceRules
    {
        public int maxLevel = 2;
        public float upgradeMultiplier = 1.2f;
        public int[] roleLimits = { 1, 18, 18 }; // by RobotRole: Support, Melee, Ranged
        public float healthLinear = .045f, healthQuadratic = .0015f;
        public float speedLinear = .025f, speedQuadratic = .0005f, speedCap = 1.7f;
        public float fastHealth = .75f, tankHealth = 1.5f, fastSpeed = 1.3f, tankSpeed = .75f;
        // Healer and Shielder: frail supports that keep pace with a tank (their pulse amount grows with the health curve).
        public float healerHealth = .8f, shielderHealth = 1f, healerSpeed = .8f, shielderSpeed = .8f;
        // Queue/Stack centipedes: per segment, as a normal enemy's.
        public float queueHealth = 1f, stackHealth = 1f, queueSpeed = .8f, stackSpeed = .8f;
        public float[] waveMultipliers = { .9f, 1f, 1.05f, 1.15f, 1f };
        // Difficulty steps a stage adds to the health/speed curves (progress = (stage - 1) * stageProgress + wave).
        public int stageProgress = 5;
        // An action of the other class (attack robot buffing or slowing, support robot attacking): this share of its effect
        // (damage, buff, slow, stun) and this many times its cooldown.
        public float reducedStrength = .25f, reducedIntervalMultiplier = 2;
        // Stage result: 3 stars with this % of the core left, 2 with twoStarCorePercent, 1 for a clear.
        public int threeStarCorePercent = 80, twoStarCorePercent = 40;
        public void Validate()
        {
            if (maxLevel < 1 || !Rules.Positive(upgradeMultiplier) ||
                roleLimits == null || roleLimits.Length != Rules.RoleCount || roleLimits.Any(v => v < 1) ||
                waveMultipliers == null || waveMultipliers.Length == 0 || waveMultipliers.Any(v => !Rules.Positive(v)) ||
                new[] { healthLinear, healthQuadratic, speedLinear, speedQuadratic }.Any(v => !Rules.Nonnegative(v)) ||
                new[] { speedCap, fastHealth, tankHealth, fastSpeed, tankSpeed, healerHealth, shielderHealth, healerSpeed, shielderSpeed, queueHealth, stackHealth, queueSpeed, stackSpeed }.Any(v => !Rules.Positive(v)) ||
                stageProgress < 0 || !Rules.Positive(reducedStrength) || reducedStrength > 1 || !(reducedIntervalMultiplier >= 1) ||
                twoStarCorePercent < 0 || threeStarCorePercent < twoStarCorePercent || threeStarCorePercent > 100)
                throw new ArgumentException("밸런스 규칙 범위를 확인하세요.");
        }
        public EnemySpec Scale(EnemySpec original, int stage, int wave)
        {
            var enemy = original.Copy();
            if (stage == 0) return enemy;
            int progress = (stage - 1) * stageProgress + wave;
            float multiplier = wave < waveMultipliers.Length ? waveMultipliers[wave] : 1;
            float curve = 1 + healthLinear * progress + healthQuadratic * progress * progress;
            enemy.health *= curve * (enemy.type switch
                { EnemyType.Fast => fastHealth, EnemyType.Tank => tankHealth, EnemyType.Healer => healerHealth, EnemyType.Shielder => shielderHealth,
                  EnemyType.Queue => queueHealth, EnemyType.Stack => stackHealth, _ => 1 }) * multiplier;
            enemy.abilityAmount *= curve * multiplier; // a heal or shield keeps up with the health it works on
            enemy.speed = Math.Min(enemy.speed * (1 + speedLinear * progress + speedQuadratic * progress * progress) *
                (enemy.type switch { EnemyType.Fast => fastSpeed, EnemyType.Tank => tankSpeed, EnemyType.Healer => healerSpeed, EnemyType.Shielder => shielderSpeed,
                    EnemyType.Queue => queueSpeed, EnemyType.Stack => stackSpeed, _ => 1 }) * multiplier, original.speed * speedCap);
            return enemy;
        }
    }
}
