using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // Developer tools (developer rights only, GameFlow.Developer) in every scene, on GameFlow's persistent root. F1: boxes that
    // jump to a dialogue id ("1-3.C05") or to a stage-wave's start ("2-4"); in the battle they sit in the battle's own F1
    // panel's empty bottom strip, elsewhere F1 shows them alone and Go starts the Defense scene for the id's stage, as the
    // stage map does, and makes the jump once the battle has started. F2: the balance window, where each enemy group's count,
    // timing, health, speed and core damage (as the campaign asset has them, before the stage's scaling) are changed for a
    // stage-wave until the game quits: in the battle the one on screen, elsewhere the one picked in the window.
    // Plain IMGUI: a team tool, not game UI.
    public sealed class DefenseDevWindow : MonoBehaviour
    {
        public static DefenseDevWindow Instance { get; private set; }
        // Set by the battle as it starts; gone (Unity null) with its scene.
        public DefenseBattle Battle { get; set; }
        bool jumpOpen, balanceOpen;
        string jumpId = "1-1.A01", waveId = "2-4", message;
        (bool wave, string id)? pending; // a jump asked for outside the battle, made once the Defense scene's battle has started
        int pickStage = 1, pickWave = 1; // the stage-wave the balance window edits outside the battle
        DefenseCampaignDefinition campaign;
        SpawnGroup[] picked; (int stage, int wave) pickedFor;
        string[][] fields;
        (int stage, int wave) fieldsFor;
        Rect jumpArea, balanceArea;
        Image jumpBlocker, balanceBlocker;
        static readonly string[] Columns = { "수", "출현(초)", "간격(초)", "체력", "속도", "코어 피해" };

        bool InBattle => Battle && Battle.Simulation != null;
        bool JumpShown => InBattle ? Battle.DeveloperMode : jumpOpen;
        // A window of this tool is up outside the battle: the stage map leaves its developer keys alone while one is.
        public static bool Open => Instance && GameFlow.Developer && (Instance.balanceOpen || Instance.jumpOpen && !Instance.InBattle);

        void Awake() => Instance = this;

        void Update()
        {
            if (!GameFlow.Developer) { jumpOpen = balanceOpen = false; pending = null; }
            var keyboard = Keyboard.current;
            if (GameFlow.Developer && keyboard != null)
            {
                if (keyboard.f2Key.wasPressedThisFrame) { balanceOpen = !balanceOpen; fields = null; message = null; }
                // In the battle F1 is the battle's own panel (DefenseBattle.Update).
                if (keyboard.f1Key.wasPressedThisFrame && !Battle) { jumpOpen = !jumpOpen; message = null; }
            }
            if (pending.HasValue && InBattle) { var (wave, id) = pending.Value; pending = null; message = Jump(wave, id); }
            float width = Screen.width / (Screen.height / 1080f);
            jumpArea = new Rect(width / 2 - 640, 908, 920, 52);
            var groups = balanceOpen ? Groups(out _, out _) : null;
            balanceArea = new Rect(width / 2 - 600, 180, 1200, 176 + 44 * (groups?.Length ?? 0));
            Block(ref jumpBlocker, JumpShown && GameFlow.Developer, jumpArea);
            Block(ref balanceBlocker, groups != null, balanceArea);
        }
        // An invisible image on GameFlow's canvas (the topmost) under each open window, so a click on it reaches no button
        // or map under it (the stage map's and the battle's world clicks skip a pointer over UI).
        void Block(ref Image blocker, bool on, Rect area)
        {
            if (!blocker)
            {
                if (!on) return;
                blocker = new GameObject("DevWindowBlocker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                blocker.rectTransform.SetParent(transform, false);
                blocker.rectTransform.anchorMin = blocker.rectTransform.anchorMax = Vector2.zero; blocker.rectTransform.pivot = new Vector2(0, 1);
                blocker.color = Color.clear;
            }
            blocker.gameObject.SetActive(on);
            if (!on || !TryGetComponent(out Canvas canvas)) return;
            float scale = Screen.height / 1080f / canvas.scaleFactor;
            blocker.rectTransform.anchoredPosition = new Vector2(area.x * scale, (1080 - area.y) * scale);
            blocker.rectTransform.sizeDelta = area.size * scale;
        }

        void OnGUI()
        {
            if (!GameFlow.Developer) return;
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.skin.label.fontSize = GUI.skin.textField.fontSize = GUI.skin.button.fontSize = GUI.skin.box.fontSize = 20;
            // The battle's F1 panel's bottom strip is empty between "수량 1~999" and "전체 추가" (1080p: x 300 to 1350, y 905 to 965).
            if (JumpShown)
            {
                if (!InBattle) GUI.Box(new Rect(jumpArea.x - 8, jumpArea.y, jumpArea.width + 16, jumpArea.height), "");
                JumpBox(jumpArea);
            }
            if (balanceOpen && Groups(out int stage, out int wave) is SpawnGroup[] groups) BalanceWindow(balanceArea, groups, stage, wave);
        }
        // The balance window takes the clicks over it (the battle would place a robot under it otherwise).
        public bool HasPointer(Vector2 screen)
        {
            if (!balanceOpen) return false;
            float scale = Screen.height / 1080f;
            return balanceArea.Contains(new Vector2(screen.x, Screen.height - screen.y) / scale);
        }

        void JumpBox(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y + 10, 140, 34), "Dialogue ID");
            jumpId = GUI.TextField(new Rect(area.x + 140, area.y + 8, 190, 36), jumpId);
            if (GUI.Button(new Rect(area.x + 336, area.y + 8, 60, 36), "Go")) message = Jump(false, jumpId);
            GUI.Label(new Rect(area.x + 420, area.y + 10, 50, 34), "S-W");
            waveId = GUI.TextField(new Rect(area.x + 470, area.y + 8, 90, 36), waveId);
            if (GUI.Button(new Rect(area.x + 566, area.y + 8, 60, 36), "Go")) message = Jump(true, waveId);
            GUI.Label(new Rect(area.x + 640, area.y + 10, 280, 34), "예: 1-3.C05 · 1-3 / 2-4");
            // Notices run long ("캐시 없음: Tools/Develop/캐시 제작으로 기록하세요"): a line of their own over the strip.
            if (!string.IsNullOrEmpty(message)) GUI.Box(new Rect(area.x, area.y - 44, area.width, 38), message);
        }
        // In the battle the battle jumps itself. Elsewhere the id's first number is the stage ("1-3.C05", "2-4"): the Defense
        // scene starts it as the stage map does (StageProgress.SelectedStage, so the battle counts as entered from the map),
        // and the jump is made once its battle has started.
        string Jump(bool wave, string id)
        {
            if (InBattle) return wave ? Battle.DeveloperJumpWave(id) : Battle.DeveloperJump(id);
            if (!int.TryParse((id ?? "").Trim().Split('-')[0], out int stage) || stage < 1 || stage > DefenseBattle.PrototypeLastStage)
                return "없는 스테이지: " + id;
            if (!GameFlow.LoadScene(GameFlow.DefenseScene)) return "장면을 바꾸는 중입니다. 잠시 뒤 다시 누르세요.";
            StageMap.StageProgress.SelectedStage = stage;
            pending = (wave, id); jumpOpen = false;
            return "전투를 불러오는 중 · " + id;
        }

        // The groups the balance window edits: the battle's stage-wave on screen, or outside the battle the picked one as the
        // campaign asset has it, with the edits (wave from 0). Null without the campaign.
        SpawnGroup[] Groups(out int stage, out int wave)
        {
            if (InBattle) { var sim = Battle.Simulation; stage = sim.Setup.Stage; wave = sim.WaveIndex; return sim.Setup.Waves[wave]; }
            stage = pickStage; wave = pickWave - 1;
            if (!campaign) campaign = Resources.Load<DefenseCampaignLink>("DefenseCampaign") is DefenseCampaignLink link ? link.campaign : null;
            if (!campaign) return null;
            if (picked == null || pickedFor != (stage, wave)) { picked = DefenseBattle.ReadWaves(campaign, stage)[wave]; pickedFor = (stage, wave); }
            return picked;
        }
        EnemyDefinition Enemy(int index) => InBattle ? Battle.EnemyAt(index) : campaign.enemies[index];

        void BalanceWindow(Rect area, SpawnGroup[] groups, int stage, int wave)
        {
            var rules = InBattle ? Battle.Simulation.Setup.RulesConfig : campaign.rules;
            if (fields == null || fieldsFor != (stage, wave)) { fields = groups.Select(Values).ToArray(); fieldsFor = (stage, wave); }
            GUI.Box(area, $"밸런스 · {stage}-{wave + 1} (F2)");
            if (!InBattle)
            {
                pickStage = Stepper(area.x + 16, area.y + 4, "스테이지", pickStage, campaign.stages.Length);
                pickWave = Stepper(area.x + 230, area.y + 4, "웨이브", Mathf.Min(pickWave, campaign.stages[pickStage - 1].waves.Length), campaign.stages[pickStage - 1].waves.Length);
            }
            float y = area.y + 40;
            GUI.Label(new Rect(area.x + 16, y, 180, 30), "적");
            for (int c = 0; c < Columns.Length; c++) GUI.Label(new Rect(area.x + 200 + c * 104, y, 104, 30), Columns[c]);
            GUI.Label(new Rect(area.x + 830, y, 170, 30), "실제 체력·속도");
            float total = 0, last = 0; int count = 0; bool boss = false;
            for (int i = 0; i < groups.Length && i < fields.Length; i++)
            {
                y += 44;
                var group = groups[i];
                var actual = rules.Scale(group.Enemy, stage, wave);
                GUI.Label(new Rect(area.x + 16, y, 184, 36), Enemy(group.DefinitionIndex).displayName + (group.Boss ? " (보스)" : "") + (DefenseBattle.WaveGroupEdited(stage, wave, i) ? " *" : ""));
                for (int c = 0; c < Columns.Length; c++) fields[i][c] = GUI.TextField(new Rect(area.x + 200 + c * 104, y, 96, 36), fields[i][c]);
                GUI.Label(new Rect(area.x + 830, y, 170, 36), Number(actual.health) + " · " + Number(actual.speed));
                if (GUI.Button(new Rect(area.x + 1004, y, 88, 36), "Apply")) Apply(stage, wave, i);
                if (GUI.Button(new Rect(area.x + 1098, y, 88, 36), "Reset")) Set(stage, wave, i, null);
                count += group.Count; total += group.Count * actual.health;
                if (group.Boss) boss = true; else last = Mathf.Max(last, group.Delay + group.Interval * (group.Count - 1));
            }
            y += 50;
            GUI.Label(new Rect(area.x + 16, y, 1170, 34), $"합계 · 적 {count}마리 · 총 체력(실제) {Number(total)} · 마지막 출현 {Number(last)}초" + (boss ? " · 보스는 졸개 처치 후" : ""));
            y += 40;
            string state = !InBattle ? "전투 밖 · 이 웨이브를 시작하면 적용" : Battle.Simulation.Phase == BattlePhase.Ready ? "준비 중 · 바로 적용" : "전투 중 · 아직 안 나온 그룹(보스 등)은 바로 적용, 나온 그룹은 다시 시작하면 적용";
            GUI.Label(new Rect(area.x + 16, y, 1040, 34), string.IsNullOrEmpty(message) ? state + " · * 손으로 바꾼 값, Reset: 원래 값" : message);
            if (GUI.Button(new Rect(area.x + area.width - 116, y, 100, 34), "Close")) balanceOpen = false;
        }
        static int Stepper(float x, float y, string label, int value, int max)
        {
            GUI.Label(new Rect(x, y, 84, 32), label);
            if (GUI.Button(new Rect(x + 84, y, 36, 32), "<")) value--;
            GUI.Label(new Rect(x + 128, y, 30, 32), value.ToString(CultureInfo.InvariantCulture));
            if (GUI.Button(new Rect(x + 156, y, 36, 32), ">")) value++;
            return Mathf.Clamp(value, 1, max);
        }

        static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        static string[] Values(SpawnGroup g) => new[] { g.Count.ToString(CultureInfo.InvariantCulture), Number(g.Delay), Number(g.Interval),
            Number(g.Enemy.health), Number(g.Enemy.speed), g.Enemy.leakDamage.ToString(CultureInfo.InvariantCulture) };

        void Apply(int stage, int wave, int group)
        {
            var f = fields[group];
            bool Real(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (!int.TryParse(f[0], out int count) || count < 1 || !Real(f[1], out float delay) || delay < 0 || !Real(f[2], out float interval) || interval <= 0 ||
                !Real(f[3], out float health) || health <= 0 || !Real(f[4], out float speed) || speed <= 0 || !int.TryParse(f[5], out int leak) || leak < 1)
            { message = "수 1 이상, 출현 0 이상, 간격·체력·속도 0보다 크게, 코어 피해 1 이상"; return; }
            Set(stage, wave, group, new DefenseBattle.GroupEdit { count = count, delay = delay, interval = interval, health = health, speed = speed, leak = leak });
        }
        // Into the store the battle reads (DefenseBattle.StoreWaveGroup): in the battle the wave on screen takes it at once
        // when it can, outside it the stage-wave takes it when it starts.
        void Set(int stage, int wave, int group, DefenseBattle.GroupEdit edit)
        {
            bool inBattle = InBattle;
            bool now = false;
            if (inBattle) now = Battle.SetWaveGroup(stage, wave, group, edit); else DefenseBattle.StoreWaveGroup(stage, wave, group, edit);
            picked = null;
            var groups = Groups(out int s, out int w);
            if (fields != null && fieldsFor == (s, w) && groups != null) fields[group] = Values(groups[group]);
            message = (edit == null ? "원래 값으로 · " : "적용 · ") + (!inBattle ? "이 웨이브를 시작하면 들어감" : now ? "이 웨이브에 바로 들어감" : "이 웨이브를 다시 시작하면 들어감");
        }
    }
}
