using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // 시안 (mockup) for the tanker's walk: dumps the rig and the artist's walk, bakes an alternative walk on the same rig and
    // renders both as frames (Python puts them into 시안/탱커 걷기). Nothing here is wired into the game: Heavy.prefab and
    // MonsterTanker.controller still play MonsterTankerWalk.fbx.
    public static class TankerWalkMockup
    {
        const string Folder = "Assets/!_Project/Resources/Models/Monsters/", Out = "TestResults/Editor/tanker-walk/";
        public const string Clip = "Assets/!_Project/Mockup/TankerWalk/MonsterTanker_Walk_Mockup.anim";
        // Right front, left front, right back, left back (Heavy faces +Z, +X is its right); each leg is one bone, its pod's
        // foot at -X 0.892 (Bone025's end bone Bone026; the other three have no end bone).
        static readonly string[] Legs = { "Bone027", "Bone029", "Bone025", "Bone028" };
        const float LegLength = .892f;
        const float Speed = 1.6f; // world units per second (Balance.md, 1-1 tanker); Heavy is scaled 1.3

        // The new gait: a trot (diagonal pairs right front + left back, then left front + right back), two steps of each pair in
        // the 1 s clip so the strides stay short for the short legs, 60% of the time on the ground, so all four feet are down
        // around each footfall; planted feet go back at exactly the walk speed. A lifted pod rises fast and comes down hard (its
        // highest point at 40% of the swing), the weight landing.
        const float Length = 1, Steps = 2, Duty = .6f, Lift = .1f;
        static readonly float[] Offsets = { 0, .5f, .5f, 0 };

        public static void All() { Dump(); Bake(); Render(); }

        // Bones (hierarchy, bind pose in Heavy's space, skin weights), the artist's curves.
        public static void Dump()
        {
            Directory.CreateDirectory(Out);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var heavy = Heavy(scene, out var model);
                var walk = Walk();
                var text = new StringBuilder();
                text.AppendLine($"clip {walk.name} length {walk.length} fps {walk.frameRate} loop {walk.isLooping}");
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                var weights = skin.sharedMesh.boneWeights;
                foreach (var t in model.GetComponentsInChildren<Transform>())
                {
                    int depth = 0; for (var p = t; p != model.transform; p = p.parent) depth++;
                    int index = Array.IndexOf(skin.bones, t);
                    int vertices = index < 0 ? 0 : weights.Count(w => (w.boneIndex0 == index && w.weight0 > .5f));
                    text.AppendLine($"{new string(' ', depth * 2)}{t.name}  heavy {heavy.InverseTransformPoint(t.position):F3}  skinned vertices {vertices}");
                }
                text.AppendLine($"mesh vertices {weights.Length}, bones in skin {skin.bones.Length}");
                // Where each bone's vertices sit (bind pose, Heavy's space): which part of the model it moves.
                var baked = new Mesh(); skin.BakeMesh(baked, true);
                var points = baked.vertices;
                for (int bone = 0; bone < skin.bones.Length; bone++)
                {
                    var mine = Enumerable.Range(0, points.Length).Where(i => weights[i].boneIndex0 == bone && weights[i].weight0 > .5f).Select(i => heavy.InverseTransformPoint(skin.transform.TransformPoint(points[i]))).ToArray();
                    if (mine.Length == 0) continue;
                    var box = new Bounds(mine[0], Vector3.zero); foreach (var p in mine) box.Encapsulate(p);
                    text.AppendLine($"  {skin.bones[bone].name}: {mine.Length} vertices, min {box.min:F2} max {box.max:F2}");
                }
                Object.DestroyImmediate(baked);
                text.AppendLine("curves:");
                foreach (var b in AnimationUtility.GetCurveBindings(walk))
                {
                    var c = AnimationUtility.GetEditorCurve(walk, b);
                    text.AppendLine($"  {b.path} {b.propertyName} keys at {string.Join(" ", c.keys.Select(k => k.time.ToString("F2")))} range {c.keys.Min(k => k.value):F3}..{c.keys.Max(k => k.value):F3}");
                }
                File.WriteAllText(Out + "rig.txt", text.ToString());
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void Bake()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var heavy = Heavy(scene, out var model);
                var spine = model.transform.Find("spine");
                var hips = Legs.Select(n => spine.Find(n)).ToArray();
                Vector3 spineAt = heavy.InverseTransformPoint(spine.position); Quaternion spineTurn = spine.rotation;
                var hipTurn = hips.Select(h => h.localRotation).ToArray();
                var foot = hips.Select(h => heavy.InverseTransformPoint(h.TransformPoint(-LegLength, 0, 0))).ToArray();
                var hip = hips.Select(h => heavy.InverseTransformPoint(h.position)).ToArray();
                float leg = Vector3.Distance(hip[0], foot[0]), side = Mathf.Abs(foot[0].x - hip[0].x), down = Mathf.Sqrt(leg * leg - side * side);
                float period = Length / Steps, stride = Speed / heavy.lossyScale.z * period * Duty; // in Heavy's units

                var clip = new AnimationClip { frameRate = 30 };
                var paths = new[] { spine }.Concat(hips).Select(t => AnimationUtility.CalculateTransformPath(t, model.transform)).ToArray();
                var curves = paths.Select(_ => Enumerable.Range(0, 7).Select(__ => new AnimationCurve()).ToArray()).ToArray();
                float worst = 0;
                for (int f = 0; f <= 30; f++)
                {
                    float time = Length * f / 30;
                    // Each foot's target in Heavy's space: planted and sliding back at the walk speed, then lifted and swung forward.
                    var target = new Vector3[4]; var planted = new bool[4];
                    for (int i = 0; i < 4; i++)
                    {
                        float u = Mathf.Repeat(time / period + Offsets[i], 1);
                        planted[i] = u < Duty;
                        float s = planted[i] ? u / Duty : (u - Duty) / (1 - Duty);
                        float z = planted[i] ? stride / 2 - stride * s : -stride / 2 + stride * s * s * (3 - 2 * s);
                        target[i] = foot[i] + new Vector3(0, planted[i] ? 0 : Lift * Mathf.Sin(Mathf.PI * Mathf.Pow(s, .75f)), z);
                    }
                    // The body rides exactly as low as the planted legs (one rigid bone each) reach at their slant: a bob at
                    // every step, lowest when the planted pods lean furthest. Any roll, dip or nod on top of it would slide the
                    // planted feet sideways (the legs hang almost straight down, so 1 cm of hip height is 3 cm of foot), so none.
                    float reach = Enumerable.Range(0, 4).Where(i => planted[i]).Average(i => Mathf.Sqrt(down * down - Sq(target[i].z - foot[i].z)) - down);
                    spine.position = heavy.TransformPoint(spineAt + Vector3.up * reach);
                    spine.rotation = spineTurn;
                    // Each leg aimed at its foot. Planted: the foot's sideways and forward place as planned, its height wherever
                    // the leg's length puts it (within 1 cm of the floor while all four are down, the two pairs leaning
                    // differently); lifted: height and forward place as planned, out sideways as far as the leg puts it.
                    for (int i = 0; i < 4; i++)
                    {
                        var h = hips[i]; h.localRotation = hipTurn[i];
                        var at = heavy.InverseTransformPoint(h.position);
                        float dx = target[i].x - at.x, dy = target[i].y - at.y, dz = target[i].z - at.z;
                        // A lifted pod never tucks in under the body (just after lift-off the body can sit too low for it).
                        bool fixedSide = planted[i] || leg * leg - dz * dz - dy * dy < dx * dx;
                        float room = leg * leg - dz * dz - (fixedSide ? dx * dx : dy * dy);
                        if (room < 0) worst = Mathf.Max(worst, -room);
                        if (fixedSide) target[i].y = at.y - Mathf.Sqrt(Mathf.Max(room, 0));
                        else target[i].x = at.x + Mathf.Sign(foot[i].x - hip[i].x) * Mathf.Sqrt(Mathf.Max(room, 0));
                        var now = h.TransformPoint(-LegLength, 0, 0) - h.position;
                        h.rotation = Quaternion.FromToRotation(now, heavy.TransformPoint(target[i]) - h.position) * h.rotation;
                    }
                    var bones = new[] { spine }.Concat(hips).ToArray();
                    for (int b = 0; b < bones.Length; b++)
                    {
                        var p = bones[b].localPosition; var q = bones[b].localRotation;
                        var values = new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                        for (int c = 0; c < 7; c++) curves[b][c].AddKey(time, values[c]);
                    }
                }
                string[] names = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int b = 0; b < paths.Length; b++)
                    for (int c = b == 0 ? 0 : 3; c < 7; c++) // legs: rotation only, as the artist's clip
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(paths[b], typeof(Transform), names[c]), curves[b][c]);
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
                Directory.CreateDirectory(Path.GetDirectoryName(Clip)); AssetDatabase.Refresh();
                AssetDatabase.DeleteAsset(Clip); AssetDatabase.CreateAsset(clip, Clip); AssetDatabase.SaveAssets();
                Debug.Log($"[TankerWalkMockup] baked {Clip}: stride {stride * heavy.lossyScale.z:F2} world units, leg {leg:F3}, unreachable {worst:F4}");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static float Sq(float x) => x * x;

        // Both walks, 2 s at 30 fps, the enemy moving at its speed over a checker floor and the camera following it, from the
        // side and three-quarters; plus each foot's world position per frame (feet_*.csv) to measure sliding.
        public static void Render()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var heavy = Heavy(scene, out var model);
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.scene = scene;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.86f, .88f, .9f);
                camera.orthographic = true; camera.orthographicSize = 1.05f; camera.nearClipPlane = .1f; camera.farClipPlane = 50;
                var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.transform.rotation = Quaternion.Euler(55, -30, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.rotation = Quaternion.Euler(90, 0, 0); floor.transform.localScale = Vector3.one * 40;
                var checker = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                Color a = new Color(.97f, .96f, .92f), b = new Color(.72f, .74f, .78f);
                checker.SetPixels(new[] { a, b, b, a }); checker.Apply();
                var ground = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                ground.SetTexture("_BaseMap", checker); ground.SetTextureScale("_BaseMap", Vector2.one * 40 / (2 * .4f)); ground.SetFloat("_Smoothness", 0);
                floor.GetComponent<Renderer>().sharedMaterial = ground;

                var spine = model.transform.Find("spine");
                var hips = Legs.Select(n => spine.Find(n)).ToArray();
                // The skinned mesh does not deform in a preview scene's Camera.Render: its pose is baked into a plain mesh per frame.
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                var posed = new GameObject("Posed", typeof(MeshFilter), typeof(MeshRenderer));
                posed.transform.SetParent(skin.transform, false);
                posed.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                var mesh = new Mesh(); posed.GetComponent<MeshFilter>().sharedMesh = mesh; skin.enabled = false;
                var clips = new[] { ("current", Walk()), ("new", AssetDatabase.LoadAssetAtPath<AnimationClip>(Clip)) };
                var views = new[] { ("side", new Vector3(1, .2f, 0), 1.05f), ("three", new Vector3(1, .75f, 1.1f), 1.35f) };
                foreach (var (name, clip) in clips)
                {
                    Directory.CreateDirectory(Out + name);
                    var csv = new StringBuilder("t," + string.Join(",", Legs.Select(l => $"{l}.x,{l}.y,{l}.z")) + "\n");
                    for (int f = 0; f < 60; f++)
                    {
                        float time = f / 30f;
                        heavy.position = new Vector3(0, 0, -1.6f + Speed * time);
                        clip.SampleAnimation(model, Mathf.Repeat(time, clip.length));
                        skin.BakeMesh(mesh, true);
                        csv.Append(time.ToString("F4"));
                        foreach (var h in hips) { var p = h.TransformPoint(-LegLength, 0, 0); csv.Append($",{p.x:F4},{p.y:F4},{p.z:F4}"); }
                        csv.AppendLine();
                        foreach (var (view, direction, size) in views)
                        {
                            var look = heavy.position + Vector3.up * .72f;
                            camera.transform.position = look + direction.normalized * 12; camera.transform.LookAt(look); camera.orthographicSize = size;
                            Shoot(camera, $"{Out}{name}/{view}_{f:00}.png");
                        }
                    }
                    File.WriteAllText($"{Out}feet_{name}.csv", csv.ToString());
                }
                Object.DestroyImmediate(checker); Object.DestroyImmediate(ground); Object.DestroyImmediate(mesh);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("[TankerWalkMockup] frames → " + Out);
        }

        static AnimationClip Walk() =>
            AssetDatabase.LoadAllAssetsAtPath(Folder + "MonsterTankerWalk.fbx").OfType<AnimationClip>().First(c => c.name == "MonsterTanker_Walk");

        // The Heavy prefab as the game places it, its model's Animator off so the clips are sampled by hand.
        static Transform Heavy(UnityEngine.SceneManagement.Scene scene, out GameObject model)
        {
            var heavy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Defense/Prefabs/Heavy.prefab"), scene);
            PrefabUtility.UnpackPrefabInstance(heavy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var health = heavy.transform.Find("Health"); if (health) health.gameObject.SetActive(false);
            foreach (var line in heavy.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false; // the attack beam, a streak at the origin
            model = heavy.transform.Find("Model").gameObject;
            model.GetComponent<Animator>().enabled = false;
            return heavy.transform;
        }

        // 640 px, linear-to-sRGB, downscaled by Python for smooth edges.
        static void Shoot(Camera camera, string path)
        {
            var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(640, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        }
    }
}
