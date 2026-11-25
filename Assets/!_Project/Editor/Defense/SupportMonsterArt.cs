using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The 2-4 / 3-4 monsters' own models and effects in place of the tinted placeholders (SupportEnemies). Art~/monsters/
    // build_new_monsters.py (Blender, headless) writes the models into Models/Monsters/Support: one skinned mesh per model,
    // palette coloured, a looping "Walk" take. Apply() imports them, makes Healer/Shielder/QueueCentipede/StackCentipede (and
    // the not yet wired Reflector) prefabs from Enemy.prefab, builds the effect prefabs (Resources/Effects/Monsters, loaded by
    // DefenseActorView and DefenseHitVisuals), points the enemy definitions at the prefabs and photographs their icons.
    // Needs a GPU for the icons: Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.SupportMonsterArt.Apply -quit
    public static class SupportMonsterArt
    {
        const string Models = "Assets/!_Project/Resources/Models/Monsters/Support/", Prefabs = "Assets/!_Project/Defense/Prefabs/",
            Data = "Assets/!_Project/Defense/Data/", Fx = "Assets/!_Project/Resources/Effects/Monsters/", FxArt = "Assets/!_Project/Defense/Effects/Monsters/";
        // How much bigger than the Blender units each monster stands on the road (Enemy.prefab is scale 1; the glitch normal is 1.35 tall).
        const float Scale = 1.7f, QueueScale = 1.3f, StackScale = 1.5f;
        // The stack's crates (build_new_monsters.py): a layer is CRATE_H + SEAM = .32 tall, the walking base's crate top .525
        // over its feet (in Blender units; the models' own origin is their lowest point).
        const float LayerStep = .32f, BaseTop = .525f;

        [Serializable] sealed class Info { public float height = 1, speed, cycle, emission = 1; }

        public static void Apply()
        {
            Directory.CreateDirectory(Fx); Directory.CreateDirectory(FxArt);
            AssetDatabase.Refresh();
            Effects();
            Single("Healer", "MonsterHealer", "Cross");
            Single("Shielder", "MonsterShielder", "Dome");
            Single("Reflector", "MonsterReflector", "Crystal");
            Parts("QueueCentipede", QueueScale, new[] { "MonsterQueueHead", "MonsterQueueBody", "MonsterQueueTail" }, 0, 0);
            Parts("StackCentipede", StackScale, new[] { "MonsterStackBase", "MonsterStackLayer0", "MonsterStackLayer1", "MonsterStackLayer2", "MonsterStackLayer3" },
                BaseTop * StackScale, LayerStep * StackScale);
            foreach (var name in new[] { "Healer", "Shielder", "QueueCentipede", "StackCentipede" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Data + name + ".asset");
                definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab").GetComponent<DefenseActorView>();
                if (name == "StackCentipede") definition.description = SupportEnemies.StackDescription; // a pile of crates now, not a centipede
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) RobotIcons.Build();
            Debug.Log("[SupportMonsterArt] models, prefabs and effects applied");
        }

        static Info Info_(string model) => JsonUtility.FromJson<Info>(File.ReadAllText(Models + model + ".json"));

        // The FBX as a Generic rig whose one clip loops, its palettes read pixel for pixel, URP Lit materials (opaque and
        // see-through) and a controller playing the walk (MonsterModels.Import on the model branch).
        static (Material[] materials, RuntimeAnimatorController walk) Import(string model)
        {
            string path = Models + model + ".fbx";
            var maps = new[] { "_Palette", "_Emission" }.Select(map =>
            {
                string png = Models + model + map + ".png";
                var texture = (TextureImporter)AssetImporter.GetAtPath(png);
                texture.filterMode = FilterMode.Point; texture.mipmapEnabled = false; texture.wrapMode = TextureWrapMode.Clamp;
                texture.textureCompression = TextureImporterCompression.Uncompressed; texture.sRGBTexture = true; texture.alphaIsTransparency = false;
                texture.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            }).ToArray();
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips) { c.name = "Walk"; c.loopTime = true; }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var walk = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            string controllerPath = Models + model + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath)
                ?? UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, walk);
            var state = controller.layers[0].stateMachine.defaultState;
            state.name = "Walk"; state.motion = walk;
            EditorUtility.SetDirty(controller);
            int parts = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().First().subMeshCount; // Opaque, then Glass if any
            var materials = Enumerable.Range(0, parts).Select(i => Lit(model + (i == 0 ? "" : "_Glass"), maps[0], maps[1], i > 0, Info_(model).emission)).ToArray();
            return (materials, controller);
        }

        static Material Lit(string name, Texture2D palette, Texture2D emission, bool glass, float strength)
        {
            string path = Models + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_BaseMap", palette); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_EmissionMap", emission); material.SetColor("_EmissionColor", Color.white * strength);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .35f);
            material.SetFloat("_Surface", glass ? 1 : 0); material.SetFloat("_Blend", 0);
            BaseShaderGUI.SetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A copy of Enemy.prefab (its view, beam and health anchor) with its model swapped out.
        static GameObject Open(string enemy)
        {
            string path = Prefabs + enemy + ".prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) AssetDatabase.CopyAsset(Prefabs + "Enemy.prefab", path);
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var name in new[] { "Body", "Head/Helmet", "Head/Visor" }) { var part = root.transform.Find(name); if (part) part.gameObject.SetActive(false); }
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name == "Model" || child.name.StartsWith("Model ") || child.name == "Base" || child.name == "Hover") Object.DestroyImmediate(child.gameObject);
            return root;
        }

        // The model standing on the ground at the enemy's place (its origin is the feet centre), turned to face +Z.
        static SkinnedMeshRenderer Place(Transform root, string model, float scale, string name)
        {
            var (materials, walk) = Import(model);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root);
            instance.name = name;
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.updateWhenOffscreen = true;
            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = walk; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.Euler(0, 180, 0) * source.transform.localRotation;
            instance.transform.localScale = source.transform.localScale;
            instance.transform.localScale *= scale * Info_(model).height / LocalBounds(root, renderer).size.y;
            instance.transform.localPosition = Vector3.up * -LocalBounds(root, renderer).min.y;
            return renderer;
        }

        static void Single(string enemy, string model, string pulse)
        {
            var root = Open(enemy);
            try
            {
                var renderer = Place(root.transform, model, Scale, "Model");
                root.transform.Find("Health").localPosition = Vector3.up * (LocalBounds(root.transform, renderer).size.y + .25f);
                var view = new SerializedObject(root.GetComponent<DefenseActorView>());
                view.FindProperty("body").objectReferenceValue = renderer;
                view.FindProperty("pulsePart").objectReferenceValue = renderer.transform.parent.GetComponentsInChildren<Transform>().First(t => t.name == pulse);
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + enemy + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // A centipede's head/body/tail models, or a pile's walking base crate and its four layer colours, side by side under the
        // view (DefenseActorView shows the one the segment is); pileBase/pileStep: how high the layers above the base sit.
        static void Parts(string enemy, float scale, string[] models, float pileBase, float pileStep)
        {
            var root = Open(enemy);
            try
            {
                var renderers = models.Select((m, i) => Place(root.transform, m, scale, "Model " + m.Replace("Monster", ""))).ToArray();
                for (int i = 0; i < renderers.Length; i++) renderers[i].transform.parent.gameObject.SetActive(i == 1);
                float tall = pileStep > 0 ? pileStep : LocalBounds(root.transform, renderers[1]).size.y;
                root.transform.Find("Health").localPosition = Vector3.up * (tall + .15f);
                var view = new SerializedObject(root.GetComponent<DefenseActorView>());
                view.FindProperty("body").objectReferenceValue = renderers[1];
                var list = view.FindProperty("parts"); list.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i].transform.parent.gameObject;
                view.FindProperty("pileBase").floatValue = pileBase; view.FindProperty("pileStep").floatValue = pileStep;
                view.FindProperty("deathEffect").stringValue = "SegmentDeath";
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + enemy + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Bounds LocalBounds(Transform space, Renderer renderer)
        {
            var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
            var toSpace = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var b = mesh.bounds; var result = new Bounds(toSpace.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(toSpace.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        // ---------------------------------------------------------------- effects
        // Particle materials on the hit glow's URP particle shader with the Kenney particle pack (CC0) and a drawn plus sign.

        static Material Particle(string name, Texture texture, bool additive)
        {
            string path = FxArt + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/!_Project/Defense/Effects/HIE_Hit_04_PointGlow_01.mat"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", additive ? 2 : 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", 1); material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(material);
            return material;
        }
        static Material Rim(string name, Color color, Color rim, float lines, float speed, float strength)
        {
            string path = FxArt + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("CodingGame/MonsterRim")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetColor("_RimColor", rim);
            material.SetFloat("_Lines", lines); material.SetFloat("_LineSpeed", speed); material.SetFloat("_LineStrength", strength);
            EditorUtility.SetDirty(material);
            return material;
        }
        static Material Unlit(string name, Color color)
        {
            string path = FxArt + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
        static Texture2D Kenney(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Kenney/Particles/" + name + ".png");

        // A soft white plus sign for the heal effect's rising particles.
        static Texture2D Plus()
        {
            string path = FxArt + "Plus.png";
            if (!File.Exists(path))
            {
                const int n = 64; var image = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float u = Mathf.Abs(x + .5f - n / 2f), v = Mathf.Abs(y + .5f - n / 2f);
                    float d = Mathf.Min(Mathf.Max(u - 8, v - 26), Mathf.Max(u - 26, v - 8)); // < 0 inside the plus
                    image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(.5f - d / 3)));
                }
                File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.alphaIsTransparency = true; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Gradient Fade(Color a, Color b, float hold = .5f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, hold), new GradientAlphaKey(0, 1) });
            return g;
        }
        // One particle system under parent (or the root when parent is null): a burst, not playing on its own (DefenseHitVisuals
        // steps it on the battle clock; DefenseActorView plays the ones it shows itself).
        static ParticleSystem System(string name, Transform parent, Material material, int count, Vector2 life, Vector2 speed, Vector2 size,
            Gradient color, ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard, float gravity = 0, ParticleSystemShapeType shape = ParticleSystemShapeType.Sphere,
            float radius = .1f, AnimationCurve grow = null, bool loop = false, float rate = 0)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            if (parent) go.transform.SetParent(parent, false);
            var p = go.GetComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main;
            main.duration = 1; main.loop = loop; main.playOnAwake = loop; main.maxParticles = Math.Max(count, 64);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y); main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y); main.gravityModifier = gravity;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = p.emission; emission.rateOverTime = rate;
            if (count > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var sh = p.shape; sh.shapeType = shape; sh.radius = radius;
            if (shape == ParticleSystemShapeType.Circle) go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var col = p.colorOverLifetime; col.enabled = true; col.color = color;
            if (grow != null) { var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, grow); }
            var look = go.GetComponent<ParticleSystemRenderer>();
            look.renderMode = mode; look.sharedMaterial = material; look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; look.receiveShadows = false;
            if (mode == ParticleSystemRenderMode.Stretch) { look.lengthScale = 2.5f; look.velocityScale = .05f; }
            return p;
        }
        static AnimationCurve Out(float from = .2f) => new AnimationCurve(new Keyframe(0, from, 0, 3), new Keyframe(1, 1, 0, 0));
        static void Save(GameObject root, string name) { PrefabUtility.SaveAsPrefabAsset(root, Fx + name + ".prefab"); Object.DestroyImmediate(root); }
        static void Save(ParticleSystem root, string name) => Save(root.gameObject, name);

        static void Effects()
        {
            var ring = Particle("FxRing", Kenney("circle_03"), false);
            var glow = Particle("FxGlow", Kenney("light_01"), true);
            var spark = Particle("FxSpark", Kenney("spark_01"), false);
            var star = Particle("FxStar", Kenney("star_04"), false);
            var smoke = Particle("FxSmoke", Kenney("smoke_04"), false);
            var plus = Particle("FxPlus", Plus(), false);
            Color green = new Color(.45f, 1, .6f), white = Color.white, blue = new Color(.45f, .8f, 1), cream = new Color(1, .95f, .82f);

            // Heal: a green ring rushing out to the heal radius (3) on the ground, green and white plus signs rising.
            var heal = System("HealPulse", null, ring, 1, new Vector2(.6f, .6f), Vector2.zero, new Vector2(6.5f, 6.5f), Fade(green, green, .3f),
                ParticleSystemRenderMode.HorizontalBillboard, grow: Out(.1f), radius: 0);
            heal.transform.localPosition = Vector3.zero;
            var pluses = System("Plus", heal.transform, plus, 14, new Vector2(.7f, 1f), new Vector2(.6f, 1.2f), new Vector2(.22f, .38f), Fade(white, green, .4f),
                shape: ParticleSystemShapeType.Circle, radius: 1.2f, gravity: -.25f);
            var m = pluses.main; m.startRotation = 0;
            System("Glow", heal.transform, glow, 1, new Vector2(.35f, .35f), Vector2.zero, new Vector2(2f, 2f), Fade(green, green, .2f), radius: 0);
            Save(heal, "HealPulse");

            // Shield cast: a blue ring out to its radius (2.5) and blue sparks rising.
            var cast = System("ShieldCast", null, ring, 1, new Vector2(.6f, .6f), Vector2.zero, new Vector2(5.5f, 5.5f), Fade(blue, blue, .3f),
                ParticleSystemRenderMode.HorizontalBillboard, grow: Out(.1f), radius: 0);
            System("Sparks", cast.transform, star, 12, new Vector2(.5f, .8f), new Vector2(1f, 2f), new Vector2(.15f, .3f), Fade(white, blue, .3f),
                shape: ParticleSystemShapeType.Hemisphere, radius: .4f);
            Save(cast, "ShieldCast");

            // 막힘: grey-white sparks glancing off and a quick dull flash (a hit on a protected segment or pile layer).
            var blocked = System("BlockedSpark", null, spark, 10, new Vector2(.18f, .3f), new Vector2(3f, 5f), new Vector2(.12f, .2f), Fade(white, new Color(.6f, .65f, .7f), .3f),
                ParticleSystemRenderMode.Stretch, gravity: 1.5f, shape: ParticleSystemShapeType.Hemisphere, radius: .05f);
            blocked.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            System("Flash", blocked.transform, glow, 1, new Vector2(.15f, .15f), Vector2.zero, new Vector2(1.1f, 1.1f), Fade(new Color(.85f, .9f, 1), white, .2f), radius: 0);
            System("Ring", blocked.transform, ring, 1, new Vector2(.25f, .25f), Vector2.zero, new Vector2(1.2f, 1.2f), Fade(new Color(.8f, .85f, .95f), white, .2f), grow: Out(.3f), radius: 0);
            Save(blocked, "BlockedSpark");

            // A layer landing on the pile with a thump: a wide flat dust ring, puffs and stars.
            var land = System("StackPush", null, smoke, 12, new Vector2(.35f, .55f), new Vector2(1.5f, 2.6f), new Vector2(.35f, .6f), Fade(cream, cream, .3f),
                shape: ParticleSystemShapeType.Circle, radius: .45f);
            System("Ring", land.transform, ring, 1, new Vector2(.35f, .35f), Vector2.zero, new Vector2(2.8f, 2.8f), Fade(white, cream, .2f),
                ParticleSystemRenderMode.HorizontalBillboard, grow: Out(.3f), radius: 0);
            System("Stars", land.transform, star, 5, new Vector2(.3f, .45f), new Vector2(1.5f, 2.5f), new Vector2(.12f, .2f), Fade(white, new Color(1, .9f, .5f), .3f),
                shape: ParticleSystemShapeType.Circle, radius: .3f);
            Save(land, "StackPush");

            // A segment or pile layer done for: a white pop, stars and a smoke puff.
            var death = System("SegmentDeath", null, glow, 1, new Vector2(.2f, .2f), Vector2.zero, new Vector2(1.8f, 1.8f), Fade(white, white, .2f), radius: 0);
            System("Stars", death.transform, star, 12, new Vector2(.35f, .6f), new Vector2(2.5f, 4f), new Vector2(.15f, .3f), Fade(white, new Color(1, .85f, .5f), .4f),
                gravity: 1, radius: .2f);
            System("Smoke", death.transform, smoke, 6, new Vector2(.4f, .6f), new Vector2(.4f, .9f), new Vector2(.5f, .8f), Fade(new Color(.9f, .9f, .95f), white, .2f), radius: .2f);
            Save(death, "SegmentDeath");

            // Shield break: glassy shards flying out with gravity, a blue flash and ring.
            var shardMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var shardMat = Unlit("FxShard", new Color(.7f, .93f, 1));
            var shatter = System("ShieldShatter", null, glow, 1, new Vector2(.2f, .2f), Vector2.zero, new Vector2(2.4f, 2.4f), Fade(blue, white, .2f), radius: 0);
            var shards = System("Shards", shatter.transform, shardMat, 18, new Vector2(.5f, .8f), new Vector2(2.5f, 4.5f), new Vector2(.06f, .14f), Fade(white, blue, .6f),
                ParticleSystemRenderMode.Mesh, gravity: 2, radius: .5f);
            shards.GetComponent<ParticleSystemRenderer>().mesh = shardMesh;
            var sm = shards.main; sm.startRotation3D = true; sm.startRotationX = sm.startRotationY = sm.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            sm.startSize3D = true; sm.startSizeX = new ParticleSystem.MinMaxCurve(.08f, .18f); sm.startSizeY = new ParticleSystem.MinMaxCurve(.02f, .04f); sm.startSizeZ = new ParticleSystem.MinMaxCurve(.08f, .16f);
            var spin = shards.rotationOverLifetime; spin.enabled = true; spin.separateAxes = true; spin.x = spin.y = spin.z = new ParticleSystem.MinMaxCurve(-8, 8);
            System("Ring", shatter.transform, ring, 1, new Vector2(.3f, .3f), Vector2.zero, new Vector2(2.6f, 2.6f), Fade(blue, white, .2f), grow: Out(.4f), radius: 0);
            Save(shatter, "ShieldShatter");

            // The shield bubble (DefenseActorView scales it round the model): a rim-lit sphere with sparkles drifting on it.
            var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            var bubble = new GameObject("ShieldBubble", typeof(MeshFilter), typeof(MeshRenderer));
            bubble.GetComponent<MeshFilter>().sharedMesh = sphere;
            var br = bubble.GetComponent<MeshRenderer>(); br.sharedMaterial = Rim("FxShieldBubble", new Color(.45f, .8f, 1, .1f), new Color(.65f, .95f, 1, .95f), 10, 1.5f, .18f);
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
            System("Sparkles", bubble.transform, star, 0, new Vector2(.6f, .9f), new Vector2(0, .1f), new Vector2(.05f, .1f), Fade(white, blue, .5f),
                radius: .5f, loop: true, rate: 10).GetComponent<ParticleSystem>();
            Save(bubble, "ShieldBubble");

            // The hittable segment's marker: a fat yellow arrow (bobbing over it, DefenseActorView) and a glowing ring at its feet.
            var marker = new GameObject("OpenEndMarker");
            var arrowModel = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "MonsterMarker.fbx");
            var arrow = new GameObject("Arrow", typeof(MeshFilter), typeof(MeshRenderer)); arrow.transform.SetParent(marker.transform, false);
            arrow.GetComponent<MeshFilter>().sharedMesh = arrowModel.GetComponentInChildren<MeshFilter>().sharedMesh;
            var ar = arrow.GetComponent<MeshRenderer>(); ar.sharedMaterial = Unlit("FxMarkerArrow", new Color(1, .86f, .2f)); ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var halo = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer)); halo.transform.SetParent(marker.transform, false);
            halo.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            halo.transform.localRotation = Quaternion.Euler(90, 0, 0); halo.transform.localScale = Vector3.one * 1.4f;
            var hr = halo.GetComponent<MeshRenderer>(); hr.sharedMaterial = Particle("FxMarkerRing", Kenney("circle_03"), false); hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hr.sharedMaterial.SetColor("_BaseColor", new Color(1, .88f, .25f, 1));
            Save(marker, "OpenEndMarker");

            // Reflector (not wired yet): its reflect-phase shimmer (a mirror-lilac rim shell with fast lines and glints, looping)
            // and the flash when it sends damage back (white-cyan rays, a ring and streaks back toward the attacker, +Z).
            var shimmer = new GameObject("ReflectShimmer", typeof(MeshFilter), typeof(MeshRenderer));
            shimmer.GetComponent<MeshFilter>().sharedMesh = sphere;
            var sr = shimmer.GetComponent<MeshRenderer>(); sr.sharedMaterial = Rim("FxReflectShimmer", new Color(.85f, .8f, 1, .08f), new Color(.75f, 1, 1, 1), 22, 6, .45f);
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            System("Glints", shimmer.transform, star, 0, new Vector2(.25f, .4f), Vector2.zero, new Vector2(.12f, .25f), Fade(white, new Color(.8f, .9f, 1), .3f),
                radius: .5f, loop: true, rate: 18);
            Save(shimmer, "ReflectShimmer");
            var flash = System("ReflectFlash", null, Particle("FxRays", AssetDatabase.LoadAssetAtPath<Material>("Assets/!_Project/Defense/Effects/HIE_Hit_04_FlashRays_01.mat").GetTexture("_BaseMap"), true),
                1, new Vector2(.25f, .25f), Vector2.zero, new Vector2(2.2f, 2.2f), Fade(new Color(.75f, 1, 1), white, .2f), radius: 0);
            System("Ring", flash.transform, ring, 1, new Vector2(.3f, .3f), Vector2.zero, new Vector2(2.4f, 2.4f), Fade(new Color(.85f, .8f, 1), white, .2f), grow: Out(.2f), radius: 0);
            var streaks = System("Streaks", flash.transform, spark, 10, new Vector2(.2f, .3f), new Vector2(6f, 9f), new Vector2(.12f, .2f), Fade(white, new Color(.7f, 1, 1), .3f),
                ParticleSystemRenderMode.Stretch, shape: ParticleSystemShapeType.Cone, radius: .1f);
            var cone = streaks.shape; cone.angle = 18;
            Save(flash, "ReflectFlash");
            AssetDatabase.SaveAssets();
        }
    }
}
