using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Photographs every robot and enemy prefab in a preview scene (front three-quarter, transparent background) and stores
    // the picture as its RobotDefinition.icon (the battle HUD's robot cards) or EnemyDefinition.icon (the next-wave cards).
    // Rerun after a model changes. Needs a GPU,
    // so batchmode without -nographics:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.RobotIcons.Build -quit
    public static class RobotIcons
    {
        const string Folder = "Assets/!_Project/Defense/UI/RobotIcons", EnemyFolder = "Assets/!_Project/Defense/UI/EnemyIcons";
        const int Size = 256;

        [MenuItem("Tools/Develop/로봇 아이콘 만들기")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var definitions = AssetDatabase.FindAssets("t:RobotDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<RobotDefinition>).Where(d => d && d.prefab).ToArray();
            foreach (var definition in definitions)
            {
                string path = $"{Folder}/{definition.name}.png";
                File.WriteAllBytes(path, Photograph(definition.prefab.gameObject));
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                // Mipmaps: the 256 px photo is shown at about 64 px on the cards, and without them it was sampled into jagged edges.
                importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                definition.icon = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                if (!definition.icon) Debug.LogError("[RobotIcons] no sprite imported at " + path);
                EditorUtility.SetDirty(definition);
            }
            // Enemies the same way; the cards show them as a RawImage, so the icon stays a plain texture.
            Directory.CreateDirectory(EnemyFolder);
            var enemies = AssetDatabase.FindAssets("t:EnemyDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EnemyDefinition>).Where(d => d && d.prefab).ToArray();
            foreach (var enemy in enemies)
            {
                string path = $"{EnemyFolder}/{enemy.name}.png";
                File.WriteAllBytes(path, Photograph(enemy.prefab.gameObject));
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                enemy.icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                EditorUtility.SetDirty(enemy);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[RobotIcons] {definitions.Length} robot icons → {Folder}, {enemies.Length} enemy icons → {EnemyFolder}");
        }

        static byte[] Photograph(GameObject prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var cameraObject = new GameObject("Icon camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var lightObject = new GameObject("Icon light"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                // The model only: the health bar (under "Health") and the attack beam are battle overlays, not the robot.
                foreach (var overlay in actor.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Beam" || r.GetComponentsInParent<Transform>(true).Any(t => t.name == "Health")))
                    overlay.enabled = false;
                var renderers = actor.GetComponentsInChildren<Renderer>().Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
                lightObject.transform.rotation = Quaternion.Euler(40, -25, 0);
                camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.orthographic = true; camera.aspect = 1; camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.22f; // room for the tilted view: a tight fit cut off tall heads
                // A little above and to the right of the robot's front, the way the battle camera sees it.
                camera.transform.position = bounds.center + new Vector3(.45f, .4f, 1).normalized * (bounds.size.magnitude * 2);
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = .01f; camera.farClipPlane = bounds.size.magnitude * 5;
                camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); image.Apply();
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera) camera.targetTexture = null;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
