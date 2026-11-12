using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodingGame.Defense
{
    // Developer tools drawn over the battle (developer rights only, GameFlow.Developer): with the F1 panel open, a box in the
    // panel's empty bottom strip that jumps to a dialogue id ("1-3.C05"); F2 opens the balance window, where each enemy's
    // health, speed, attack damage and core damage are set to fixed numbers for the battle under way and the ones after it.
    // Plain IMGUI: a team tool, not game UI.
    public sealed class DefenseDevWindow : MonoBehaviour
    {
        DefenseBattle battle;
        bool balanceOpen;
        string jumpId = "1-1.A01", message;
        string[][] fields;
        static readonly string[] Columns = { "HP", "Speed", "Damage", "Leak" };

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
            balanceArea = new Rect(width / 2 - 470, 180, 940, 130 + 44 * battle.EnemyDefinitions.Count);
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
            GUI.Label(new Rect(area.x, area.y + 10, 220, 34), "Dialogue ID");
            jumpId = GUI.TextField(new Rect(area.x + 150, area.y + 8, 300, 36), jumpId);
            if (GUI.Button(new Rect(area.x + 460, area.y + 8, 110, 36), "Go")) message = battle.DeveloperJump(jumpId);
            GUI.Label(new Rect(area.x + 585, area.y + 10, 340, 34), string.IsNullOrEmpty(message) ? "e.g. 1-3.C05 or 1-3" : message);
        }

        void BalanceWindow(Rect area)
        {
            var enemies = battle.EnemyDefinitions;
            if (fields == null)
            {
                fields = new string[enemies.Count][];
                for (int i = 0; i < enemies.Count; i++) fields[i] = Values(battle.EnemyNow(i));
            }
            GUI.Box(area, "Enemy stats (F2) - the numbers this battle's enemies come with; kept until the game quits");
            float y = area.y + 40;
            for (int c = 0; c < Columns.Length; c++) GUI.Label(new Rect(area.x + 250 + c * 110, y, 110, 30), Columns[c]);
            for (int i = 0; i < enemies.Count; i++)
            {
                y += 44;
                GUI.Label(new Rect(area.x + 16, y, 230, 36), enemies[i].name + (battle.EnemyStatsSet(i) ? "  *" : ""));
                for (int c = 0; c < Columns.Length; c++) fields[i][c] = GUI.TextField(new Rect(area.x + 250 + c * 110, y, 96, 36), fields[i][c]);
                if (GUI.Button(new Rect(area.x + 700, y, 100, 36), "Apply")) Apply(i);
                if (GUI.Button(new Rect(area.x + 808, y, 116, 36), "Reset")) { battle.SetEnemyStats(i, null); fields[i] = Values(battle.EnemyNow(i)); }
            }
            y += 54;
            GUI.Label(new Rect(area.x + 16, y, 700, 34), string.IsNullOrEmpty(message) ? "* set by hand. Reset: the wave's own numbers back." : message);
            if (GUI.Button(new Rect(area.x + area.width - 136, y, 120, 34), "Close")) balanceOpen = false;
        }

        static string[] Values(EnemySpec spec) => new[] { spec.health.ToString("0.##", CultureInfo.InvariantCulture),
            spec.speed.ToString("0.##", CultureInfo.InvariantCulture), spec.damage.ToString("0.##", CultureInfo.InvariantCulture),
            spec.leakDamage.ToString(CultureInfo.InvariantCulture) };

        void Apply(int definition)
        {
            var f = fields[definition];
            bool Number(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (!Number(f[0], out float health) || health <= 0 || !Number(f[1], out float speed) || speed <= 0 ||
                !Number(f[2], out float damage) || damage < 0 || !int.TryParse(f[3], out int leak) || leak < 1)
            { message = $"{battle.EnemyDefinitions[definition].name}: HP and Speed above 0, Damage 0 or more, Leak 1 or more"; return; }
            battle.SetEnemyStats(definition, new DefenseSimulation.EnemyStats { health = health, speed = speed, damage = damage, leakDamage = leak });
            message = null;
        }
    }
}
