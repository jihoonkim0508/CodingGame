using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense {
 public sealed class DefenseEditorToolbar:MonoBehaviour {
  [SerializeField] Button[] rightToLeft=System.Array.Empty<Button>();
  int visible=-1;
  void OnEnable(){var buttons=new System.Collections.Generic.List<Button>();foreach(var name in new[]{"CloseEditor","Automatic","Hint","Info"}){var button=transform.Find(name)?.GetComponent<Button>();if(button)buttons.Add(button);}if(buttons.Count>0)rightToLeft=buttons.ToArray();visible=-1;Reflow();}
  public void Configure(Button[] buttons){rightToLeft=buttons;visible=-1;Reflow();}
  void LateUpdate(){int mask=0;for(int i=0;i<rightToLeft.Length;i++)if(rightToLeft[i]&&rightToLeft[i].gameObject.activeSelf)mask|=1<<i;if(mask!=visible){Reflow();visible=mask;}}
  void Reflow(){float x=26;foreach(var button in rightToLeft){if(!button||!button.gameObject.activeSelf)continue;var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=r.pivot=Vector2.one;r.anchoredPosition=new Vector2(-x,-14);x+=r.sizeDelta.x+8;}}
 }
}
