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
        [MenuItem("Tools/Develop/Monster Model Sides")]
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

        // The tank on Heavy and the runner on Runner: the primitive body, helmet and visor are switched off (the base disc stays),
        // the model stands on the base at a height in the enemy's own units (Heavy is scaled 1.3, Runner 0.8) and faces +Z as
        // the walk turns it, and it becomes the body DefenseActorView tints when stunned or slowed.
        [MenuItem("Tools/Develop/Apply Monster Models")]
        public static void Apply()
        {
            Fit("Heavy", "MonsterTank", 1.1f);
            Fit("Runner", "MonsterRunner", 1.25f); // at 1.0 the runner read as a small dot on the road (3-5 screenshot)
            AssetDatabase.SaveAssets();
            Debug.Log("[MonsterModels] Heavy and Runner use the team's models");
        }

        static void Fit(string enemy, string model, float height)
        {
            string path = Prefabs + enemy + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var name in new[] { "Body", "Head/Helmet", "Head/Visor" })
                { var part = root.transform.Find(name); if (part) part.gameObject.SetActive(false); }
                var old = root.transform.Find("Model");
                if (old) Object.DestroyImmediate(old.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + model + ".fbx"), root.transform);
                instance.name = "Model";
                var renderer = instance.GetComponentInChildren<MeshRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + model + ".mat");
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // Size and place in the enemy's local space: as tall as asked, centred, standing just above the base disc. The
                // FBX root keeps its own rotation and scale (3ds Max axes: the tank turned 270° on X, the runner scaled -0.04).
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + model + ".fbx").transform;
                instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = source.localRotation; instance.transform.localScale = source.localScale;
                var local = LocalBounds(root.transform, renderer);
                float k = height / local.size.y;
                instance.transform.localScale = source.localScale * k;
                local = LocalBounds(root.transform, renderer);
                instance.transform.localPosition = new Vector3(-local.center.x, .06f - local.min.y, -local.center.z);
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
