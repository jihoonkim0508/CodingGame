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
            ((RectTransform)transform).sizeDelta=new Vector2(220,46);
            var container=fill.parent as RectTransform;
            var background=container.Find("Background")?.GetComponent<Image>() ?? container.GetComponent<Image>();
            if(background) {
                background.enabled=false;
            }
            var track=new GameObject("Health track",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));var trackRect=track.GetComponent<RectTransform>();trackRect.SetParent(container,false);
            trackRect.anchorMin=Vector2.zero;trackRect.anchorMax=new Vector2(1,0);trackRect.offsetMin=new Vector2(8,8);trackRect.offsetMax=new Vector2(-8,16);track.GetComponent<Image>().color=new Color(.035f,.08f,.12f,.9f);track.GetComponent<Image>().raycastTarget=false;
            fill.SetParent(trackRect,false);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
            if(fill.TryGetComponent<Image>(out var tint)){tint.sprite=Array.Find(Resources.LoadAll<Sprite>("LearningSkins/CoreStatusFill"),s=>s.name=="CoreStatusFill");tint.color=Color.white;tint.type=Image.Type.Sliced;tint.pixelsPerUnitMultiplier=6;tint.raycastTarget=false;}
            label.rectTransform.anchorMin=new Vector2(0,1);label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(0,-25);label.rectTransform.offsetMax=new Vector2(0,-2);
            label.color=new Color(.92f,.97f,1f);label.fontStyle=FontStyles.Bold;label.fontSize=20;label.alignment=TextAlignmentOptions.Center;label.margin=Vector4.zero;
            var previous=transform.Find("BaseShield");if(previous)previous.gameObject.SetActive(false);
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
