// Run with Unity MCP eval_file in paused Defense Play Mode.
if (!UnityEngine.Application.isPlaying) throw new System.Exception("Enter Play Mode first.");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var battle = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseBattle>();
var preview = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseAttackPreview>();
foreach (var owner in new UnityEngine.Object[] { battle, preview })
{
    var reference = new UnityEditor.SerializedObject(owner).FindProperty("hitEffects.prefab");
    var prefab = reference.objectReferenceValue as UnityEngine.ParticleSystem;
    if (!prefab) throw new System.Exception("Missing Inspector hit prefab");
    foreach (var ps in prefab.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
    {
        var material = ps.GetComponent<UnityEngine.ParticleSystemRenderer>().sharedMaterial;
        if (ps.main.loop || ps.main.playOnAwake || ps.main.stopAction != UnityEngine.ParticleSystemStopAction.None ||
            material.shader.name != "Universal Render Pipeline/Particles/Unlit" || !material.GetTexture("_BaseMap"))
            throw new System.Exception("Invalid hit particle/material configuration");
    }
    var effects = owner.GetType().GetField("hitEffects", flags).GetValue(owner);
    var type = effects.GetType();
    var active = (System.Collections.IList)type.GetField("active", flags).GetValue(effects);
    var parentField = owner == battle ? "actorsRoot" : "arena";
    var parent = (UnityEngine.Transform)new UnityEditor.SerializedObject(owner).FindProperty(parentField).objectReferenceValue;
    var offset = owner == battle ? UnityEngine.Vector3.zero : parent.position;
    var cameraField = owner == battle ? "battleCamera" : "previewCamera";
    var camera = (UnityEngine.Camera)new UnityEditor.SerializedObject(owner).FindProperty(cameraField).objectReferenceValue;
    type.GetMethod("Clear").Invoke(effects, null);
    foreach (var kind in new[] { "hit", "projectile-hit", "buff", "stun" })
        type.GetMethod("Show").Invoke(effects, new object[] {
            new CodingGame.Defense.CombatEvent { Kind = kind, Amount = 10 }, parent, offset, camera.transform.rotation });
    type.GetMethod("Show").Invoke(effects, new object[] {
        new CodingGame.Defense.CombatEvent { Kind = "hit", Amount = 0 }, parent, offset, camera.transform.rotation });
    if (active.Count != 2) throw new System.Exception("Only damaging hit events should spawn effects");
    type.GetMethod("Tick").Invoke(effects, new object[] { .1f });
    var effect = ((System.ValueTuple<UnityEngine.ParticleSystem, float>)active[0]).Item1;
    if (!effect.GetComponentsInChildren<UnityEngine.ParticleSystem>().Any(p => p.particleCount > 0))
        throw new System.Exception("No visible hit particles");
    if (UnityEngine.Vector3.Distance(effect.transform.position, offset + UnityEngine.Vector3.up * .45f) > .001f)
        throw new System.Exception("Hit position does not match beam endpoint");
    float time = effect.time;
    type.GetMethod("Tick").Invoke(effects, new object[] { 0f });
    if (effect.time != time) throw new System.Exception("Paused effects must not advance");
    type.GetMethod("Tick").Invoke(effects, new object[] { .2f });
    if (UnityEngine.Mathf.Abs(effect.time - time - .2f) > .001f) throw new System.Exception("Scaled time was not applied");
    type.GetMethod("Tick").Invoke(effects, new object[] { 3f });
    if (active.Count != 0) throw new System.Exception("Finished effects were not removed");
    type.GetMethod("Show").Invoke(effects, new object[] {
        new CodingGame.Defense.CombatEvent { Kind = "hit", Amount = 10 }, parent, offset, camera.transform.rotation });
    var cleared = ((System.ValueTuple<UnityEngine.ParticleSystem, float>)active[0]).Item1;
    type.GetMethod("Clear").Invoke(effects, null);
    if (active.Count != 0 || cleared.gameObject.activeSelf) throw new System.Exception("Reset did not clear effects");
}
return "PASS: battle/preview references, URP materials, damage-only spawn, position, pause, scaled time, expiry and reset";
