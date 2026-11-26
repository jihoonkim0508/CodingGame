using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The stage-2 monsters chosen from 시안/2스테이지 몬스터 3차 초안 (일반 4 뿔 마귀, 빠른 적 3 추격 랩터, 탱커병 1 철방패 거병) on
    // the enemy prefabs Enemy, Runner and Heavy. Every stage shares those prefabs (one enemy set per campaign), so they are the
    // monsters of every stage. 시안/2스테이지 몬스터 3차 초안/게임 모델/build_game.py writes each one's <Model>.fbx (one skinned mesh
    // walking on a looping "Walk" take) and <Model>.json into Models/Monsters; Apply() imports them, paints their palettes from
    // Palettes below, fits them and photographs the next-wave cards again. Needs a GPU for the cards (batchmode without -nographics):
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.MonsterModels.Apply -quit
    public static class MonsterModels
    {
        const string Folder = "Assets/!_Project/Resources/Models/Monsters/", Prefabs = "Assets/!_Project/Defense/Prefabs/";
        // The drafts' design units in the world: their melee robot (the AttackRobot model, as the Warrior prefab wears it) stands
        // 1.55 of them, so every monster keeps its drafted size beside the robots.
        static float Design
        {
            get
            {
                var warrior = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Warrior.prefab").transform;
                return LocalBounds(warrior, warrior.GetComponentInChildren<SkinnedMeshRenderer>()).size.y * warrior.localScale.y / 1.55f;
            }
        }
        // Prefab, model, size against the draft (the user asked for the imp a quarter bigger, and the shield brute at 80%:
        // 시안/탱커 크기).
        static readonly (string prefab, string model, float size)[] Monsters =
        {
            ("Enemy", "MonsterImp", 1.25f), ("Runner", "MonsterRaptor", 1), ("Heavy", "MonsterShieldBrute", .8f),
        };

        // Each monster's colours, one per region of its palette texture, in build_game.py's REGIONS order. The drafts' colours
        // for now; another colourway is a change here and a run of Paint (or Apply).
        static readonly string[] Regions = { "white", "lightgrey", "midgrey", "charcoal", "gunmetal", "accent", "eye" };
        static readonly Dictionary<string, Color32[]> Palettes = new Dictionary<string, Color32[]>
        {
            //                                white          light grey     mid grey       charcoal    gunmetal    accent          eye slits
            ["MonsterImp"] = new[] { C(246, 246, 246), C(174, 174, 174), C(108, 108, 108), C(33, 33, 33), C(62, 62, 68), C(255, 128, 0), C(255, 236, 170) },
            ["MonsterRaptor"] = new[] { C(246, 246, 246), C(174, 174, 174), C(108, 108, 108), C(33, 33, 33), C(64, 66, 74), C(240, 44, 52), C(255, 92, 80) },
            ["MonsterShieldBrute"] = new[] { C(246, 246, 246), C(174, 174, 174), C(108, 108, 108), C(33, 33, 33), C(66, 70, 80), C(255, 190, 0), C(255, 214, 92) },
        };
        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        const int Cell = 8, Cols = 4; // the palette: 4 x 4 cells of 8 px, UVs on the cell centres

        [Serializable] sealed class Info { public float height = 1, speed, cycle; }

        public static void Apply()
        {
            Debug.Log($"[MonsterModels] one design unit = {Design:0.000} world units");
            foreach (var (prefab, model, size) in Monsters) Fit(prefab, model, size);
            TankerDustEffects.Attach(); // on the brute's foot bones
            AssetDatabase.SaveAssets();
            RobotIcons.Build();
            Debug.Log("[MonsterModels] " + string.Join(", ", Monsters.Select(m => $"{m.prefab} = {m.model}")));
        }

        // The palettes and materials again, after a change to Palettes.
        public static void Paint()
        {
            foreach (var m in Monsters) Material(m.model);
            AssetDatabase.SaveAssets();
            RobotIcons.Build();
        }

        // <Model>_Palette.png from Palettes and <Model>.mat: the robots' toon shader (the monsters' Monster.mat: cel bands and its
        // own thin outline, no emission) over the palette, its rim light in the accent.
        static Material Material(string model)
        {
            var colours = Palettes[model];
            var image = new Texture2D(Cell * Cols, Cell * Cols, TextureFormat.RGBA32, false);
            image.SetPixels32(Enumerable.Repeat(new Color32(0, 0, 0, 255), Cell * Cols * Cell * Cols).ToArray());
            for (int i = 0; i < colours.Length; i++)
                image.SetPixels32(i % Cols * Cell, (Cols - 1 - i / Cols) * Cell, Cell, Cell, Enumerable.Repeat(colours[i], Cell * Cell).ToArray());
            string png = Folder + model + "_Palette.png";
            File.WriteAllBytes(png, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(png);
            var texture = (TextureImporter)AssetImporter.GetAtPath(png);
            texture.filterMode = FilterMode.Point; texture.mipmapEnabled = false; texture.wrapMode = TextureWrapMode.Clamp;
            texture.textureCompression = TextureImporterCompression.Uncompressed; texture.sRGBTexture = true; texture.npotScale = TextureImporterNPOTScale.None;
            texture.SaveAndReimport();

            string path = Folder + model + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/!_Project/Resources/Models/CharacterAnimation/Materials/Monster.mat"));
                AssetDatabase.CreateAsset(material, path);
            }
            // White tint: DefenseActorView tints it (stunned, slowed) over every colour of the palette at once.
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(png)); material.SetColor("_BaseColor", Color.white);
            material.SetColor("_AccentColor", colours[Array.IndexOf(Regions, "accent")]);
            material.SetFloat("_TextureSaturation", 1);
            material.SetFloat("_OutlineWidth", 1.8f); material.SetFloat("_OutlineUseSmoothNormals", 1); // as the robots (CharacterOutlineImporter)
            EditorUtility.SetDirty(material);
            return material;
        }

        // The FBX as a Generic rig whose one clip loops, and a controller playing it.
        static (RuntimeAnimatorController walk, Info info) Import(string model)
        {
            string path = Folder + model + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off; // reduced keys would let planted feet creep
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips) { c.name = "Walk"; c.loopTime = true; }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var info = JsonUtility.FromJson<Info>(File.ReadAllText(Folder + model + ".json"));
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Folder + model + ".controller");
            if (!controller) controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(Folder + model + ".controller", clip);
            var state = controller.layers[0].stateMachine.defaultState;
            state.name = "Walk"; state.motion = clip;
            EditorUtility.SetDirty(controller);
            return (controller, info);
        }

        // The model in place of the old one: the primitive body, helmet and visor switched off, facing +Z (the view turns the
        // prefab along its walk), its design units scaled to the world's in the prefab's own scale (Heavy 1.3, Runner 0.8),
        // centred on the enemy's place with its feet on the ground; it becomes the body DefenseActorView tints, and its walk
        // plays at the enemy's speed (walkSpeed).
        static void Fit(string enemy, string model, float size)
        {
            var (walk, info) = Import(model);
            var material = Material(model);
            string path = Prefabs + enemy + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var name in new[] { "Body", "Head/Helmet", "Head/Visor" })
                { var part = root.transform.Find(name); if (part) part.gameObject.SetActive(false); }
                var disc = root.transform.Find("Base"); if (disc) Object.DestroyImmediate(disc.gameObject);
                var old = root.transform.Find("Hover") ?? root.transform.Find("Model"); // the old runner flew in Hover
                if (old) Object.DestroyImmediate(old.gameObject);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + model + ".fbx");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                instance.name = "Model";
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = walk; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var model0 = instance.transform;
                model0.localPosition = Vector3.zero; model0.localRotation = source.transform.localRotation; model0.localScale = source.transform.localScale;
                // The head is the front.
                var head = instance.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
                if (root.transform.InverseTransformPoint(head.position).z < 0) model0.localRotation = Quaternion.Euler(0, 180, 0) * model0.localRotation;
                // The design's ground is the model's origin and its top info.height above it.
                float tall = info.height * Design * size / root.transform.lossyScale.y; // in the prefab's units
                model0.localScale *= tall / LocalBounds(root.transform, renderer).max.y;
                var local = LocalBounds(root.transform, renderer);
                model0.localPosition = new Vector3(-local.center.x, 0, -local.center.z);
                root.transform.Find("Health").localPosition = Vector3.up * (tall + .1f); // the view lifts it HealthLift more

                var view = new SerializedObject(root.GetComponent<DefenseActorView>());
                view.FindProperty("body").objectReferenceValue = renderer;
                view.FindProperty("hover").objectReferenceValue = null;
                view.FindProperty("thrusters").arraySize = 0;
                view.FindProperty("walkSpeed").floatValue = info.speed * Design * size / root.transform.lossyScale.z;
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[MonsterModels] {enemy}: {model} {tall * root.transform.lossyScale.y:0.00} world units tall, " +
                          $"{renderer.sharedMesh.triangles.Length / 3} triangles, walk {info.cycle:0.00} s at {info.speed * Design * size:0.00} world units/s");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // A renderer's mesh bounds (a skinned mesh's bind pose) in another transform's local space.
        static Bounds LocalBounds(Transform space, Renderer renderer)
        {
            var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
            var toSpace = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var b = mesh.bounds; var result = new Bounds(toSpace.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(toSpace.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }
    }
}
