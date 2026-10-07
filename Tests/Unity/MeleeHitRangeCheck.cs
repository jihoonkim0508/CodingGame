// Run via Unity eval_file in paused Defense Play Mode.
if (!UnityEngine.Application.isPlaying) throw new System.Exception("Enter Play Mode first.");
var battle = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseBattle>();
var preview = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseAttackPreview>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var button = (UnityEngine.UI.Button)new UnityEditor.SerializedObject(battle).FindProperty("hitRangeButton").objectReferenceValue;
if (!button || !button.gameObject.activeInHierarchy) throw new System.Exception("Open developer mode: hit range button must be visible.");
if (battle.ShowHitRanges) button.onClick.Invoke();
foreach (var owner in new UnityEngine.Object[] { battle, preview })
{
    var effects = owner.GetType().GetField("hitEffects", flags).GetValue(owner);
    var type = effects.GetType();
    var ranges = (System.Collections.IList)type.GetField("hitRanges", flags).GetValue(effects);
    var so = new UnityEditor.SerializedObject(owner);
    var parent = (UnityEngine.Transform)so.FindProperty(owner == battle ? "actorsRoot" : "arena").objectReferenceValue;
    var offset = owner == battle ? UnityEngine.Vector3.zero : parent.position;
    var evt = new CodingGame.Defense.CombatEvent { Kind = "slash", Origin = new System.Numerics.Vector2(4,5),
        Position = new System.Numerics.Vector2(3,5), Radius = 3 };
    object[] arguments = { evt, parent, offset, UnityEngine.Quaternion.identity };
    type.GetMethod("Clear").Invoke(effects, null);
    type.GetMethod("Show").Invoke(effects, arguments);
    if (ranges.Count != 0) throw new System.Exception("OFF spawned a hit range.");
    button.onClick.Invoke();
    type.GetMethod("Show").Invoke(effects, arguments);
    if (!battle.ShowHitRanges || ranges.Count != 1) throw new System.Exception("ON failed in " + owner.name);
    var pair = ((System.ValueTuple<CodingGame.Defense.DefenseRangeRing, float>)ranges[0]);
    var line = (UnityEngine.LineRenderer)new UnityEditor.SerializedObject(pair.Item1).FindProperty("line").objectReferenceValue;
    var center = offset + new UnityEngine.Vector3(4,.105f,5);
    for (int i = 0; i < line.positionCount - 1; i++)
    {
        var delta = line.GetPosition(i) - center;
        if (UnityEngine.Mathf.Abs(delta.magnitude - 3) > .001f || delta.x > .001f)
            throw new System.Exception("Displayed range differs from left-facing radius-three splash.");
    }
    if (UnityEngine.Vector3.Distance((line.GetPosition(0) + line.GetPosition(line.positionCount - 2)) * .5f, center) > .001f)
        throw new System.Exception("Robot is not centered on the diameter.");
    type.GetMethod("Tick").Invoke(effects, new object[] { 0f });
    if (((System.ValueTuple<CodingGame.Defense.DefenseRangeRing, float>)ranges[0]).Item2 != pair.Item2)
        throw new System.Exception("Paused range expired.");
    button.onClick.Invoke();
    if (ranges.Count != 0 || line.enabled) throw new System.Exception("OFF did not immediately hide existing range.");
    type.GetMethod("Clear").Invoke(effects, null);
}
var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/!_Project/Defense/Effects/WarriorSlashSemicircle.asset");
if (!mesh || mesh.vertices.Any(v => v.z < -.00001f || new UnityEngine.Vector2(v.x,v.z).magnitude * 200 > 1.0001f))
    throw new System.Exception("Slash mesh extends outside the forward semicircle.");
return "PASS: visible developer toggle, battle/preview ON/OFF, robot pivot, direction/radius, pause and VFX bounds";
