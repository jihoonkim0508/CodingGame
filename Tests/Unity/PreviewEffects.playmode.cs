// Run as Unity MCP Unity_RunCommand.Code in Defense Play Mode.
// Pauses on a rendered slash + hit frame for camera inspection. Stop Play Mode afterwards.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using CodingGame.BlockCoding;
using CodingGame.Defense;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var battle = roots.SelectMany(root => root.GetComponentsInChildren<DefenseBattle>(true)).First();
        var preview = roots.SelectMany(root => root.GetComponentsInChildren<DefenseAttackPreview>(true)).First();
        var battleSettings = new SerializedObject(battle);
        var previewSettings = new SerializedObject(preview);
        foreach (var field in new[] { "prefab", "slashPrefab", "hitRangePrefab" })
            if (battleSettings.FindProperty("hitEffects." + field).objectReferenceValue !=
                previewSettings.FindProperty("hitEffects." + field).objectReferenceValue)
                throw new Exception("Battle/preview effect mismatch: " + field);
        var slashPrefab = (VisualEffect)previewSettings.FindProperty("hitEffects.slashPrefab").objectReferenceValue;
        if (AssetDatabase.GetAssetPath(slashPrefab.visualEffectAsset) !=
            "Assets/ThirdParty/slash5-HungNguyen/slash 5 visual vfx profiles/slash-white-yellow bolder.vfx")
            throw new Exception("Original slash mesh/rotation graph not restored");
        int index = Enumerable.Range(0, 6).First(i => battle.DefinitionRole(i) == RobotRole.Warrior);
        battle.Simulation.GrantRobot(RobotRole.Warrior, 1);
        if (!battle.PlaceRobot(index, new Vector3(-8, 0, 6))) throw new Exception("Cannot place test warrior");
        var robot = battle.Simulation.Robots.Last();
        robot.Program = new DefenseProgram("effects_check", new[] { new CodeBlock(BlockKind.Slash, "0", new CodeBlock(BlockKind.NearestEnemy)) });
        battle.DevelopSelected();
        preview.Open(robot);
        var arena = (Transform)previewSettings.FindProperty("arena").objectReferenceValue;
        var camera = (Camera)previewSettings.FindProperty("previewCamera").objectReferenceValue;
        bool wasBackground = Application.runInBackground;
        Application.runInBackground = true;
        double deadline = EditorApplication.timeSinceStartup + 15;
        EditorApplication.CallbackFunction check = null;
        check = () =>
        {
            var slash = arena.GetComponentsInChildren<VisualEffect>().FirstOrDefault(v => v.aliveParticleCount > 0 && !v.culled);
            var hit = arena.GetComponentsInChildren<ParticleSystem>().FirstOrDefault(p => p.particleCount > 0);
            if (slash && hit)
            {
                EditorApplication.update -= check;
                Application.runInBackground = wasBackground;
                EditorApplication.isPaused = true;
                Debug.Log("PASS: shared effect references, original slash graph, preview slash + hit particles visible. Camera=" + camera.gameObject.GetInstanceID());
            }
            else if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= check;
                Application.runInBackground = wasBackground;
                Debug.LogError("Preview slash + hit did not render within 15 seconds");
            }
        };
        EditorApplication.update += check;
        result.Log("Watching actual preview attack; pauses when slash and hit particles render.");
    }
}
