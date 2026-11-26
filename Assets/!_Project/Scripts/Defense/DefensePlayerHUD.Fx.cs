using System.Collections.Generic;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // The battle presentation's HUD parts (2026-11-25), built at runtime under the layers the HUD already has:
    // the code hologram over each running robot, coins flying into the header, the wave call's band, the running pill's dots.
    public sealed partial class DefensePlayerHUD
    {
        // ---- The code hologram: over each robot whose function runs, the line it stands at, in Python, on a dark glass chip
        // with a cyan edge and a tail pointing at the robot. It pops and lights up when the robot acts (an attack, a slow), its
        // ▶ blinks each time a loop goes round. A line is held at least .3 s, so a loop that runs many lines in a frame reads.
        // Shown only briefly (2026-11-26: grouped robots' chips stacked over the screen): an action, or a line it has not shown
        // in RepeatSeconds, wakes it for VisibleSeconds, then it fades; a loop going round the same lines does not wake it.
        // A chip that would cover one already up stays down, so a group shows one chip, the one up first.
        sealed class Hologram
        {
            public RectTransform rect; public Image back, mark; public TextMeshProUGUI text; public Outline edge; public CanvasGroup fade;
            public CodeBlock block; public float shownAt = -9, actedAt = -9, turnedAt = -9, wakeAt = -9, since = -9; public int turns = -1;
            public readonly Dictionary<CodeBlock, float> recent = new Dictionary<CodeBlock, float>();
        }
        readonly Dictionary<int, Hologram> holograms = new Dictionary<int, Hologram>();
        readonly List<Hologram> spareHolograms = new List<Hologram>();
        readonly List<int> goneHolograms = new List<int>();
        readonly Dictionary<int, float> actedAt = new Dictionary<int, float>();
        static readonly Color HoloBack = new Color(.05f, .14f, .22f, .9f), HoloLit = new Color(.16f, .55f, .78f, .95f),
            HoloText = new Color(.78f, .97f, 1f), HoloEdge = new Color(.35f, .9f, 1f, .75f);
        const float HoldSeconds = .3f, FlashSeconds = .3f, VisibleSeconds = 1.2f, FadeSeconds = .35f, RepeatSeconds = 3;

        // A robot's action this frame (DefenseSimulation's events whose source is the robot).
        void NoteAction(CombatEvent evt)
        {
            switch (evt.Kind)
            {
                case "slash": case "shot": case "launch": case "stun": case "shield-break": case "slow": case "block":
                    actedAt[evt.Source] = Time.unscaledTime; break;
            }
        }
        void RefreshCodeHolograms(DefenseSimulation sim)
        {
            bool battleOn = sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused;
            var canvas = (RectTransform)canvasRoot.transform;
            float now = Time.unscaledTime;
            goneHolograms.Clear(); goneHolograms.AddRange(holograms.Keys);
            if (battleOn && statusLayer)
                foreach (var bot in sim.Robots)
                {
                    var program = bot.Program;
                    if (program == null || !program.Running || program.Fault != null || robotActions.gameObject.activeSelf && bot.Id == battle.SelectedId) continue;
                    goneHolograms.Remove(bot.Id);
                    if (!holograms.TryGetValue(bot.Id, out var h))
                    {
                        h = spareHolograms.Count > 0 ? spareHolograms[spareHolograms.Count - 1] : MakeHologram();
                        if (spareHolograms.Count > 0) spareHolograms.RemoveAt(spareHolograms.Count - 1);
                        h.block = null; h.turns = -1; h.shownAt = h.wakeAt = h.since = -9; h.recent.Clear(); holograms[bot.Id] = h;
                    }
                    var block = program.CurrentBlock;
                    if (block != null && block != h.block && (h.block == null || now - h.shownAt >= HoldSeconds))
                    {
                        h.block = block; h.shownAt = now;
                        h.text.text = Line(block);
                        if (!h.recent.TryGetValue(block, out float seenAt) || now - seenAt > RepeatSeconds) h.wakeAt = now;
                        h.recent[block] = now;
                    }
                    if (h.turns >= 0 && bot.LoopTurnsThisWave != h.turns) h.turnedAt = now;
                    h.turns = bot.LoopTurnsThisWave;
                    if (actedAt.TryGetValue(bot.Id, out float acted)) h.actedAt = h.wakeAt = acted;
                    float shown = Mathf.Clamp01(1 - (now - h.wakeAt - VisibleSeconds) / FadeSeconds);
                    if (shown <= 0) { h.since = -9; if (h.rect.gameObject.activeSelf) h.rect.gameObject.SetActive(false); continue; }
                    if (h.since < 0) h.since = now;
                    h.fade.alpha = shown;
                    // Lit and popped right after an action, settling back over FlashSeconds.
                    float lit = Mathf.Clamp01(1 - (now - h.actedAt) / FlashSeconds);
                    h.back.color = Color.Lerp(HoloBack, HoloLit, lit);
                    h.text.color = Color.Lerp(HoloText, Color.white, lit);
                    h.edge.effectColor = Color.Lerp(HoloEdge, new Color(1, 1, 1, .95f), lit);
                    float turned = Mathf.Clamp01(1 - (now - h.turnedAt) / .2f);
                    h.mark.color = Color.Lerp(new Color(.35f, .9f, 1f), new Color(1, .9f, .4f), Mathf.Max(turned, lit));
                    h.mark.rectTransform.localScale = Vector3.one * (1 + .5f * turned);
                    // Over the head, above the health bar of a robot that has one (the tank's).
                    float height = DefenseBattle.RobotHeight + (bot.Health.HasValue ? DefenseActorView.HealthLift + .45f : 0);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, battleCamera.WorldToScreenPoint(DefenseViews.World(bot.Position, height)), null, out var head);
                    placed.Add((h, head + Vector2.up * (14 + 2 * Mathf.Sin(now * 2.4f + bot.Id))));
                    h.rect.localScale = Vector3.one * (1 + .16f * lit * lit);
                }
            // Robots side by side: a chip that would cover one up before it stays down until its next wake.
            placed.Sort((a, b) => a.h.since.CompareTo(b.h.since));
            int up = 0;
            for (int i = 0; i < placed.Count; i++)
            {
                var (chip, at) = placed[i];
                bool covers = false;
                for (int j = 0; j < up && !covers; j++)
                {
                    var (other, there) = placed[j];
                    covers = Mathf.Abs(at.x - there.x) < (chip.rect.rect.width + other.rect.rect.width) / 2 + 4 &&
                        Mathf.Abs(at.y - there.y) < (chip.rect.rect.height + other.rect.rect.height) / 2 + 3;
                }
                if (covers) { chip.wakeAt = chip.since = -9; chip.rect.gameObject.SetActive(false); continue; }
                placed[up++] = (chip, at); chip.rect.anchoredPosition = at;
                if (!chip.rect.gameObject.activeSelf) chip.rect.gameObject.SetActive(true);
            }
            placed.Clear();
            foreach (var id in goneHolograms)
            {
                var h = holograms[id]; holograms.Remove(id);
                h.rect.gameObject.SetActive(false); spareHolograms.Add(h);
            }
            actedAt.Clear();
        }
        readonly List<(Hologram h, Vector2 at)> placed = new List<(Hologram, Vector2)>();
        Hologram MakeHologram()
        {
            if (!statusLayer) BuildStatusLayer();
            var plate = new GameObject("Code hologram", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline), typeof(CanvasGroup));
            var rect = (RectTransform)plate.transform; rect.SetParent(statusLayer, false);
            rect.anchorMin = rect.anchorMax = ((RectTransform)canvasRoot.transform).pivot; rect.pivot = new Vector2(.5f, 0);
            var back = UITheme.Style(plate.GetComponent<Image>(), UITheme.Chip, HoloBack); back.pixelsPerUnitMultiplier = 1.7f; back.raycastTarget = false;
            var edge = plate.GetComponent<Outline>(); edge.effectColor = HoloEdge; edge.effectDistance = new Vector2(1.5f, -1.5f);
            var layout = plate.GetComponent<HorizontalLayoutGroup>(); layout.padding = new RectOffset(10, 14, 5, 7); layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleCenter; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = plate.GetComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // ▶ the line being run.
            var mark = new GameObject("Run", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
            mark.rectTransform.SetParent(rect, false); mark.sprite = PlayIcon; mark.raycastTarget = false;
            var size = mark.GetComponent<LayoutElement>(); size.preferredWidth = size.preferredHeight = 13;
            var text = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(rect, false);
            text.font = UITheme.Bold ? UITheme.Bold : rewardItems.font; text.fontSize = 19; text.color = HoloText;
            text.alignment = TextAlignmentOptions.MidlineLeft; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false; text.richText = true;
            // The tail under the chip, pointing down at the robot.
            var tail = new GameObject("Tail", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
            tail.GetComponent<LayoutElement>().ignoreLayout = true;
            tail.rectTransform.SetParent(rect, false); tail.sprite = PlayIcon; tail.color = HoloBack; tail.raycastTarget = false;
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(.5f, 0); tail.rectTransform.pivot = new Vector2(.5f, 1);
            tail.rectTransform.sizeDelta = new Vector2(16, 16); tail.rectTransform.anchoredPosition = new Vector2(0, 5); tail.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            plate.SetActive(false);
            var fade = plate.GetComponent<CanvasGroup>(); fade.blocksRaycasts = fade.interactable = false;
            return new Hologram { rect = rect, back = back, mark = mark, text = text, edge = edge, fade = fade };
        }
        Sprite PlayIcon
        {
            get
            {
                if (!playTriangle) playTriangle = playIcon;
                if (!playTriangle) playTriangle = Sprite.Create(PlayTriangleTexture(), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
                return playTriangle;
            }
        }
        // One line of the function in Python, coloured like the code panel's text: keywords pink, calls yellow.
        internal static string Line(CodeBlock b)
        {
            const string K = "<color=#FF8FC8>", F = "<color=#FFE066>", X = "</color>";
            string Short(string text) => text.Length > 26 ? text.Substring(0, 25) + "…" : text;
            string E(int i) => i < b.Arguments.Count ? Short(PythonTreeCompiler.ExpressionText(b.Arguments[i])) : "";
            switch (b.Kind)
            {
                case BlockKind.While: return K + "while" + X + " True:";
                case BlockKind.For: return K + "for" + X + " i " + K + "in" + X + " " + F + "range" + X + "(" + E(0) + "):";
                case BlockKind.ForEach: return K + "for" + X + " " + b.Value + " " + K + "in" + X + " " + F + "get_enemies" + X + "():";
                case BlockKind.DeclareVariable: return b.Value + " = " + E(0);
                case BlockKind.Wait: return F + "wait" + X + "(" + E(0) + ")";
            }
            string label = PythonTreeCompiler.Label(b.Kind);
            if (PythonTreeCompiler.HasEnemyArgument(b.Kind)) return F + label.Replace("()", "") + X + "(" + E(0) + ")";
            bool condition;
            try { condition = PythonTreeCompiler.OutputSlot(b.Kind) == BlockSlotKind.Condition; }
            catch (System.FormatException) { condition = false; } // a block kind added later: shown by its label
            return condition ? K + "if" + X + " " + Short(PythonTreeCompiler.ExpressionText(b)) + ":" : F + label + X;
        }

        // ---- Coins: a gold coin with "+n" pops where the enemy fell and arcs into the header's coin count, which bumps.
        sealed class Coin { public RectTransform rect; public TextMeshProUGUI amount; public Vector2 from; public float born; }
        readonly List<Coin> coins = new List<Coin>();
        float coinLandedAt = -9;
        const float CoinSeconds = .75f;
        void FlyCoin(System.Numerics.Vector2 position, int amount)
        {
            if (!floaterLayer) BuildFloaterLayer();
            var canvas = (RectTransform)canvasRoot.transform;
            Coin coin = null;
            foreach (var c in coins) if (!c.rect.gameObject.activeSelf) { coin = c; break; }
            if (coin == null && coins.Count < 16) coins.Add(coin = MakeCoin());
            if (coin == null) { coin = coins[0]; foreach (var c in coins) if (c.born < coin.born) coin = c; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, battleCamera.WorldToScreenPoint(DefenseViews.World(position, EnemyHeadHeight)), null, out coin.from);
            coin.amount.text = "+" + amount; coin.born = Time.unscaledTime;
            coin.rect.anchoredPosition = coin.from; coin.rect.gameObject.SetActive(true);
        }
        Coin MakeCoin()
        {
            var face = new GameObject("Coin", typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<Image>();
            face.rectTransform.SetParent(floaterLayer, false); face.rectTransform.sizeDelta = new Vector2(30, 30);
            UITheme.Style(face, UITheme.Circle, UITheme.Sunny); face.raycastTarget = false;
            var rim = face.GetComponent<Outline>(); rim.effectColor = new Color(.55f, .3f, 0, .9f); rim.effectDistance = new Vector2(2, -2);
            var shine = new GameObject("Shine", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shine.rectTransform.SetParent(face.rectTransform, false); shine.rectTransform.sizeDelta = new Vector2(12, 12); shine.rectTransform.anchoredPosition = new Vector2(-5, 5);
            UITheme.Style(shine, UITheme.Circle, new Color(1, 1, 1, .75f)); shine.raycastTarget = false;
            var label = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(face.rectTransform, false); label.rectTransform.sizeDelta = new Vector2(80, 34);
            label.rectTransform.anchoredPosition = new Vector2(52, 0);
            label.alignment = TextAlignmentOptions.MidlineLeft; label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
            if (UITheme.Display && UITheme.Label) { label.font = UITheme.Display; label.fontSharedMaterial = UITheme.Label; } else label.font = rewardItems.font;
            label.fontSize = 28; label.color = new Color(1, .85f, .4f);
            face.gameObject.SetActive(false);
            return new Coin { rect = face.rectTransform, amount = label };
        }
        void AnimateCoins()
        {
            if (coins.Count == 0 || !coinCounter) return;
            var canvas = (RectTransform)canvasRoot.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, RectTransformUtility.WorldToScreenPoint(null, coinCounter.transform.position), null, out var to);
            to += Vector2.left * 34; // the coin goes into the count's left side, where the word 코인 ends
            float now = Time.unscaledTime;
            foreach (var c in coins)
            {
                if (!c.rect.gameObject.activeSelf) continue;
                float k = (now - c.born) / CoinSeconds;
                if (k >= 1) { c.rect.gameObject.SetActive(false); coinLandedAt = now; continue; }
                // A short hop up where it fell (the first fifth), then an arc into the counter, shrinking.
                float hop = Mathf.Clamp01(k / .2f), fly = Mathf.Clamp01((k - .2f) / .8f), e = fly * fly * (3 - 2 * fly);
                var start = c.from + Vector2.up * (40 * Mathf.Sin(hop * Mathf.PI * .5f));
                var mid = Vector2.Lerp(start, to, .5f) + Vector2.up * 140;
                var p = Vector2.Lerp(Vector2.Lerp(start, mid, e), Vector2.Lerp(mid, to, e), e);
                c.rect.anchoredPosition = p;
                c.rect.localScale = Vector3.one * (hop < 1 ? .6f + .6f * hop : Mathf.Lerp(1.2f, .7f, e));
                c.amount.alpha = 1 - fly;
            }
            float bump = Mathf.Clamp01(1 - (now - coinLandedAt) / .25f);
            coinCounter.transform.localScale = Vector3.one * (1 + .3f * bump * bump);
        }

        // ---- The wave call's band: a dark strip with gold edges opens across the screen behind the words, then closes.
        Image waveBand;
        RectTransform WaveBand()
        {
            if (waveBand) return waveBand.rectTransform;
            waveBand = new GameObject("Wave band", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = waveBand.rectTransform; rect.SetParent(canvasRoot.transform, false);
            rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(0, 230); rect.anchoredPosition = new Vector2(0, 104);
            waveBand.color = new Color(.06f, .09f, .16f, .78f); waveBand.raycastTarget = false;
            foreach (float y in new[] { 0f, 1f })
            {
                var line = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                line.rectTransform.SetParent(rect, false); line.raycastTarget = false;
                line.rectTransform.anchorMin = new Vector2(0, y); line.rectTransform.anchorMax = new Vector2(1, y); line.rectTransform.pivot = new Vector2(.5f, y);
                line.rectTransform.sizeDelta = new Vector2(0, 5);
            }
            waveBand.gameObject.SetActive(false);
            return rect;
        }
        void AnimateWaveBand(float t, bool shown, Color accent)
        {
            if (!waveBand) return;
            waveBand.gameObject.SetActive(shown);
            if (!shown) return;
            float open = t < .16f ? Mathf.SmoothStep(0, 1, t / .16f) : t > WaveCallSeconds - .3f ? Mathf.SmoothStep(1, 0, (t - (WaveCallSeconds - .3f)) / .3f) : 1;
            waveBand.rectTransform.localScale = new Vector3(1, open, 1);
            foreach (Transform edge in waveBand.transform) if (edge.TryGetComponent<Image>(out var line)) line.color = new Color(accent.r, accent.g, accent.b, .9f);
        }

        // ---- The running pill's dots light one after another (a function at work).
        static readonly string[] Dots =
        {
            "<voffset=-0.14em><alpha=#FF>·<alpha=#77>··</voffset>", "<voffset=-0.14em><alpha=#77>·<alpha=#FF>·<alpha=#77>·</voffset>",
            "<voffset=-0.14em><alpha=#77>··<alpha=#FF>·</voffset>",
        };
        static string RunningDots() => Dots[(int)(Time.unscaledTime * 5) % 3];
    }
}
