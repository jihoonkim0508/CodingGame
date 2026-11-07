using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense {
 public sealed class DefenseRobotStatsCard:MonoBehaviour {
  [SerializeField] TMP_Text title,damage,range,interval,health,extra,coins;
  [SerializeField] Image roleIcon;
  [SerializeField] Sprite[] roleSprites;
  Vector2 laidOutSize;
  bool layoutValid;
  float laidOutExtraHeight;
  public void Configure(TMP_Text t,TMP_Text d,TMP_Text r,TMP_Text i,TMP_Text h,TMP_Text e,TMP_Text c){title=t;damage=d;range=r;interval=i;health=h;extra=e;coins=c;}
  public void SetIcons(Image image,Sprite[] sprites){roleIcon=image;roleSprites=sprites;}
  public void Show(DefenseBattle battle,RobotState bot){
   var spec=bot.Spec;float next=(float)Math.Pow(bot.UpgradeFactor,bot.Level);
   if(roleIcon&&roleSprites!=null&&roleSprites.Length==6)roleIcon.sprite=roleSprites[spec.role==RobotRole.Warrior?0:spec.role==RobotRole.Tank?1:spec.role==RobotRole.Shooter?2:spec.role==RobotRole.Bomber?3:spec.role==RobotRole.Buffer?4:5];
   string Upgrade(float current,float upgraded)=>bot.IsMaxLevel||Mathf.Approximately(current,upgraded)?$"{current:0.##}":$"{current:0.##} <color=#98E6C6>→ {upgraded:0.##}</color>";
   title.text=$"{battle.RobotName(bot.Id)}  Lv.{bot.Level}"+(bot.IsMaxLevel?"":" → Lv."+(bot.Level+1));
   damage.text=spec.role==RobotRole.Buffer?"지원형":Upgrade(bot.Damage,bot.Damage/bot.LevelMultiplier*next);
   range.text=$"{bot.Range:0.##}";interval.text=$"{bot.Interval:0.##}초";
   health.text=bot.Health.HasValue?(bot.IsMaxLevel?$"{bot.Health:0} / {bot.MaxHealth:0}":Upgrade(bot.MaxHealth,spec.health*next)):"피해 대상 제외";
   extra.text=spec.role==RobotRole.Bomber?$"폭발 반경  {spec.effectRadius:0.##}":spec.role==RobotRole.Utility?$"감속  {(1-spec.slowMultiplier)*100:0}%":spec.role==RobotRole.Buffer?
    $"버프 피해 {Upgrade(1+(spec.buffDamage-1)*bot.LevelMultiplier,1+(spec.buffDamage-1)*next)}배\n버프 사거리 {Upgrade(1+(spec.buffRange-1)*bot.LevelMultiplier,1+(spec.buffRange-1)*next)}배\n버프 간격 {Upgrade(Math.Max(.01f,1-(1-spec.buffInterval)*bot.LevelMultiplier),Math.Max(.01f,1-(1-spec.buffInterval)*next))}배":bot.Health.HasValue?$"저지  {spec.blockCapacity}" : "";
   coins.text=$"보유 코인   {battle.Simulation.Coins:N0}";
   AlignRows(false);
  }
  void OnEnable(){layoutValid=false;}
  void LateUpdate()=>AlignRows(false);
  void AlignRows(bool force){
   var card=(RectTransform)transform;var size=card.rect.size;
   if(!damage||!extra||size.x<=0||size.y<=0)return;
   float extraHeight=string.IsNullOrEmpty(extra.text)?0:extra.text.Contains("\n")?60:26;
   if(!force&&layoutValid&&size==laidOutSize&&extraHeight==laidOutExtraHeight)return;
   layoutValid=true;laidOutSize=size;laidOutExtraHeight=extraHeight;
   float top=62,footer=size.y-48,rowHeight=Mathf.Clamp((footer-12-top-extraHeight-(extraHeight>0?8:0))/4,32,48);
   var values=new[]{damage,range,interval,health};
   Position(title.rectTransform,58,8,size.x-80,34);title.enableAutoSizing=true;title.fontSizeMin=14;title.fontSizeMax=19;
   for(int i=0;i<4;i++){
    float y=top+i*rowHeight;
    var label=transform.Find("StatName"+i) as RectTransform;
    if(label)Position(label,62,y,106,rowHeight);
    Position(values[i].rectTransform,180,y,Mathf.Max(80,size.x-204),rowHeight);
    values[i].alignment=TextAlignmentOptions.MidlineRight;values[i].enableAutoSizing=true;values[i].fontSizeMin=15;values[i].fontSizeMax=19;
    var icon=transform.Find("StatIcon"+i) as RectTransform;if(icon)Position(icon,24,y+(rowHeight-25)/2,25,25);
    var line=transform.Find("StatDivider"+i) as RectTransform;if(line)Position(line,26,y+rowHeight-1,size.x-50,1);
   }
   float extraTop=top+4*rowHeight+8;
   Position(extra.rectTransform,30,extraTop,size.x-54,Mathf.Max(0,footer-extraTop-8));extra.fontSize=15;extra.alignment=TextAlignmentOptions.TopLeft;extra.margin=Vector4.zero;
   coins.rectTransform.anchorMin=coins.rectTransform.anchorMax=new Vector2(0,0);coins.rectTransform.pivot=Vector2.zero;coins.rectTransform.anchoredPosition=new Vector2(62,11);coins.rectTransform.sizeDelta=new Vector2(size.x-84,30);
  }
  static void Position(RectTransform r,float x,float y,float width,float height){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
 }
}
