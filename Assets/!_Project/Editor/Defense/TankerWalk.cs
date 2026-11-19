using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The tanker's walk (MonsterTankerWalk.anim), baked on the artist's rig in MonsterTanker.fbx: the artist's own walk only
    // kicked each leg forward and back, so the planted feet slid along with the body (시안/탱커 걷기). Called by
    // MonsterModels.Tanker(); reads Heavy.prefab with the model already fitted (its scale and bind pose).
    public static class TankerWalk
    {
        public const string Clip = "Assets/!_Project/Resources/Models/Monsters/MonsterTankerWalk.anim";
        // Right front, left front, right back, left back (Heavy faces +Z, +X is its right); each leg is one bone, its pod's
        // foot at -X 0.892 (Bone025's end bone Bone026; the other three have no end bone).
        static readonly string[] Legs = { "Bone027", "Bone029", "Bone025", "Bone028" };
        const float LegLength = .892f;

        // A trot (diagonal pairs right front + left back, then left front + right back), two steps of each pair in the 1 s clip
        // so the strides stay short for the short legs, 60% of the time on the ground, so all four feet are down around each
        // footfall; planted feet go back at exactly DefenseActorView.TankerWalkSpeed (the view plays the clip faster or slower
        // with the enemy's speed). A lifted pod rises fast and comes down hard (its highest point at 40% of the swing).
        const float Length = 1, Steps = 2, Duty = .6f, Lift = .1f;
        static readonly float[] Offsets = { 0, .5f, .5f, 0 };

        public static AnimationClip Bake()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var heavy = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Defense/Prefabs/Heavy.prefab"), scene)).transform;
                var model = heavy.Find("Model").gameObject;
                var spine = model.transform.Find("spine");
                var hips = Legs.Select(n => spine.Find(n)).ToArray();
                Vector3 spineAt = heavy.InverseTransformPoint(spine.position); Quaternion spineTurn = spine.rotation;
                var hipTurn = hips.Select(h => h.localRotation).ToArray();
                var foot = hips.Select(h => heavy.InverseTransformPoint(h.TransformPoint(-LegLength, 0, 0))).ToArray();
                var hip = hips.Select(h => heavy.InverseTransformPoint(h.position)).ToArray();
                float leg = Vector3.Distance(hip[0], foot[0]), side = Mathf.Abs(foot[0].x - hip[0].x), down = Mathf.Sqrt(leg * leg - side * side);
                float period = Length / Steps, stride = DefenseActorView.TankerWalkSpeed / heavy.lossyScale.z * period * Duty; // in Heavy's units

                var clip = new AnimationClip { name = "MonsterTankerWalk", frameRate = 30 };
                var bones = new[] { spine }.Concat(hips).ToArray();
                var paths = bones.Select(t => AnimationUtility.CalculateTransformPath(t, model.transform)).ToArray();
                var curves = paths.Select(_ => Enumerable.Range(0, 7).Select(__ => new AnimationCurve()).ToArray()).ToArray();
                float worst = 0;
                for (int f = 0; f <= 30; f++)
                {
                    float time = Length * f / 30;
                    // Each foot's target in Heavy's space: planted and going back at the walk speed, then lifted and swung forward.
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
                    for (int b = 0; b < bones.Length; b++)
                    {
                        var p = bones[b].localPosition; var q = bones[b].localRotation;
                        var values = new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                        for (int c = 0; c < 7; c++) curves[b][c].AddKey(time, values[c]);
                    }
                }
                string[] names = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int b = 0; b < paths.Length; b++)
                    for (int c = b == 0 ? 0 : 3; c < (b == 0 ? 3 : 7); c++) // the body only bobs, the legs only turn
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(paths[b], typeof(Transform), names[c]), curves[b][c]);
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
                // Into the existing asset when there is one, so the controller's reference (its GUID) stays.
                var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clip);
                if (saved) { EditorUtility.CopySerialized(clip, saved); Object.DestroyImmediate(clip); EditorUtility.SetDirty(saved); }
                else AssetDatabase.CreateAsset(saved = clip, Clip);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TankerWalk] baked {Clip}: stride {stride * heavy.lossyScale.z:F2} world units, leg {leg:F3}, unreachable {worst:F4}");
                return saved;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static float Sq(float x) => x * x;
    }
}
