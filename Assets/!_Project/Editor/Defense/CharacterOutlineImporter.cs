using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // This shader does not use tangent-space normal maps. Reuse the skinned tangent
    // stream for welded outline normals, leaving the surface's hard normals intact.
    public sealed class CharacterOutlineImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPostprocessModel(GameObject root)
        {
            const string folder = "Assets/!_Project/Resources/Models/CharacterAnimation/FBX/";
            if (assetPath != folder + "Monster.fbx" && assetPath != folder + "AttackRobot.fbx" &&
                assetPath != folder + "ShotRobot.fbx" && assetPath != folder + "TankRobot.fbx") return;

            var meshes = new HashSet<Mesh>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh) meshes.Add(renderer.sharedMesh);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh) meshes.Add(filter.sharedMesh);

            foreach (var mesh in meshes)
            {
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                if (normals.Length != vertices.Length) continue;
                var groups = new Dictionary<Vector3, HashSet<Vector3>>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!groups.TryGetValue(vertices[i], out var group))
                        groups.Add(vertices[i], group = new HashSet<Vector3>());
                    group.Add(normals[i]);
                }
                var smooth = new Dictionary<Vector3, Vector3>();
                foreach (var entry in groups)
                {
                    var sum = Vector3.zero;
                    foreach (var normal in entry.Value) sum += normal;
                    smooth.Add(entry.Key, sum.normalized);
                }
                var tangents = new Vector4[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var normal = smooth[vertices[i]];
                    if (normal.sqrMagnitude < .01f) normal = normals[i];
                    tangents[i] = new Vector4(normal.x, normal.y, normal.z, 1);
                }
                mesh.tangents = tangents;
            }
        }
    }
}
