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
        Image fillImage, trailImage;
        Color labelColor;
        float flash;
        void Start()
        {
            if (!battle || !battleCamera || !fill || !label)
                throw new InvalidOperationException("DefenseCoreHealthBar Inspector 참조를 연결하세요.");
            ((RectTransform)transform).sizeDelta=new Vector2(220,50);
            var container=fill.parent as RectTransform;
            var background=container.Find("Background")?.GetComponent<Image>() ?? container.GetComponent<Image>();
            if(background) {
                background.enabled=false;
            }
            var track=new GameObject("Health track",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));var trackRect=track.GetComponent<RectTransform>();trackRect.SetParent(container,false);
            trackRect.anchorMin=Vector2.zero;trackRect.anchorMax=new Vector2(1,0);trackRect.offsetMin=new Vector2(0,4);trackRect.offsetMax=new Vector2(0,24);
            var trackImage=track.GetComponent<Image>();trackImage.sprite=Skin("CoreHealthTrackSoft");trackImage.color=Color.white;trackImage.type=Image.Type.Sliced;trackImage.pixelsPerUnitMultiplier=4;trackImage.raycastTarget=false;
            // Keep fill and damage trail inside the painted track border at every health fraction.
            var well=new GameObject("Fill well",typeof(RectTransform)).GetComponent<RectTransform>();well.SetParent(trackRect,false);well.anchorMin=Vector2.zero;well.anchorMax=Vector2.one;well.offsetMin=new Vector2(6,5);well.offsetMax=new Vector2(-6,-6);
            fill.SetParent(well,false);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
            fillImage=fill.GetComponent<Image>();
            fillImage.sprite=Skin("CoreHealthFillSoft");fillImage.color=Color.white;fillImage.type=Image.Type.Filled;fillImage.fillMethod=Image.FillMethod.Horizontal;fillImage.fillOrigin=0;fillImage.fillAmount=1;fillImage.preserveAspect=false;fillImage.raycastTarget=false;
            label.rectTransform.anchorMin=new Vector2(0,1);label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(0,-24);label.rectTransform.offsetMax=new Vector2(0,-1);
            label.color=new Color(.92f,.97f,1f);label.fontStyle=FontStyles.Bold;label.fontSize=19;label.alignment=TextAlignmentOptions.Midline;label.margin=Vector4.zero;label.raycastTarget=false;
            var previous=transform.Find("BaseShield");if(previous)previous.gameObject.SetActive(false);
            // A copy of the fill drawn just before it, so the lost part shows as a trail.
            trailBar = Instantiate(fill, fill.parent); trailBar.name = "Trail";
            trailBar.SetSiblingIndex(fill.GetSiblingIndex());
            trailImage=trailBar.GetComponent<Image>();
            if (trailBar.TryGetComponent<Graphic>(out var graphic)) { graphic.color = trailColor; graphic.raycastTarget = false; }
            labelColor = label.color;
        }
        static Sprite Skin(string name)
        {
            var sprite=Array.Find(Resources.LoadAll<Sprite>("LearningSkins/"+name),s=>s.name==name);
            if(!sprite)throw new InvalidOperationException("Core health sprite is missing: "+name);
            return sprite;
        }
        void LateUpdate()
        {
            var sim = battle.Simulation;
            if (sim == null) return;
            float fraction = Mathf.Clamp01((float)sim.BaseHealth / sim.Setup.BaseHealth);
            // Reveal the full-size painted fill rather than compressing its rounded ends as health falls.
            fillImage.fillAmount = fraction;
            trailImage.fillAmount = trail.Step(fraction, Time.unscaledDeltaTime);
            if (trail.JustHit) flash = FlashSeconds;
            flash = Mathf.Max(0, flash - Time.unscaledDeltaTime);
            label.color = Color.Lerp(labelColor, hitColor, flash / FlashSeconds);
            label.text = $"기지 {sim.BaseHealth} / {sim.Setup.BaseHealth}";
            transform.rotation = battleCamera.transform.rotation;
        }
    }
}
