using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The team's monster models (tank and runner, static meshes with one texture each) on the Heavy and Runner enemies,
    // in place of the primitive robot shapes. Models/Monsters holds the FBX, texture and URP material of each.
    public static class MonsterModels
    {
        const string Folder = "Assets/!_Project/Resources/Models/Monsters/", Prefabs = "Assets/!_Project/Defense/Prefabs/";

        // Four sides of each model (front = looking down -Z at its +Z face) into TestResults/Editor/monsters, to see which
        // way it faces before it is fitted.
        public static void Sides()
        {
            Directory.CreateDirectory("TestResults/Editor/monsters");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.scene = scene;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f, .25f, .3f); camera.orthographic = true;
                var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.transform.rotation = Quaternion.Euler(40, 30, 0);
                foreach (var name in new[] { "MonsterTank", "MonsterRunner" })
                {
                    var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".fbx"));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, scene);
                    foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + name + ".mat");
                    var bounds = Bounds(model);
                    camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * .6f;
                    foreach (var (side, direction) in new[] { ("front", Vector3.forward), ("right", Vector3.right), ("back", Vector3.back), ("left", Vector3.left) })
                    {
                        camera.transform.position = bounds.center + direction * 10 + Vector3.up * 2;
                        camera.transform.LookAt(bounds.center);
                        Render(camera, $"TestResults/Editor/monsters/{name}_{side}.png");
                    }
                    Object.DestroyImmediate(model);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("[MonsterModels] sides → TestResults/Editor/monsters");
        }

        // The tank on Heavy and the runner on Runner: the primitive body, helmet and visor are switched off and the base disc
        // removed, the model stands on the ground at a height in the enemy's own units (Heavy is scaled 1.3, Runner 0.8) and faces +Z as
        // the walk turns it, and it becomes the body DefenseActorView tints when stunned or slowed.
        public static void Apply()
        {
            Fit("Heavy", "MonsterTank", 1.1f);
            Runner();
            Debug.Log("[MonsterModels] Heavy and Runner use the team's models");
        }

        // The runner flies: lifted off the ground and bobbing (DefenseActorView.hover), a flame from each foot (the two
        // octagonal nozzles low on its left and right, facing outward and down).
        public static void Runner()
        {
            Fit("Runner", "MonsterRunner", 1.25f); // at 1.0 the runner read as a small dot on the road (3-5 screenshot)
            Thrusters("Runner", .5f);
            AssetDatabase.SaveAssets();
        }

        // No dark base disc under the enemies (the team's models stood on a primitive cylinder): removed from the three enemy
        // prefabs, the walking model set down onto the ground, and the next-wave cards photographed again.
        public static void NoBase()
        {
            foreach (var enemy in new[] { "Enemy", "Heavy", "Runner" })
            {
                string path = Prefabs + enemy + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var disc = root.transform.Find("Base");
                    if (disc) Object.DestroyImmediate(disc.gameObject);
                    var model = root.transform.Find("Model"); // the runner's flies in Hover
                    var mesh = model ? model.GetComponentInChildren<MeshRenderer>() : null;
                    if (mesh) model.localPosition -= Vector3.up * LocalBounds(root.transform, mesh).min.y;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            RobotIcons.Build();
        }

        static void Thrusters(string enemy, float lift)
        {
            string path = Prefabs + enemy + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model = root.transform.Find("Model");
                var renderer = model.GetComponentInChildren<MeshRenderer>();
                var hover = new GameObject("Hover").transform; hover.SetParent(root.transform, false);
                model.SetParent(hover, true);
                var glow = AssetDatabase.LoadAssetAtPath<Material>(Folder + "ThrusterFlame.mat");
                if (!glow)
                {
                    glow = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/!_Project/Defense/Effects/HIE_Hit_04_PointGlow_01.mat"));
                    AssetDatabase.CreateAsset(glow, Folder + "ThrusterFlame.mat");
                }
                // The hit glow's soft dot, alpha-blended instead of added: added light vanished on the cream road and desk wood.
                glow.SetFloat("_Blend", 0);
                glow.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); glow.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glow.SetFloat("_SrcBlendAlpha", 1); glow.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                EditorUtility.SetDirty(glow);
                var flames = new[] { -1, 1 }.Select(side =>
                {
                    var (at, outward) = Nozzle(root.transform, renderer, side);
                    return Flame(hover, side < 0 ? "Thruster L" : "Thruster R", at, outward, glow);
                }).ToArray();
                hover.localPosition = Vector3.up * lift;
                // The health bar as far over the lifted head as it was over the standing one.
                root.transform.Find("Health").localPosition = Vector3.up * (LocalBounds(root.transform, renderer).max.y + .34f);
                var view = new SerializedObject(root.GetComponent<DefenseActorView>());
                view.FindProperty("hover").objectReferenceValue = hover;
                var list = view.FindProperty("thrusters"); list.arraySize = flames.Length;
                for (int i = 0; i < flames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = flames[i];
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // The outer face centre and facing of the nozzle on one side, in the enemy's local space: the mesh's points low on that
        // side near the middle (the runner's nozzles sit at x ±0.39, y 0.1-0.25, z 0 at height 1.25).
        static (Vector3 at, Vector3 outward) Nozzle(Transform space, MeshRenderer renderer, int side)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var toSpace = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var vertices = mesh.vertices; var normals = mesh.normals;
            float bottom = LocalBounds(space, renderer).min.y;
            var points = new System.Collections.Generic.List<Vector3>(); var normal = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = toSpace.MultiplyPoint3x4(vertices[i]);
                if (p.y > bottom + .25f || side * p.x < .28f || Mathf.Abs(p.z) > .13f) continue;
                points.Add(p); normal += toSpace.MultiplyVector(normals[i]).normalized;
            }
            var outward = normal.normalized;
            if (points.Count < 20 || outward.y > -.3f || side * outward.x < .3f)
                throw new InvalidOperationException($"{renderer.name}: {side} 쪽 발(노즐)을 찾지 못했습니다 (점 {points.Count}, 방향 {outward}).");
            var centre = points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Count;
            return (centre + outward * points.Max(p => Vector3.Dot(p - centre, outward)), outward);
        }

        // A short jet along the nozzle's facing: white-yellow at the nozzle, orange, red, gone; in the enemy's own space and
        // scale, so it turns and shrinks with it.
        static ParticleSystem Flame(Transform parent, string name, Vector3 at, Vector3 outward, Material glow)
        {
            var flame = new GameObject(name, typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = at; flame.transform.localRotation = Quaternion.LookRotation(outward);
            flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = flame.main;
            main.duration = 1; main.loop = true; main.playOnAwake = true; main.maxParticles = 70;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.26f, .34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.8f, 1f); // the glow dot fills only the middle of its quad; seen from the battle camera
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = flame.emission; emission.rateOverTime = 130;
            var shape = flame.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 5; shape.radius = .04f;
            var size = flame.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .3f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1, 1, .85f), 0), new GradientColorKey(new Color(1, .85f, .3f), .2f),
                        new GradientColorKey(new Color(1, .45f, .08f), .5f), new GradientColorKey(new Color(.75f, .12f, .05f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .45f), new GradientAlphaKey(.75f, .75f), new GradientAlphaKey(0, 1) });
            var color = flame.colorOverLifetime; color.enabled = true; color.color = gradient;
            var look = flame.GetComponent<ParticleSystemRenderer>();
            // Stretched along the jet, so the dots read as one flame.
            look.renderMode = ParticleSystemRenderMode.Stretch; look.lengthScale = 1.8f; look.velocityScale = 0;
            look.sharedMaterial = glow; look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; look.receiveShadows = false;
            return flame;
        }

        static void Fit(string enemy, string model, float height)
        {
            string path = Prefabs + enemy + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var name in new[] { "Body", "Head/Helmet", "Head/Visor" })
                { var part = root.transform.Find(name); if (part) part.gameObject.SetActive(false); }
                var disc = root.transform.Find("Base"); if (disc) Object.DestroyImmediate(disc.gameObject);
                var old = root.transform.Find("Hover") ?? root.transform.Find("Model"); // the runner's model flies in Hover
                if (old) Object.DestroyImmediate(old.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + model + ".fbx"), root.transform);
                instance.name = "Model";
                var renderer = instance.GetComponentInChildren<MeshRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + model + ".mat");
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // Size and place in the enemy's local space: as tall as asked, centred, standing on the ground. The
                // FBX root keeps its own rotation and scale (3ds Max axes: the tank turned 270° on X, the runner scaled -0.04).
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + model + ".fbx").transform;
                instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = source.localRotation; instance.transform.localScale = source.localScale;
                var local = LocalBounds(root.transform, renderer);
                float k = height / local.size.y;
                instance.transform.localScale = source.localScale * k;
                local = LocalBounds(root.transform, renderer);
                instance.transform.localPosition = new Vector3(-local.center.x, -local.min.y, -local.center.z);
                var view = new SerializedObject(root.GetComponent<DefenseActorView>());
                view.FindProperty("body").objectReferenceValue = renderer;
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // A renderer's mesh bounds in another transform's local space.
        static Bounds LocalBounds(Transform space, MeshRenderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var toSpace = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var b = mesh.bounds; var result = new Bounds(toSpace.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(toSpace.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        public static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static void Render(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(512, 512, 24);
            camera.targetTexture = target; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
        }
    }
}
