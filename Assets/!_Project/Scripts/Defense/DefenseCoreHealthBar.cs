using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class DefenseCoreHealthBar : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] Camera battleCamera;
        [SerializeField] RectTransform fill;
        [SerializeField] TMP_Text label;
        [SerializeField] Color trailColor = new Color(1, .93f, .8f, .9f), hitColor = new Color(1, .3f, .3f);
        const float FlashSeconds = .4f;
        readonly DefenseHealthTrail trail = new DefenseHealthTrail();
        RectTransform trailBar;
        Color labelColor;
        float flash;
        void Start()
        {
            if (!battle || !battleCamera || !fill || !label)
                throw new InvalidOperationException("DefenseCoreHealthBar Inspector 참조를 연결하세요.");
            // A copy of the fill drawn just before it, so the lost part shows as a trail.
            trailBar = Instantiate(fill, fill.parent); trailBar.name = "Trail";
            trailBar.SetSiblingIndex(fill.GetSiblingIndex());
            if (trailBar.TryGetComponent<Graphic>(out var graphic)) { graphic.color = trailColor; graphic.raycastTarget = false; }
            labelColor = label.color;
        }
        void LateUpdate()
        {
            var sim = battle.Simulation;
            if (sim == null) return;
            float fraction = Mathf.Clamp01((float)sim.BaseHealth / sim.Setup.BaseHealth);
            fill.anchorMax = new Vector2(fraction, 1);
            trailBar.anchorMax = new Vector2(trail.Step(fraction, Time.unscaledDeltaTime), 1);
            if (trail.JustHit) flash = FlashSeconds;
            flash = Mathf.Max(0, flash - Time.unscaledDeltaTime);
            label.color = Color.Lerp(labelColor, hitColor, flash / FlashSeconds);
            label.text = $"기지 {sim.BaseHealth} / {sim.Setup.BaseHealth}";
            transform.rotation = battleCamera.transform.rotation;
        }
    }
}
