using System;
using TMPro;
using UnityEngine;
namespace CodingGame.Defense {
 public sealed class DefenseRobotStatsCard:MonoBehaviour {
  [SerializeField] TMP_Text title,damage,range,interval,health,extra,coins;
  const float SideInset = 24;
  Vector2 laidOutSize;
  bool layoutValid;
  float laidOutExtraHeight;
  public void Configure(TMP_Text t,TMP_Text d,TMP_Text r,TMP_Text i,TMP_Text h,TMP_Text e,TMP_Text c){title=t;damage=d;range=r;interval=i;health=h;extra=e;coins=c;}
  // The robot's class and what the other class's actions cost it (the block help says the same).
  public static string ClassNote(RobotRole role,DefenseBalanceRules rules)=>(Rules.AttackClass(role)?"공격류 · 유틸 기술(slow)은":"유틸류 · 공격 기술은")+
   $" 효과 {rules.reducedStrength:0%}, 대기 {rules.reducedIntervalMultiplier:0.#}배";
  public void Show(DefenseBattle battle,RobotState bot){
   var spec=bot.Spec;float next=(float)Math.Pow(bot.UpgradeFactor,bot.Level);
   string Upgrade(float current,float upgraded)=>bot.IsMaxLevel||Mathf.Approximately(current,upgraded)?$"{current:0.##}":$"{current:0.##} <color=#98E6C6>→ {upgraded:0.##}</color>";
   title.text=$"{battle.RobotName(bot.Id)}  Lv.{bot.Level}"+(bot.IsMaxLevel?"":" → Lv."+(bot.Level+1));
   damage.text=Upgrade(bot.Damage,bot.Damage/bot.LevelMultiplier*next);
   range.text=$"{bot.Range:0.##}";interval.text=$"{bot.Interval:0.##}초";
   health.text=bot.IsMaxLevel?$"{bot.Health:0} / {bot.MaxHealth:0}":Upgrade(bot.MaxHealth,spec.health*next);
   extra.text=(spec.role==RobotRole.Utility?$"감속 속도 {spec.slowMultiplier:0%} · {spec.duration:0.#}초\n":
    spec.role==RobotRole.Tank?$"가드 {Upgrade(bot.MaxGuard,spec.guard*next)} · 길막기 거리 {spec.blockRadius:0.#}\n가드가 다 닳으면 {spec.knockback:0.#}초 밀려나요\n":"")+
    ClassNote(spec.role,battle.Simulation.Setup.RulesConfig);
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
   float top=50,footer=size.y-44,rowHeight=Mathf.Clamp((footer-12-top-extraHeight-(extraHeight>0?8:0))/4,22,48);
   var values=new[]{damage,range,interval,health};
   float contentWidth = size.x - 2 * SideInset;
   const float valueStart = 140;
   Position(title.rectTransform,SideInset,8,contentWidth,34);title.enableAutoSizing=true;title.fontSizeMin=14;title.fontSizeMax=19;
   for(int i=0;i<4;i++){
    float y=top+i*rowHeight;
    var label=transform.Find("StatName"+i) as RectTransform;
    if(label)Position(label,SideInset,y,106,rowHeight);
    Position(values[i].rectTransform,valueStart,y,Mathf.Max(80,size.x-valueStart-SideInset),rowHeight);
    values[i].alignment=TextAlignmentOptions.MidlineRight;values[i].enableAutoSizing=true;values[i].fontSizeMin=15;values[i].fontSizeMax=19;
    var line=transform.Find("StatDivider"+i) as RectTransform;if(line)Position(line,SideInset,y+rowHeight-1,contentWidth,1);
   }
   float extraTop=top+4*rowHeight+8;
   Position(extra.rectTransform,SideInset,extraTop,contentWidth,Mathf.Max(0,footer-extraTop-8));extra.fontSize=15;extra.alignment=TextAlignmentOptions.TopLeft;extra.margin=Vector4.zero;
   coins.rectTransform.anchorMin=coins.rectTransform.anchorMax=new Vector2(0,0);coins.rectTransform.pivot=Vector2.zero;coins.rectTransform.anchoredPosition=new Vector2(SideInset,11);coins.rectTransform.sizeDelta=new Vector2(contentWidth,30);
  }
  static void Position(RectTransform r,float x,float y,float width,float height){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
 }
}
