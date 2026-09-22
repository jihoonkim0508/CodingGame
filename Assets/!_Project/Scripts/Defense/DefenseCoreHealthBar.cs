using System;
using TMPro;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed class DefenseCoreHealthBar : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] Camera battleCamera;
        [SerializeField] RectTransform fill;
        [SerializeField] TMP_Text label;
        void Start()
        {
            if (!battle || !battleCamera || !fill || !label)
                throw new InvalidOperationException("DefenseCoreHealthBar Inspector 참조를 연결하세요.");
        }
        void LateUpdate()
        {
            var sim = battle.Simulation;
            if (sim == null) return;
            float fraction = Mathf.Clamp01((float)sim.BaseHealth / sim.Setup.BaseHealth);
            fill.anchorMax = new Vector2(fraction, 1);
            label.text = $"기지 {sim.BaseHealth} / {sim.Setup.BaseHealth}";
            transform.rotation = battleCamera.transform.rotation;
        }
    }
}
