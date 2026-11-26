using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // The team board on screen (stage 4): a small panel of its own, apart from the robots' code panels. Each board name is a
    // row: a list as cards (an append slides a card in from the robot that added it, a pop flies it to the robot that took
    // it; a dead enemy's card stays, greyed, until it is popped), a dict as a small table, anything else as its value. On
    // the field every enemy in a board list wears a numbered reticle (its place in the list, 1 first) and a robot working
    // on a job it popped draws a thin line to it. Built at run time on its own canvas: no scene or prefab of its own.
    public sealed class DefenseTeamBoardView : MonoBehaviour
    {
        DefenseBattle battle;
        RectTransform panel, rows; Button edit; TMP_Text hint;
        Canvas canvas;
        string shownSource;
        sealed class Row { public string Name; public RectTransform Root, Content; public TMP_Text Value; public readonly List<Card> Cards = new List<Card>(); public object List; }
        sealed class Card { public RectTransform Rect; public Image Face; public TMP_Text Label, Number, Cross; public DefenseProgram.Val Item; public float Fly = -1; public Vector3 Goal; }
        readonly List<Row> rowViews = new List<Row>();
        readonly List<Card> flying = new List<Card>(), spare = new List<Card>();
        readonly Dictionary<int, EnemyType> kinds = new Dictionary<int, EnemyType>();
        int notesSeen;
        const float Width = 380, CardWidth = 52, CardGap = 6, CardHeight = 60;

        public static void Attach(DefenseBattle battle) { if (battle && !battle.GetComponent<DefenseTeamBoardView>()) battle.gameObject.AddComponent<DefenseTeamBoardView>().battle = battle; }
        DefenseSimulation Sim => battle ? battle.Simulation : null;
        // The board shows from the stage that hands out its blocks (4), or once anything is on it.
        bool Shown => Sim != null && (battle.Stage >= 4 || !Sim.Board.IsEmpty) && Sim.Phase != BattlePhase.Victory && Sim.Phase != BattlePhase.Defeat &&
            Sim.Phase != BattlePhase.Reward && !battle.CodeEditor.IsOpen;

        void Build()
        {
            canvas = new GameObject("Team board", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            panel = Box("Panel", (RectTransform)canvas.transform, UITheme.Card, new Color(UITheme.Paper.r, UITheme.Paper.g, UITheme.Paper.b, .96f));
            // Bottom left, over the desk's corner: the left middle covered the enemies' gate (play capture 2026-11-26).
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 0); panel.anchoredPosition = new Vector2(18, 64);
            var title = Text("Title", panel, "팀 보드", 26, UITheme.Ink, UITheme.Display); Place(title.rectTransform, 18, 12, 200, 34);
            var sub = Text("Subtitle", panel, "모든 로봇이 함께 쓰는 변수", 15, UITheme.Muted, UITheme.Bold); Place(sub.rectTransform, 122, 20, 200, 22);
            edit = Box("Edit", panel, UITheme.Button, UITheme.Sky).gameObject.AddComponent<Button>();
            var editRect = (RectTransform)edit.transform; editRect.anchorMin = editRect.anchorMax = editRect.pivot = new Vector2(1, 1);
            editRect.anchoredPosition = new Vector2(-14, -12); editRect.sizeDelta = new Vector2(70, 34);
            var editLabel = Text("Label", editRect, "편집", 18, UITheme.Ink, UITheme.Display); Stretch(editLabel.rectTransform); editLabel.alignment = TextAlignmentOptions.Center;
            edit.onClick.AddListener(() => { if (Sim != null && Sim.CanEdit) battle.CodeEditor.OpenBoard(); });
            rows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>(); rows.SetParent(panel, false);
            Place(rows, 14, 56, Width - 28, 10);
            hint = Text("Hint", panel, "편집을 눌러 jobs = [] 처럼\n모든 로봇이 함께 쓸 변수를 만들어요.", 16, UITheme.Muted, UITheme.Bold); Place(hint.rectTransform, 18, 58, Width - 36, 50);
        }
        static RectTransform Box(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false); UITheme.Style(image, sprite, color); return image.rectTransform;
        }
        static TMP_Text Text(string name, RectTransform parent, string text, float size, Color color, TMP_FontAsset font)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false); if (font) label.font = font;
            label.text = text; label.fontSize = size; label.color = color; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
        static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        // Rows follow the board's names (rebuilt when its code changes); their contents follow the values every frame.
        void Rebuild()
        {
            foreach (var row in rowViews) { foreach (var card in row.Cards) Recycle(card); Destroy(row.Root.gameObject); }
            rowViews.Clear();
            float y = 0;
            foreach (var name in Sim.Board.Names)
            {
                var row = new Row { Name = name, Root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>() };
                row.Root.SetParent(rows, false);
                var label = Text("Name", row.Root, name, 20, new Color(.1f, .44f, .76f), UITheme.Bold); Place(label.rectTransform, 0, 0, Width - 28, 26);
                row.Value = Text("Value", row.Root, "", 17, UITheme.Ink, UITheme.Bold); Place(row.Value.rectTransform, 0, 26, Width - 28, 24);
                row.Content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); row.Content.SetParent(row.Root, false);
                Place(row.Content, 0, 28, Width - 28, CardHeight);
                Place(row.Root, 0, y, Width - 28, 0);
                rowViews.Add(row);
                y += 26 + Height(name);
            }
            shownSource = Sim.Board.Source;
        }
        float Height(string name) => Sim.Board.Variables.TryGetValue(name, out var v) ? v.Kind == DefenseProgram.ValueKind.List ? CardHeight + 12 :
            v.Kind == DefenseProgram.ValueKind.Dict ? 22 * Mathf.Max(1, v.Table.Keys.Count) + 10 : 30 : 30;

        void LateUpdate()
        {
            if (!battle) return;
            if (!panel) Build();
            bool shown = Shown;
            canvas.gameObject.SetActive(shown);
            if (!shown) { ClearField(); return; }
            var board = Sim.Board;
            if (shownSource != board.Source || rowViews.Count != board.Names.Count) Rebuild();
            edit.interactable = Sim.CanEdit;
            hint.gameObject.SetActive(board.IsEmpty);
            // A new wave runs the board again: its notes start over.
            if (board.Notes.Count < notesSeen) notesSeen = 0;
            for (; notesSeen < board.Notes.Count; notesSeen++) Play(board.Notes[notesSeen]);
            float y = 0, dt = Time.unscaledDeltaTime;
            foreach (var row in rowViews)
            {
                row.Root.anchoredPosition = new Vector2(0, -y);
                bool made = board.Variables.TryGetValue(row.Name, out var value);
                bool list = made && value.Kind == DefenseProgram.ValueKind.List;
                row.Value.text = !made ? "웨이브가 시작되면 만들어져요" : list ? (value.Items.Count == 0 ? "[ ]  비어 있음" : $"len {value.Items.Count}") : Describe(value);
                row.Value.rectTransform.anchoredPosition = new Vector2(list ? 110 : 0, list ? 0 : -26);
                if (list) Sync(row, value.Items, dt); else { foreach (var card in row.Cards) Recycle(card); row.Cards.Clear(); }
                float height = list ? CardHeight + 12 : value.Kind == DefenseProgram.ValueKind.Dict ? 22 * Mathf.Max(1, value.Table.Keys.Count) + 10 : 30;
                row.Value.rectTransform.sizeDelta = new Vector2(Width - 28 - (list ? 110 : 0), list ? 24 : height);
                y += 26 + height;
            }
            panel.sizeDelta = new Vector2(Width, 66 + Mathf.Max(board.IsEmpty ? 56 : 0, y));
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var card = flying[i]; card.Fly += dt / .5f;
                float t = Mathf.Clamp01(card.Fly);
                card.Rect.position = Vector3.Lerp(card.Rect.position, card.Goal, 1 - Mathf.Exp(-dt * 9));
                card.Rect.localScale = Vector3.one * Mathf.Lerp(1.15f, .35f, t);
                SetAlpha(card, 1 - t * t);
                if (t >= 1) { flying.RemoveAt(i); Recycle(card); }
            }
            Field();
        }
        static string Describe(DefenseProgram.Val v)
        {
            switch (v.Kind)
            {
                case DefenseProgram.ValueKind.Dict:
                    return string.Join("\n", v.Table.Keys.Select((k, i) => $"<color={UITheme.CodeHex}>{Key(k)}</color>  →  {Number(v.Table.Values[i])}"));
                case DefenseProgram.ValueKind.Number: return "= " + Number(v);
                case DefenseProgram.ValueKind.Truth: return v.Number != 0 ? "= True" : "= False";
                case DefenseProgram.ValueKind.Kind: case DefenseProgram.ValueKind.Role: return "= " + Key(v);
                default: return "(웨이브가 시작되면 채워져요)";
            }
        }
        static string Number(DefenseProgram.Val v) => v.Kind == DefenseProgram.ValueKind.Number ? v.Number.ToString("0.##") : Key(v);
        static string Key(DefenseProgram.Val v) => v.Kind == DefenseProgram.ValueKind.Kind ? "Enemy." + Name(PythonTreeCompiler.EnemyTypes, v.Number) :
            v.Kind == DefenseProgram.ValueKind.Role ? "Robot." + Name(PythonTreeCompiler.RobotRoles, v.Number) : v.Number.ToString("0.##");
        static string Name(string[] names, double i) => i >= 0 && i < names.Length ? names[(int)i] : "?";

        // A note as it happened: an append's card starts at the robot that appended, a pop's leaves for the robot that popped.
        void Play(DefenseTeamBoard.Note note)
        {
            var row = rowViews.FirstOrDefault(r => Sim.Board.Variables.TryGetValue(r.Name, out var v) && v.Ref == note.List);
            if (row == null) return;
            if (note.Kind == "append")
            {
                var card = Take(row, note.Item);
                card.Rect.position = RobotScreen(note.Robot) ?? row.Content.TransformPoint(new Vector3(Width, -CardHeight / 2));
                row.Cards.Add(card);
            }
            else if (note.Kind == "pop")
            {
                int at = row.Cards.FindIndex(c => DefenseProgram.Same(c.Item, note.Item));
                if (at < 0) return;
                var card = row.Cards[at]; row.Cards.RemoveAt(at);
                card.Rect.SetParent(canvas.transform, true); card.Fly = 0;
                card.Goal = RobotScreen(note.Robot) ?? card.Rect.position + Vector3.down * 200;
                flying.Add(card);
            }
        }
        Vector3? RobotScreen(int id)
        {
            var bot = Sim.FindRobot(id); var camera = battle.BattleCamera;
            if (bot == null || !camera) return null;
            var p = camera.WorldToScreenPoint(DefenseViews.World(bot.Position, 1.4f));
            return p.z > 0 ? p : (Vector3?)null;
        }
        // The cards as the list stands: any change the notes did not explain (a new wave, a list assigned anew) is redrawn as is.
        void Sync(Row row, List<DefenseProgram.Val> items, float dt)
        {
            bool same = row.Cards.Count == items.Count && !row.Cards.Where((c, i) => !DefenseProgram.Same(c.Item, items[i])).Any();
            if (!same)
            {
                foreach (var card in row.Cards) Recycle(card);
                row.Cards.Clear();
                foreach (var item in items) { var card = Take(row, item); card.Rect.anchoredPosition = Slot(row.Cards.Count, items.Count); row.Cards.Add(card); }
            }
            for (int i = 0; i < row.Cards.Count; i++)
            {
                var card = row.Cards[i]; var goal = Slot(i, row.Cards.Count);
                card.Rect.anchoredPosition = Vector2.Lerp(card.Rect.anchoredPosition, goal, 1 - Mathf.Exp(-dt * 10));
                card.Rect.localScale = Vector3.one * Mathf.Lerp(card.Rect.localScale.x, 1, 1 - Mathf.Exp(-dt * 10));
                card.Number.text = (i + 1).ToString();
                Dress(card);
            }
        }
        static Vector2 Slot(int i, int count)
        {
            float step = count <= 1 ? 0 : Mathf.Min(CardWidth + CardGap, (Width - 28 - CardWidth) / (count - 1));
            return new Vector2(i * step, 0);
        }
        Card Take(Row row, DefenseProgram.Val item)
        {
            Card card;
            if (spare.Count > 0) { card = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1); card.Rect.gameObject.SetActive(true); }
            else
            {
                card = new Card { Rect = Box("Card", row.Content, UITheme.Card, Color.white) };
                card.Face = card.Rect.GetComponent<Image>();
                card.Label = Text("Kind", card.Rect, "", 15, UITheme.Ink, UITheme.Display); Stretch(card.Label.rectTransform); card.Label.alignment = TextAlignmentOptions.Center;
                card.Label.rectTransform.offsetMin = new Vector2(0, 4);
                card.Number = Text("Number", card.Rect, "", 17, UITheme.Ink, UITheme.Display); Place(card.Number.rectTransform, 6, 1, 22, 20);
                card.Cross = Text("Dead", card.Rect, "✕", 34, new Color(.85f, .15f, .15f, .9f), UITheme.Bold); Stretch(card.Cross.rectTransform); card.Cross.alignment = TextAlignmentOptions.Center;
            }
            card.Rect.SetParent(row.Content, false);
            card.Rect.anchorMin = card.Rect.anchorMax = card.Rect.pivot = new Vector2(0, 1);
            card.Rect.sizeDelta = new Vector2(CardWidth, CardHeight); card.Rect.localScale = Vector3.one * 1.15f;
            card.Item = item; card.Fly = -1; SetAlpha(card, 1);
            Dress(card);
            return card;
        }
        void Recycle(Card card) { if (!card.Rect) return; card.Rect.gameObject.SetActive(false); spare.Add(card); }
        static void SetAlpha(Card card, float a) { foreach (var g in card.Rect.GetComponentsInChildren<Graphic>(true)) g.canvasRenderer.SetAlpha(a); }
        // An enemy's card: its kind's colour and short name; greyed with a cross once it is dead (it stays until popped).
        void Dress(Card card)
        {
            var item = card.Item;
            if (item.Kind == DefenseProgram.ValueKind.Enemy)
            {
                var enemy = Sim.Enemies.FirstOrDefault(e => e.Id == item.Enemy && e.Active);
                if (enemy != null) kinds[item.Enemy] = enemy.Spec.type;
                bool known = kinds.TryGetValue(item.Enemy, out var type);
                card.Face.color = enemy == null ? new Color(.62f, .62f, .66f) : Tint(type);
                card.Label.text = known ? Short(type) : "적";
                card.Cross.enabled = enemy == null;
            }
            else
            {
                card.Face.color = item.Kind == DefenseProgram.ValueKind.Robot ? UITheme.Sky : UITheme.PaperDeep;
                card.Label.text = item.Kind == DefenseProgram.ValueKind.Robot ? "로봇" : Key(item);
                card.Cross.enabled = item.Kind == DefenseProgram.ValueKind.Robot && Sim.FindRobot(item.Enemy) == null;
            }
        }
        static string Short(EnemyType type) => type switch { EnemyType.Healer => "힐러", EnemyType.Shielder => "보호막", EnemyType.Fast => "빠른", EnemyType.Tank => "탱커",
            EnemyType.Queue => "큐", EnemyType.Stack => "스택", EnemyType.Reflector => "반사", EnemyType.Tree => "트리", _ => "일반" };
        // The colours the field gives them (DefenseActorView's tints), so a card and its enemy match at a glance.
        public static Color Tint(EnemyType type) => type switch { EnemyType.Healer => new Color(.35f, .95f, .45f), EnemyType.Shielder => new Color(.45f, .65f, 1),
            EnemyType.Queue => new Color(1, .65f, .3f), EnemyType.Stack => new Color(.78f, .55f, 1), EnemyType.Reflector => new Color(.6f, .75f, .95f),
            EnemyType.Tree => new Color(.55f, .85f, .5f), EnemyType.Fast => new Color(1, .85f, .4f), EnemyType.Tank => new Color(.95f, .55f, .5f), _ => new Color(.95f, .9f, .8f) };

        // On the field: numbered reticles on the enemies in board lists and a line from each robot to the job it popped.
        readonly List<LineRenderer> rings = new List<LineRenderer>(), lasers = new List<LineRenderer>();
        readonly List<TextMeshPro> numbers = new List<TextMeshPro>();
        static Material lineMaterial;
        void Field()
        {
            var camera = battle.BattleCamera; var board = Sim.Board; int used = 0, beams = 0;
            var turn = camera ? camera.transform.rotation : Quaternion.identity;
            foreach (var e in Sim.Enemies)
            {
                if (!e.Active) continue;
                int at = board.IndexOf(e.Id, out _);
                if (at < 0) continue;
                var ring = Ring(used); var label = numbers[used]; used++;
                var foot = DefenseViews.World(e.Position, .08f);
                float pulse = 1 + .08f * Mathf.Sin(Time.unscaledTime * 6 + e.Id);
                ring.transform.SetPositionAndRotation(foot, Quaternion.Euler(0, Time.unscaledTime * 60, 0)); ring.transform.localScale = Vector3.one * pulse;
                label.text = (at + 1).ToString();
                label.transform.SetPositionAndRotation(DefenseViews.World(e.Position, 2.6f), turn);
            }
            foreach (var bot in Sim.Robots)
            {
                var job = bot.Job == 0 ? null : Sim.Enemies.FirstOrDefault(e => e.Id == bot.Job && e.Active);
                if (job == null) continue;
                var beam = Laser(beams++);
                var hit = DefenseSimulation.WeakPoint(job);
                beam.SetPosition(0, DefenseViews.World(bot.Position, 1.1f)); beam.SetPosition(1, DefenseViews.World(hit.Position, .9f));
                var c = new Color(1, .42f, .42f, .55f + .35f * Mathf.Sin(Time.unscaledTime * 10)); beam.startColor = c; beam.endColor = c;
            }
            for (int i = used; i < rings.Count; i++) { rings[i].gameObject.SetActive(false); numbers[i].gameObject.SetActive(false); }
            for (int i = beams; i < lasers.Count; i++) lasers[i].gameObject.SetActive(false);
        }
        void ClearField()
        {
            foreach (var r in rings) r.gameObject.SetActive(false);
            foreach (var n in numbers) n.gameObject.SetActive(false);
            foreach (var l in lasers) l.gameObject.SetActive(false);
        }
        static LineRenderer Line(string name, int points, float width, Color color)
        {
            if (!lineMaterial) lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Team board lines" };
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial; line.positionCount = points; line.widthMultiplier = width;
            line.startColor = line.endColor = color; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            line.numCapVertices = 2;
            return line;
        }
        LineRenderer Ring(int i)
        {
            while (rings.Count <= i)
            {
                var ring = Line("Job reticle", 33, .12f, new Color(1, .84f, .15f, 1)); ring.useWorldSpace = false; ring.loop = false;
                for (int p = 0; p < 33; p++)
                {
                    float a = p / 32f * Mathf.PI * 2; float r = p % 8 < 6 ? .95f : .7f; // a ring with four notches, like a sight
                    ring.SetPosition(p, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                }
                ring.transform.SetParent(transform, false); rings.Add(ring);
                var number = new GameObject("Job number", typeof(TextMeshPro)).GetComponent<TextMeshPro>(); number.transform.SetParent(transform, false);
                if (UITheme.Display && UITheme.Label) { number.font = UITheme.Display; number.fontSharedMaterial = UITheme.Label; }
                number.fontSize = 11; number.alignment = TextAlignmentOptions.Center; number.color = new Color(1, .86f, .2f); number.rectTransform.sizeDelta = new Vector2(3, 1.6f);
                numbers.Add(number);
            }
            rings[i].gameObject.SetActive(true); numbers[i].gameObject.SetActive(true);
            return rings[i];
        }
        LineRenderer Laser(int i)
        {
            while (lasers.Count <= i) { var beam = Line("Job line", 2, .045f, new Color(1, .4f, .4f, .8f)); beam.transform.SetParent(transform, false); lasers.Add(beam); }
            lasers[i].gameObject.SetActive(true);
            return lasers[i];
        }
        void OnDestroy() { if (canvas) Destroy(canvas.gameObject); }
    }
}
