using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // Robot and enemy health bars as UI on one layer (13차: they were three cubes per actor, turned to the camera every frame
    // and recoloured with property blocks, so each drew on its own). An actor gets a bar from a pool baked in the scene
    // (Tools/Optimize/Unit Health Bars; a wave larger than the pool copies more) while it is shown, placed where the camera
    // shows its bar anchor and as wide as its bar in the world. The battle uses one over the screen, the code preview one over
    // its picture.
    public sealed class DefenseHealthBars : MonoBehaviour
    {
        [SerializeField] List<RectTransform> bars = new List<RectTransform>();
        // Height to width of a bar, and the least height so a far one still reads.
        const float Aspect = .13f, MinHeight = 6;

        sealed class Bar
        {
            public RectTransform rect, fill, trail;
            public readonly DefenseHealthTrail lag = new DefenseHealthTrail();
            public int frame;
            public float ratio = -1, lagShown = -1;
            public Vector2 size, position = new Vector2(float.NaN, 0);
        }
        readonly Dictionary<long, Bar> shown = new Dictionary<long, Bar>();
        readonly Stack<Bar> spare = new Stack<Bar>();
        readonly List<long> gone = new List<long>();
        RectTransform layer;
        int frame;

        void Awake()
        {
            layer = (RectTransform)transform;
            foreach (var bar in bars) { bar.gameObject.SetActive(false); spare.Push(Wrap(bar)); }
        }
        static Bar Wrap(RectTransform rect) =>
            new Bar { rect = rect, fill = (RectTransform)rect.Find("Well/Fill"), trail = (RectTransform)rect.Find("Well/Trail") };

        // A robot's key, apart from the enemies' (their ids are counted separately).
        public static long RobotKey(int id) => (1L << 32) + id;

        public void Show(long key, Camera camera, Vector3 anchor, float worldWidth, float health, float maximum)
        {
            if (!shown.TryGetValue(key, out var bar))
            {
                bar = spare.Count > 0 ? spare.Pop() : Wrap(Instantiate(bars[0], layer));
                bar.ratio = bar.lagShown = -1; bar.position.x = float.NaN;
                bar.lag.Reset(); bar.rect.gameObject.SetActive(true);
                shown.Add(key, bar);
            }
            bar.frame = frame;
            var area = layer.rect;
            Vector3 at = camera.WorldToViewportPoint(anchor);
            Vector3 edge = camera.WorldToViewportPoint(anchor + camera.transform.right * (worldWidth * .5f));
            var position = new Vector2(at.x * area.width, at.y * area.height);
            float width = Mathf.Round(Mathf.Abs(edge.x - at.x) * area.width * 2);
            var size = new Vector2(width, Mathf.Max(MinHeight, Mathf.Round(width * Aspect)));
            // Only what changed is written: a resting bar costs nothing, a walking one moves only.
            if (float.IsNaN(bar.position.x) || (position - bar.position).sqrMagnitude > .01f) { bar.position = position; bar.rect.anchoredPosition = position; }
            if (size != bar.size) { bar.size = size; bar.rect.sizeDelta = size; }
            float ratio = maximum > 0 ? Mathf.Clamp01(health / maximum) : 0;
            float lag = bar.lag.Step(ratio, Time.unscaledDeltaTime);
            if (ratio != bar.ratio) { bar.ratio = ratio; bar.fill.anchorMax = new Vector2(ratio, 1); }
            if (lag != bar.lagShown) { bar.lagShown = lag; bar.trail.anchorMax = new Vector2(lag, 1); }
        }

        // Bars not shown since the last call go back to the pool.
        public void Flush()
        {
            gone.Clear();
            foreach (var pair in shown) if (pair.Value.frame != frame) gone.Add(pair.Key);
            foreach (var key in gone) { var bar = shown[key]; bar.rect.gameObject.SetActive(false); spare.Push(bar); shown.Remove(key); }
            frame++;
        }

#if UNITY_EDITOR
        // The bars baked by Tools/Optimize/Unit Health Bars.
        public void SetBars(List<RectTransform> made) => bars = made;
#endif
    }
}
