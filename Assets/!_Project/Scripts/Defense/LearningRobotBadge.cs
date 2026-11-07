using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense {
 public sealed class LearningRobotBadge:MonoBehaviour {
  [SerializeField] Image background;
  int oldCount=-1; bool selected;
  public void Setup(Image image){background=image;}
  public void Refresh(int count,bool on){if(oldCount==count&&selected==on)return;oldCount=count;selected=on;if(background)background.color=on?new Color(.65f,.9f,1f):Color.white;}
 }
}
