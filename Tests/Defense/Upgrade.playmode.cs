// Run with Unity CLI/MCP eval_file while Defense.unity is playing. Resets this test run.
if (!UnityEditor.EditorApplication.isPlaying) return "Play Mode required";
var battle=UnityEngine.GameObject.Find("DefenseBattle").GetComponent<CodingGame.Defense.DefenseBattle>();
var preview=battle.GetComponent<CodingGame.Defense.DefenseAttackPreview>();
var editor=battle.GetComponent<CodingGame.Defense.DefenseCodeEditor>();
var fields=new UnityEditor.SerializedObject(preview);
var button=(UnityEngine.UI.Button)fields.FindProperty("upgradeButton").objectReferenceValue;
var label=(TMPro.TMP_Text)fields.FindProperty("upgradeLabel").objectReferenceValue;
var price=(TMPro.TMP_Text)fields.FindProperty("upgradePrice").objectReferenceValue;
var coins=(TMPro.TMP_Text)fields.FindProperty("coinLabel").objectReferenceValue;
var stats=(TMPro.TMP_Text)fields.FindProperty("stats").objectReferenceValue;
int checks=0;
void Check(bool ok,string message) { if(!ok)throw new System.Exception(message);checks++; }
try
{
    battle.Restart();
    Check(battle.PlaceRobot(2,new UnityEngine.Vector3(-8,0,4)),"place tank");battle.SelectRobot(battle.SelectedId);
    var tank=battle.Simulation.Robots.Single();
    Check(button.interactable&&label.text=="레벨업"&&price.text=="50 코인"&&coins.text.Contains("100"),"initial button price and balance");
    Check(stats.text.Contains("→ Lv.2")&&stats.text.Contains("→ 2.4")&&stats.text.Contains("→ 168"),"tank upgrade comparison before purchase");
    UnityEngine.Canvas.ForceUpdateCanvases();
    var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
    pointer.position=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((UnityEngine.RectTransform)button.transform).rect.center));
    // Canvas draw depth is assigned on the next rendered frame after reopening.
    Check(button.image.raycastTarget&&button.isActiveAndEnabled,"upgrade button exposes an active raycast target");
    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    Check(tank.Level==2&&battle.Simulation.Coins==50&&System.Math.Abs(tank.Health.Value-168)<.001f,"click purchases authored tank upgrade");
    Check(!button.interactable&&label.text=="최고레벨"&&price.text==""&&stats.text.Contains("Lv.2"),"max level disables button and hides price");
    Check(!stats.text.Contains("→"),"max level shows current stats only");
    button.onClick.Invoke();Check(battle.Simulation.Coins==50,"duplicate callback cannot charge twice");
    editor.Panel.LoadProgram("guard",new[]{new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Block)});
    preview.TogglePreview();
    var clone=preview.PreviewSimulation.Robots.Single();
    Check(clone.Level==2&&System.Math.Abs(clone.Damage-tank.Damage)<.001f&&System.Math.Abs(clone.MaxHealth-168)<.001f,"preview copies upgraded stats");
    preview.SendMessage("ResetPreview");Check(preview.PreviewSimulation.Robots.Single().Level==2&&battle.Simulation.Coins==50,"preview loop does not purchase again");
    preview.TogglePreview(); editor.Close();
    Check(battle.PlaceRobot(4,new UnityEngine.Vector3(-8,0,6)),"place second robot");battle.SelectRobot(battle.SelectedId);
    Check(button.interactable&&label.text=="레벨업", "second robot has independent upgrade");
    Check(stats.text.Contains("22")&&stats.text.Contains("→ 26.4"),"shooter upgrade comparison");
    button.onClick.Invoke();editor.Close();
    Check(battle.PlaceRobot(1,new UnityEngine.Vector3(-6,0,6)),"place third robot");battle.SelectRobot(battle.SelectedId);
    Check(!button.interactable&&label.text=="레벨업"&&price.text=="50 코인 · 50 부족"&&coins.text.Contains("0"),"insufficient funds displays exact shortage");
    button.onClick.Invoke();Check(battle.Simulation.Coins==0&&battle.Simulation.Robots.Last().Level==1,"insufficient callback leaves level unchanged");
    editor.Close();battle.SelectRobot(tank.Id);battle.RemoveSelected();
    Check(battle.Simulation.Coins==50,"recall refunds paid upgrade");
    Check(battle.PlaceRobot(2,new UnityEngine.Vector3(-8,0,4)),"redeploy recalled tank");battle.SelectRobot(battle.SelectedId);
    Check(battle.Simulation.Robots.Last().Level==1&&button.interactable,"recalled item starts at level one");
    Check(price.text=="50 코인","shortage clears after refund");
    editor.Close();Check(battle.PlaceRobot(0,new UnityEngine.Vector3(-4,0,6)),"place buffer");battle.SelectRobot(battle.SelectedId);
    Check(stats.text.Contains("→ 1.3")&&stats.text.Contains("→ 1.18")&&stats.text.Contains("→ 0.76"),"buffer comparison includes all three effects");
    UnityEngine.Canvas.ForceUpdateCanvases(); stats.ForceMeshUpdate();
    Check(stats.preferredHeight<=stats.rectTransform.rect.height&&stats.preferredWidth<=stats.rectTransform.rect.width,"buffer comparisons fit authored stats panel");
    return $"Upgrade UI checks passed: {checks}";
}
finally { battle.Restart(); }
