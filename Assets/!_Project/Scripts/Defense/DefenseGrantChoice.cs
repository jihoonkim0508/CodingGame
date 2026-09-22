using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class DefenseGrantChoice : MonoBehaviour
    {
        [SerializeField] DefenseInventoryUI inventory;
        [SerializeField] DefenseBattle battle;
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] BlockKind kind;
        void Start() => button.onClick.AddListener(() => inventory.SelectBlockItem(kind));
        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (!battle || battle.Simulation == null) return;
            battle.Simulation.Inventory.TryGetValue(kind, out int owned);
            label.text = DefenseProgression.Label(kind) + "  ×" + owned;
        }
    }
}
