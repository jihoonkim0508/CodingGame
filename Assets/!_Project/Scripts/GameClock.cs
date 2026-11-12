using UnityEngine;

namespace CodingGame
{
    // The Esc menus stop the game's time (play feedback 2026-11-12): Time.timeScale goes to 0 for the world, the sound
    // pauses, and Now/Delta (unscaled time that stands still meanwhile) drive the lessons and the stage map's animations,
    // so a timed step or the road's reveal picks up where it was. Leaving a scene with the menu open resumes it.
    public static class GameClock
    {
        static float pausedAt = -1, pausedTotal, scale = 1;
        public static bool Paused => pausedAt >= 0;
        public static float Now => (Paused ? pausedAt : Time.unscaledTime) - pausedTotal;
        public static float Delta => Paused ? 0 : Time.unscaledDeltaTime;

        public static void Pause(bool pause)
        {
            if (pause == Paused) return;
            if (pause) { pausedAt = Time.unscaledTime; scale = Time.timeScale; Time.timeScale = 0; }
            else { pausedTotal += Time.unscaledTime - pausedAt; pausedAt = -1; Time.timeScale = scale; }
            AudioListener.pause = pause;
        }

        // Play mode may start without a domain reload: never begin paused.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { pausedAt = -1; pausedTotal = 0; scale = 1; }
    }
}
