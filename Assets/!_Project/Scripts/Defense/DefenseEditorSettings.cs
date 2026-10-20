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
        public bool waveTimeout = true, deploymentLimit = true, inventoryLimit = true;
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
        public int[] roleLimits = { 1, 18, 18, 18, 18, 18 };
        public float healthLinear = .045f, healthQuadratic = .0015f;
        public float speedLinear = .025f, speedQuadratic = .0005f, speedCap = 1.7f;
        public float fastHealth = .75f, tankHealth = 1.5f, fastSpeed = 1.3f, tankSpeed = .75f;
        public float[] waveMultipliers = { .9f, 1f, 1.05f, 1.15f, 1f };
        public void Validate()
        {
            if (maxLevel < 1 || !Rules.Positive(upgradeMultiplier) ||
                roleLimits == null || roleLimits.Length != 6 || roleLimits.Any(v => v < 1) ||
                waveMultipliers == null || waveMultipliers.Length == 0 || waveMultipliers.Any(v => !Rules.Positive(v)) ||
                new[] { healthLinear, healthQuadratic, speedLinear, speedQuadratic }.Any(v => !Rules.Nonnegative(v)) ||
                new[] { speedCap, fastHealth, tankHealth, fastSpeed, tankSpeed }.Any(v => !Rules.Positive(v)))
                throw new ArgumentException("밸런스 규칙 범위를 확인하세요.");
        }
        public EnemySpec Scale(EnemySpec original, int stage, int wave)
        {
            var enemy = original.Copy();
            if (stage == 0) return enemy;
            int progress = (stage - 1) * 5 + wave;
            float multiplier = wave < waveMultipliers.Length ? waveMultipliers[wave] : 1;
            enemy.health *= (1 + healthLinear * progress + healthQuadratic * progress * progress) *
                (enemy.type == EnemyType.Fast ? fastHealth : enemy.type == EnemyType.Tank ? tankHealth : 1) * multiplier;
            enemy.speed = Math.Min(enemy.speed * (1 + speedLinear * progress + speedQuadratic * progress * progress) *
                (enemy.type == EnemyType.Fast ? fastSpeed : enemy.type == EnemyType.Tank ? tankSpeed : 1) * multiplier, original.speed * speedCap);
            return enemy;
        }
    }
}
