using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodingGame.Defense
{
    // Developer tools drawn over the battle (developer rights only, GameFlow.Developer): with the F1 panel open, a box that
    // jumps to a dialogue id ("1-3.C05"); F2 opens the balance window, where each enemy's health, speed, attack damage and
    // core damage are multiplied for the battle under way and the ones after it. Plain IMGUI: a team tool, not game UI.
    public sealed class DefenseDevWindow : MonoBehaviour
    {
        DefenseBattle battle;
        bool balanceOpen;
        string jumpId = "1-1.A01", message;
        string[][] fields;
        static readonly string[] Columns = { "HP x", "Speed x", "Damage x", "Leak x" };

        void Awake() => battle = GetComponent<DefenseBattle>();

        void Update()
        {
            if (!GameFlow.Developer) { balanceOpen = false; return; }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame) { balanceOpen = !balanceOpen; fields = null; }
        }

        void OnGUI()
        {
            if (!GameFlow.Developer || battle.Simulation == null) return;
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            GUI.skin.label.fontSize = GUI.skin.textField.fontSize = GUI.skin.button.fontSize = GUI.skin.box.fontSize = 20;
            if (battle.DeveloperMode) JumpBox(new Rect(width / 2 - 300, 70, 600, 92));
            balanceArea = new Rect(width / 2 - 420, 180, 840, 120 + 44 * battle.EnemyDefinitions.Count);
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
            GUI.Box(area, "Dialogue ID (e.g. 1-3.C05)");
            jumpId = GUI.TextField(new Rect(area.x + 16, area.y + 36, 400, 36), jumpId);
            if (GUI.Button(new Rect(area.x + 430, area.y + 36, 150, 36), "Go"))
                message = battle.DeveloperJump(jumpId);
            if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(area.x + 16, area.y + area.height, 600, 30), message);
        }

        void BalanceWindow(Rect area)
        {
            var enemies = battle.EnemyDefinitions;
            if (fields == null)
            {
                fields = new string[enemies.Count][];
                for (int i = 0; i < enemies.Count; i++)
                    fields[i] = System.Array.ConvertAll(battle.EnemyBalance(i), m => m.ToString("0.##", CultureInfo.InvariantCulture));
            }
            GUI.Box(area, "Enemy balance (F2) - multipliers on the authored stats, kept until the game quits");
            float y = area.y + 40;
            for (int c = 0; c < Columns.Length; c++) GUI.Label(new Rect(area.x + 260 + c * 120, y, 120, 30), Columns[c]);
            for (int i = 0; i < enemies.Count; i++)
            {
                y += 44;
                var stats = enemies[i].stats;
                GUI.Label(new Rect(area.x + 16, y, 240, 36), $"{enemies[i].name}  ({stats.health:0} hp)");
                for (int c = 0; c < Columns.Length; c++) fields[i][c] = GUI.TextField(new Rect(area.x + 260 + c * 120, y, 100, 36), fields[i][c]);
                if (GUI.Button(new Rect(area.x + 740, y, 84, 36), "Apply")) Apply(i);
            }
            y += 50;
            if (GUI.Button(new Rect(area.x + 16, y, 200, 34), "All x1 (reset)"))
            {
                for (int i = 0; i < enemies.Count; i++) { fields[i] = new[] { "1", "1", "1", "1" }; Apply(i); }
            }
            if (GUI.Button(new Rect(area.x + area.width - 136, y, 120, 34), "Close")) balanceOpen = false;
            if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(area.x + 240, y, 360, 34), message);
        }

        void Apply(int definition)
        {
            var multipliers = new float[4];
            for (int c = 0; c < 4; c++)
                if (!float.TryParse(fields[definition][c], NumberStyles.Float, CultureInfo.InvariantCulture, out multipliers[c]) || multipliers[c] <= 0)
                { message = $"{battle.EnemyDefinitions[definition].name}: '{fields[definition][c]}' is not a multiplier above 0"; return; }
            battle.SetEnemyBalance(definition, multipliers); message = null;
        }
    }
}
