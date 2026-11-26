using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The tanker's footfall dust (TankerDust), one 시안 per particle texture already in the project or from Unity Labs:
    // Build() makes a material and an effect prefab per option in Defense/Effects/TankerDust, Attach() puts TankerDust on
    // Heavy.prefab with the chosen one (MonsterModels.Apply calls it after refitting the model), Record() renders each option
    // walking into TestResults/Editor/tanker-dust (Python turns the frames into the GIFs in 시안/탱커 흙먼지).
    public static class TankerDustEffects
    {
        const string Folder = "Assets/!_Project/Defense/Effects/TankerDust/", Heavy = "Assets/!_Project/Defense/Prefabs/Heavy.prefab";
        const string Out = "TestResults/Editor/tanker-dust/";
        public const string Default = "TankerDust1_Kenney";
        static readonly string[] Feet = { "Foot_L", "Foot_R" }; // the shield brute's sole bones (build_game.py)

        // name, texture, flipbook tiles, stretched streaks, size, speed, lifetime, particles per footfall, brightness, alpha (시안1 at 50%: 시안/탱커 흙먼지 농도)
        static readonly (string name, string texture, int tiles, bool streak, float size, float speed, float life, int perStep, float bright, float alpha)[] Options =
        {
            ("TankerDust1_Kenney", "Assets/ThirdParty/Kenney/Particles/smoke_07.png", 1, false, 1f, .6f, 1f, 3, 1.1f, .5f),
            ("TankerDust2_Travis", "Assets/ThirdParty/Travis Game Assets/Hit Impact Effects/Textures/HIE_Smoke_01.tga", 1, false, .8f, .65f, .85f, 3, 1f, 1f),
            ("TankerDust3_Piloto", Folder + "Dust_Trail_Alpha.png", 1, true, .45f, 1.7f, .55f, 6, 1.3f, 1f),
            ("TankerDust4_UnityLabs", "Assets/ThirdParty/UnityLabs VFX/WispySmoke01_8x8.tga", 8, false, 1.15f, .5f, 1.1f, 3, 1.5f, 1f),
        };

        public static void All() { Build(); Attach(); Record(); }

        public static void Build()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            // Piloto's trail is grey on black with a solid alpha: a copy whose alpha is its brightness.
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Dust_Trail_Alpha.png"))
            {
                AssetDatabase.CopyAsset("Assets/ThirdParty/Piloto Studio/Textures/Dust_Trail.png", Folder + "Dust_Trail_Alpha.png");
                var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "Dust_Trail_Alpha.png");
                importer.alphaSource = TextureImporterAlphaSource.FromGrayScale; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            }
            // The project's URP particle material (the hit glow), alpha-blended as the runner's thruster flame is.
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/!_Project/Defense/Effects/HIE_Hit_04_PointGlow_01.mat");
            foreach (var o in Options)
            {
                var material = new Material(template) { name = o.name };
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(o.texture));
                material.SetColor("_BaseColor", new Color(o.bright, o.bright, o.bright, 1));
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", 1); material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // Soft particles (the PC pipeline asset renders depth): the puffs fade into the road instead of a hard cut line.
                material.SetFloat("_SoftParticlesEnabled", 1); material.EnableKeyword("_SOFTPARTICLES_ON");
                material.SetFloat("_SoftParticlesNearFadeDistance", 0); material.SetFloat("_SoftParticlesFarFadeDistance", .5f);
                material.SetVector("_SoftParticleFadeParams", new Vector4(0, 1 / .5f, 0, 0));
                AssetDatabase.DeleteAsset(Folder + o.name + ".mat"); AssetDatabase.CreateAsset(material, Folder + o.name + ".mat");

                var go = new GameObject(o.name, typeof(ParticleSystem));
                try
                {
                    var dust = go.GetComponent<ParticleSystem>();
                    dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = dust.main;
                    main.duration = 1; main.loop = true; main.playOnAwake = true; main.maxParticles = 40;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(o.life * .75f, o.life);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(o.speed * .6f, o.speed);
                    main.startSize = new ParticleSystem.MinMaxCurve(o.size * .7f, o.size);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, 2 * Mathf.PI);
                    // Earth brown, a lighter and a darker grain.
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, .37f, .2f, o.alpha), new Color(.74f, .56f, .34f, o.alpha));
                    main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.gravityModifier = o.streak ? .6f : 0;
                    var emission = dust.emission; emission.rateOverTime = 0; // TankerDust emits on each footfall
                    var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.rotation = new Vector3(-90, 0, 0);
                    shape.angle = o.streak ? 55 : 70; shape.radius = .12f;
                    // Kicked out, then hanging low and slowing.
                    var limit = dust.limitVelocityOverLifetime; limit.enabled = !o.streak; limit.limit = .05f; limit.dampen = .12f;
                    var size = dust.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1, o.streak ? AnimationCurve.Linear(0, 1, 1, .6f) : new AnimationCurve(new Keyframe(0, .55f), new Keyframe(1, 1.4f)));
                    var gradient = new Gradient();
                    gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                        new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.7f, .5f), new GradientAlphaKey(0, 1) });
                    var color = dust.colorOverLifetime; color.enabled = true; color.color = gradient;
                    var turn = dust.rotationOverLifetime; turn.enabled = !o.streak; turn.z = new ParticleSystem.MinMaxCurve(-.8f, .8f);
                    if (o.tiles > 1)
                    {
                        var sheet = dust.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = sheet.numTilesY = o.tiles;
                        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1));
                    }
                    var look = go.GetComponent<ParticleSystemRenderer>();
                    look.sharedMaterial = material; look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; look.receiveShadows = false;
                    if (o.streak) { look.renderMode = ParticleSystemRenderMode.Stretch; look.lengthScale = 1.2f; look.velocityScale = .2f; }
                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + o.name + ".prefab");
                    PrefabUtility.SavePrefabAsset(prefab);
                }
                finally { Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[TankerDustEffects] built " + string.Join(", ", Options.Select(o => o.name)));
        }

        // TankerDust on Heavy.prefab with its two foot bones; keeps the option already chosen, else Default.
        public static void Attach()
        {
            var root = PrefabUtility.LoadPrefabContents(Heavy);
            try
            {
                Set(Dust(root), root.transform, null);
                PrefabUtility.SaveAsPrefabAsset(root, Heavy);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Switch Heavy to another option, e.g. -executeMethod ...TankerDustEffects.Choose2 (or edit TankerDust's Effect field).
        public static void Choose1() => Choose(Options[0].name);
        public static void Choose2() => Choose(Options[1].name);
        public static void Choose3() => Choose(Options[2].name);
        public static void Choose4() => Choose(Options[3].name);
        static void Choose(string option)
        {
            var root = PrefabUtility.LoadPrefabContents(Heavy);
            try { Set(Dust(root), root.transform, option); PrefabUtility.SaveAsPrefabAsset(root, Heavy); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static TankerDust Dust(GameObject heavy) { var dust = heavy.GetComponent<TankerDust>(); return dust ? dust : heavy.AddComponent<TankerDust>(); }

        static void Set(TankerDust dust, Transform heavy, string option)
        {
            var serialized = new SerializedObject(dust);
            var effect = serialized.FindProperty("effect");
            if (option != null || !effect.objectReferenceValue)
                effect.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ParticleSystem>(Folder + (option ?? Default) + ".prefab");
            var chosen = Options.FirstOrDefault(o => effect.objectReferenceValue && o.name == effect.objectReferenceValue.name);
            if (chosen.name != null) serialized.FindProperty("perStep").intValue = chosen.perStep;
            var bones = heavy.Find("Model").GetComponentsInChildren<Transform>();
            var legs = serialized.FindProperty("legs"); legs.arraySize = Feet.Length;
            for (int i = 0; i < Feet.Length; i++) legs.GetArrayElementAtIndex(i).objectReferenceValue = bones.First(b => b.name == Feet[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Each option, the tanker walking 3 s (after 1 s for the dust to build up) at its game speed across a cream road:
        // left the battle camera's angle and distance (47.5° down, 33 units, a 480 px crop of a 1080p screen), right a close
        // three-quarter view following it.
        public static void Record() { foreach (var o in Options) Record(o.name, o.name, 1); }

        // 시안1 at 40-100% strength (시안/탱커 흙먼지 농도): its puffs' alpha scaled, the brown kept; the same random seed in
        // each, so only the strength differs between them.
        public static void RecordStrengths()
        {
            for (int percent = 40; percent <= 100; percent += 10) Record(Options[0].name, $"Strength{percent}", percent / 100f, 1);
        }

        static void Record(string option, string folder, float strength, uint seed = 0)
        {
            {
                Directory.CreateDirectory(Out + folder);
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var heavy = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Heavy), scene)).transform;
                    PrefabUtility.UnpackPrefabInstance(heavy.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    var health = heavy.Find("Health"); if (health) health.gameObject.SetActive(false);
                    foreach (var line in heavy.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
                    var model = heavy.Find("Model").gameObject;
                    model.GetComponent<Animator>().enabled = false;
                    var dust = Dust(heavy.gameObject);
                    Set(dust, heavy, option);
                    if (strength < 1 || seed != 0)
                    {
                        // A copy of the effect in the preview scene with the strength and seed, for TankerDust to emit from.
                        var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ParticleSystem>(Folder + option + ".prefab"));
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy.gameObject, scene);
                        copy.transform.position = Vector3.down * 100;
                        var main = copy.main; var c = main.startColor;
                        Color lo = c.colorMin, hi = c.colorMax; lo.a *= strength; hi.a *= strength;
                        main.startColor = new ParticleSystem.MinMaxGradient(lo, hi);
                        if (seed != 0) { copy.useAutoRandomSeed = false; copy.randomSeed = seed; }
                        var so = new SerializedObject(dust); so.FindProperty("effect").objectReferenceValue = copy; so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var walk = model.GetComponent<Animator>().runtimeAnimatorController.animationClips[0];
                    // The skinned mesh does not deform in a preview scene's Camera.Render: its pose is baked into a plain mesh per frame.
                    var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                    var posed = new GameObject("Posed", typeof(MeshFilter), typeof(MeshRenderer));
                    posed.transform.SetParent(skin.transform, false);
                    posed.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    var mesh = new Mesh(); posed.GetComponent<MeshFilter>().sharedMesh = mesh; skin.enabled = false;

                    var camera = Add<Camera>(scene, "Camera");
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.55f, .7f, .5f);
                    camera.fieldOfView = 19; camera.nearClipPlane = .3f; camera.farClipPlane = 100;
                    var light = Add<Light>(scene, "Light"); light.type = LightType.Directional; light.intensity = 1.25f; light.shadows = LightShadows.Soft;
                    light.transform.rotation = Quaternion.Euler(55, -30, 0);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
                    floor.transform.rotation = Quaternion.Euler(90, 0, 0); floor.transform.localScale = Vector3.one * 60;
                    var checker = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                    Color a = new Color(.93f, .89f, .8f), b = new Color(.88f, .84f, .74f);
                    checker.SetPixels(new[] { a, b, b, a }); checker.Apply();
                    var ground = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    ground.SetTexture("_BaseMap", checker); ground.SetTextureScale("_BaseMap", Vector2.one * 30); ground.SetFloat("_Smoothness", 0);
                    floor.GetComponent<Renderer>().sharedMaterial = ground;

                    heavy.rotation = Quaternion.LookRotation(Vector3.right);
                    ParticleSystem particles = null;
                    float speed = heavy.GetComponent<DefenseActorView>().WalkSpeed * heavy.lossyScale.z; const float start = -4;
                    for (int f = -30; f < 90; f++)
                    {
                        float time = f / 30f;
                        heavy.position = new Vector3(start + speed * (time + 1), 0, 0);
                        walk.SampleAnimation(model, Mathf.Repeat(time + 1, walk.length));
                        dust.Tick();
                        particles ??= heavy.GetComponentsInChildren<ParticleSystem>().FirstOrDefault(); // the instance TankerDust made
                        if (particles) particles.Simulate(1 / 30f, true, false, false);
                        if (f < 0) continue;
                        skin.BakeMesh(mesh, true);
                        var middle = new Vector3(start + speed * 2.5f, .5f, 0);
                        camera.fieldOfView = 19;
                        camera.transform.rotation = Quaternion.Euler(47.5f, 0, 0); camera.transform.position = middle - camera.transform.forward * 32.9f;
                        Shoot(camera, $"{Out}{folder}/game_{f:00}.png");
                        var look = heavy.position + Vector3.up * .45f;
                        camera.fieldOfView = 19;
                        camera.transform.position = look + new Vector3(.8f, .75f, -1).normalized * 14; camera.transform.LookAt(look);
                        Shoot(camera, $"{Out}{folder}/close_{f:00}.png");
                    }
                    Object.DestroyImmediate(checker); Object.DestroyImmediate(ground); Object.DestroyImmediate(mesh);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Debug.Log("[TankerDustEffects] frames → " + Out);
        }

        static T Add<T>(UnityEngine.SceneManagement.Scene scene, string name) where T : Component
        {
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var c = go.AddComponent<T>();
            if (c is Camera camera) camera.scene = scene;
            return c;
        }

        static void Shoot(Camera camera, string path)
        {
            var target = new RenderTexture(480, 480, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(480, 480, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 480, 480), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        }
    }
}
