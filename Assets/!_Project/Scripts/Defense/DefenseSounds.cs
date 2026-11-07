using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.Defense
{
    // Short battle sounds: what landed, what died, what got through and how the wave ended, so the player hears the result
    // of their code as well as seeing it. Resources/Sounds/<kind> (Kenney's CC0 packs, see CREDITS.txt there) replaces the
    // tone made in code for the same kind.
    public sealed class DefenseSounds : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource source;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        BattlePhase shownPhase = BattlePhase.Ready;

        // The player's sound volume (Esc menu slider, 0-1), kept across sessions; it scales every sound through the listener.
        // The old on/off switch's "off" starts it at 0.
        const string VolumeKey = "CodingGame.Sound.Volume", MutedKey = "CodingGame.Sound.Muted";
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, PlayerPrefs.GetInt(MutedKey, 0) == 1 ? 0 : 1);
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = Mathf.Clamp01(value); }
        }
        // From the first scene on (the title's music too), not only once a battle has loaded.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyVolume() => AudioListener.volume = Volume;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            foreach (var clip in Resources.LoadAll<AudioClip>("Sounds")) clips[clip.name] = clip;
            // A tone made in code only for a kind with no sound file (13차: all of them were made at every battle load, then
            // replaced by the files).
            void Tone(string kind, Func<AudioClip> make) { if (!clips.ContainsKey(kind)) clips[kind] = make(); }
            Tone("hit", () => Sweep("hit", .03f, 1400, 900, volume: .1f));
            Tone("projectile-hit", () => clips["hit"]);
            Tone("slash", () => Sweep("slash", .09f, 900, 250, noise: .7f, volume: .22f));
            Tone("shot", () => Sweep("shot", .06f, 1000, 700, volume: .15f));
            Tone("launch", () => clips["shot"]);
            Tone("killed", () => Sweep("killed", .09f, 500, 950, volume: .25f));
            Tone("stun", () => Sweep("stun", .12f, 140, 90, square: .5f, volume: .28f));
            Tone("buff", () => Notes("buff", .06f, .14f, 660, 990));
            Tone("blast", () => Sweep("blast", .2f, 180, 60, noise: .8f, volume: .3f));
            Tone("leaked", () => Sweep("leaked", .25f, 170, 150, square: .8f, volume: .3f));
            Tone("rush", () => Sweep("rush", .5f, 160, 70, square: .8f, noise: .2f, volume: .4f));
            Tone("tank-destroyed", () => Sweep("tank-destroyed", .3f, 320, 90, square: .4f, volume: .3f));
            Tone("place", () => Sweep("place", .05f, 700, 900, volume: .2f));
            Tone("deny", () => Notes("deny", .07f, .2f, 220, 180));
            Tone("clear", () => Notes("clear", .1f, .28f, 523.25f, 659.25f, 783.99f));
            Tone("timeup", () => Notes("timeup", .12f, .25f, 523.25f, 392f));
            Tone("victory", () => Notes("victory", .12f, .3f, 523.25f, 659.25f, 783.99f, 1046.5f));
            Tone("defeat", () => Notes("defeat", .16f, .3f, 392f, 329.63f, 261.63f));
            // Each stage-clear star rings a step higher than the one before.
            Tone("star1", () => Notes("star1", .16f, .3f, 1318.5f, 1975.5f));
            Tone("star2", () => Notes("star2", .16f, .3f, 1568f, 2349.3f));
            Tone("star3", () => Notes("star3", .16f, .34f, 2093f, 3136f));
            Tone("wave-start", () => Sweep("wave-start", .35f, 220, 440, square: .6f, volume: .3f));
        }

        // Hits and kills can pile up in one frame; one of each kind per short gap is enough to hear.
        public void Play(string kind)
        {
            if (!clips.TryGetValue(kind, out var clip)) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(kind, out float last) && now - last < (kind == "hit" || kind == "projectile-hit" ? .06f : .04f)) return;
            lastPlayed[kind] = now;
            source.PlayOneShot(clip);
        }

        // The wave's end, once per change of phase.
        public void Follow(DefenseSimulation sim)
        {
            if (sim.Phase == shownPhase) return;
            shownPhase = sim.Phase;
            if (sim.Phase == BattlePhase.Reward) Play(sim.LastWaveTimedOut ? "timeup" : "clear");
            else if (sim.Phase == BattlePhase.Victory) Play("victory");
            else if (sim.Phase == BattlePhase.Defeat) Play("defeat");
        }

        // A tone gliding between two pitches with a quick attack and a falling tail; square adds a buzz, noise a hiss.
        static AudioClip Sweep(string name, float seconds, float from, float to, float square = 0, float noise = 0, float volume = .3f)
        {
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            Fill(data, 0, data.Length, from, to, square, noise, volume, new System.Random(7));
            return Clip(name, data);
        }
        // Notes one after another: up for a clear, down for a loss.
        static AudioClip Notes(string name, float step, float volume, params float[] pitches)
        {
            int length = Mathf.CeilToInt(step * Rate), tail = length * 2;
            var data = new float[length * (pitches.Length - 1) + tail];
            for (int i = 0; i < pitches.Length; i++) Fill(data, i * length, i + 1 == pitches.Length ? tail : length, pitches[i], pitches[i], .15f, 0, volume, null);
            return Clip(name, data);
        }
        static void Fill(float[] data, int start, int count, float from, float to, float square, float noise, float volume, System.Random random)
        {
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                phase += 2 * Math.PI * Mathf.Lerp(from, to, t) / Rate;
                float sine = (float)Math.Sin(phase);
                float sample = Mathf.Lerp(sine, Math.Sign(sine), square);
                if (random != null) sample = Mathf.Lerp(sample, (float)(random.NextDouble() * 2 - 1), noise);
                float envelope = Mathf.Min(1, i / (Rate * .004f)) * (1 - t) * (1 - t);
                data[start + i] += sample * envelope * volume;
            }
        }
        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create("Defense " + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
