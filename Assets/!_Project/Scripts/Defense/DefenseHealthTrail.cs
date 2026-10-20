using System;

namespace CodingGame.Defense
{
    // The pale "lost health" trail behind a health bar: after a hit it holds the old value briefly, then slides down
    // to the current value. Healing snaps it up. Values are fractions 0..1.
    public sealed class DefenseHealthTrail
    {
        public const float HoldSeconds = .35f, SlidePerSecond = 1.2f;
        float hold, last;
        bool started;
        public float Value { get; private set; }
        public bool JustHit { get; private set; }
        public float Step(float current, float deltaSeconds)
        {
            JustHit = started && current < last;
            if (!started || current >= Value) { Value = current; hold = 0; }
            else if (JustHit) hold = HoldSeconds; // each new hit restarts the pause
            else if (hold > 0) hold -= deltaSeconds;
            else Value = Math.Max(current, Value - SlidePerSecond * deltaSeconds);
            started = true; last = current;
            return Value;
        }
    }
}
