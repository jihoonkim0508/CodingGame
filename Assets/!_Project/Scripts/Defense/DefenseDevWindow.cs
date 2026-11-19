using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodingGame.Defense
{
    // Developer tools drawn over the battle (developer rights only, GameFlow.Developer): with the F1 panel open, boxes in the
    // panel's empty bottom strip that jump to a dialogue id ("1-3.C05") or to a stage-wave's start ("2-4"); F2 opens the
    // balance window of the stage-wave on screen, where each enemy group's count, timing, health, speed and core damage (as
    // the campaign asset has them, before the stage's scaling) are changed for that stage-wave until the game quits.
    // Plain IMGUI: a team tool, not game UI.
    public sealed class DefenseDevWindow : MonoBehaviour
    {
        DefenseBattle battle;
        bool balanceOpen;
        string jumpId = "1-1.A01", waveId = "2-4", message;
        string[][] fields;
        (int stage, int wave) fieldsFor;
        static readonly string[] Columns = { "수", "출현(초)", "간격(초)", "체력", "속도", "코어 피해" };

        void Awake() => battle = GetComponent<DefenseBattle>();

        void Update()
        {
            if (!GameFlow.Developer) { balanceOpen = false; return; }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame) { balanceOpen = !balanceOpen; fields = null; message = null; }
        }

        void OnGUI()
        {
            if (!GameFlow.Developer || battle.Simulation == null) return;
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            GUI.skin.label.fontSize = GUI.skin.textField.fontSize = GUI.skin.button.fontSize = GUI.skin.box.fontSize = 20;
            // The F1 panel's bottom strip is empty between "수량 1~999" and "전체 추가" (1080p: x 300 to 1350, y 905 to 965).
            if (battle.DeveloperMode) JumpBox(new Rect(width / 2 - 640, 908, 920, 52));
            int groups = battle.Simulation.Setup.Waves[battle.Simulation.WaveIndex].Length;
            balanceArea = new Rect(width / 2 - 600, 180, 1200, 176 + 44 * groups);
            if (balanceOpen) BalanceWindow(balanceArea);
        }
        Rect balanceArea;
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
            if (GUI.Button(new Rect(area.x + 336, area.y + 8, 60, 36), "Go")) message = battle.DeveloperJump(jumpId);
            GUI.Label(new Rect(area.x + 420, area.y + 10, 50, 34), "S-W");
            waveId = GUI.TextField(new Rect(area.x + 470, area.y + 8, 90, 36), waveId);
            if (GUI.Button(new Rect(area.x + 566, area.y + 8, 60, 36), "Go")) message = battle.DeveloperJumpWave(waveId);
            GUI.Label(new Rect(area.x + 640, area.y + 10, 280, 34), "예: 1-3.C05 · 1-3 / 2-4");
            // Notices run long ("캐시 없음: Tools/Develop/캐시 제작으로 기록하세요"): a line of their own over the strip.
            if (!string.IsNullOrEmpty(message)) GUI.Box(new Rect(area.x, area.y - 44, area.width, 38), message);
        }

        void BalanceWindow(Rect area)
        {
            var sim = battle.Simulation;
            int stage = sim.Setup.Stage, wave = sim.WaveIndex;
            var groups = sim.Setup.Waves[wave];
            if (fields == null || fieldsFor != (stage, wave)) { fields = groups.Select(Values).ToArray(); fieldsFor = (stage, wave); }
            GUI.Box(area, $"밸런스 · {stage}-{wave + 1} (F2)");
            float y = area.y + 40;
            GUI.Label(new Rect(area.x + 16, y, 180, 30), "적");
            for (int c = 0; c < Columns.Length; c++) GUI.Label(new Rect(area.x + 200 + c * 104, y, 104, 30), Columns[c]);
            GUI.Label(new Rect(area.x + 830, y, 170, 30), "실제 체력·속도");
            float total = 0, last = 0; int count = 0; bool boss = false;
            for (int i = 0; i < groups.Length; i++)
            {
                y += 44;
                var group = groups[i];
                var actual = sim.Setup.RulesConfig.Scale(group.Enemy, stage, wave);
                string name = battle.EnemyAt(group.DefinitionIndex).displayName;
                GUI.Label(new Rect(area.x + 16, y, 184, 36), name + (group.Boss ? " (보스)" : "") + (battle.WaveGroupEdited(stage, wave, i) ? " *" : ""));
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
            string state = sim.Phase == BattlePhase.Ready ? "준비 중 · 바로 적용" : "전투 중 · 이 웨이브를 다시 시작하면 적용";
            GUI.Label(new Rect(area.x + 16, y, 1040, 34), string.IsNullOrEmpty(message) ? state + " · * 손으로 바꾼 값, Reset: 원래 값" : message);
            if (GUI.Button(new Rect(area.x + area.width - 116, y, 100, 34), "Close")) balanceOpen = false;
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
        void Set(int stage, int wave, int group, DefenseBattle.GroupEdit edit)
        {
            bool now = battle.SetWaveGroup(stage, wave, group, edit);
            fields[group] = Values(battle.Simulation.Setup.Waves[wave][group]);
            message = (edit == null ? "원래 값으로 · " : "적용 · ") + (now ? "이 웨이브에 바로 들어감" : "이 웨이브를 다시 시작하면 들어감");
        }
    }
}
