using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapPainterAssets
    {
        public const string Root = "Assets/!_Project/Resources/ArtScripts/MapPainter";
        public const string Generated = Root + "/Generated";
        public const string PalettePath = Generated + "/ModularMapPalette.asset";
        public const string MapPrefabs = "Assets/!_Project/Resources/ArtPrefabs";
        public const string FloorFolder = "Assets/!_Project/Resources/Map/Floor";
        public const string RoadFolder = "Assets/!_Project/Resources/Map/Road";
        public const string ArtScene = "Assets/!_Project/Scenes/ArtTestScene.unity";
        public static void EnsureFolder(string path)
        {
            if(string.IsNullOrEmpty(path)||path=="Assets")return;
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        public static List<Vector3> UsedVertices(GameObject root)
        {
            var points = new List<Vector3>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh) continue;
                var vertices = mesh.vertices;
                var used = new HashSet<int>();
                for (int s = 0; s < mesh.subMeshCount; s++) used.UnionWith(mesh.GetIndices(s));
                foreach (int i in used) points.Add(root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i])));
            }
            if (points.Count == 0) throw new InvalidOperationException(root.name + ": MeshFilter geometry is required.");
            return points;
        }
        public static Bounds BoundsOf(IReadOnlyList<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++) bounds.Encapsulate(points[i]);
            return bounds;
        }
        public static FloorShape InferFloor(IReadOnlyList<Vector3> points)
        {
            Bounds bounds = BoundsOf(points);
            var top = points.Where(p => Mathf.Abs(p.y - bounds.max.y) < 0.0005f).ToArray();
            if (top.Length < 3) throw new InvalidOperationException("No horizontal top face.");
            int edges = 0, corners = 0;
            float tolerance = Mathf.Max(bounds.size.x, bounds.size.z) * 0.002f;
            for (int i = 0; i < 4; i++)
            {
                var d = GridTopology.Directions[i];
                float full = d.x != 0 ? bounds.extents.x : bounds.extents.z;
                float actual = top.Max(p => (p.x - bounds.center.x) * d.x + (p.z - bounds.center.z) * d.z);
                if (actual < full - tolerance) edges |= 1 << i;
            }
            for (int i = 0; i < 4; i++)
            {
                int adjacent = (1 << i) | (1 << ((i + 1) % 4));
                if ((edges & adjacent) != 0) continue;
                var d = GridTopology.Diagonals[i];
                Vector3 corner = new Vector3(bounds.center.x + bounds.extents.x * d.x, bounds.max.y, bounds.center.z + bounds.extents.z * d.z);
                if (!top.Any(p => Vector3.Distance(p, corner) < tolerance)) corners |= 1 << i;
            }
            return new FloorShape(edges, corners);
        }
        public static GameObject NormalizeModule(GameObject source, bool floor, string path, out Bounds sourceBounds)
        {
            if (!source || !EditorUtility.IsPersistent(source)) throw new InvalidOperationException("Choose a model or prefab asset.");
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var wrapper = new GameObject(source.name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, wrapper.transform);
                model.transform.localPosition = Vector3.zero;
                var points = UsedVertices(wrapper);
                sourceBounds = BoundsOf(points);
                if (sourceBounds.size.x < 0.00001f || Mathf.Abs(sourceBounds.size.x - sourceBounds.size.z) > sourceBounds.size.x * 0.01f)
                    throw new InvalidOperationException(source.name + ": X/Z footprint must be square; no automatic stretching is applied.");
                // Keep the source FBX and its importer untouched. Repair oversized bounds only on a copy.
                int index = 0;
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    if (!mesh) continue;
                    var vertices = mesh.vertices;
                    var used = new HashSet<int>();
                    for (int s = 0; s < mesh.subMeshCount; s++) used.UnionWith(mesh.GetIndices(s));
                    if (used.Count == 0) continue;
                    Bounds actual = BoundsOf(used.Select(i => vertices[i]).ToArray());
                    if (mesh.bounds.size.magnitude > actual.size.magnitude * 1.05f)
                    {
                        var copy = Object.Instantiate(mesh);
                        copy.name = mesh.name + "_BoundsFixed";
                        copy.bounds = actual;
                        string meshPath = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + source.name + "_Mesh" + index + ".asset";
                        SaveMesh(copy, meshPath);
                        filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    }
                    index++;
                }
                float scale = 1f / sourceBounds.size.x;
                model.transform.localScale *= scale;
                model.transform.localPosition = -new Vector3(sourceBounds.center.x, floor ? sourceBounds.max.y : sourceBounds.min.y, sourceBounds.center.z) * scale;
                if(floor && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline)
                {
                    foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var materials=renderer.sharedMaterials;
                        for(int i=0;i<materials.Length;i++)
                            if(!materials[i] || !materials[i].shader || materials[i].shader.name=="Standard" || materials[i].shader.name=="Hidden/InternalErrorShader")
                                materials[i]=CompatibleFloorMaterial();
                        renderer.sharedMaterials=materials;
                    }
                }
                return PrefabUtility.SaveAsPrefabAsset(wrapper, path);
            }
            finally { Object.DestroyImmediate(wrapper); }
        }
        public static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                // CopySerialized does not reliably invalidate an already rendered mesh's GPU layout.
                var vertices=mesh.vertices; var normals=mesh.normals; var tangents=mesh.tangents; var colors=mesh.colors;
                var channels=new List<Vector4>[8]; for(int c=0;c<8;c++) { channels[c]=new List<Vector4>(); mesh.GetUVs(c,channels[c]); }
                var triangles=new int[mesh.subMeshCount][]; for(int s=0;s<triangles.Length;s++)triangles[s]=mesh.GetTriangles(s);
                var format=mesh.indexFormat; string name=mesh.name;
                existing.Clear(false); existing.indexFormat=format; existing.name=name;
                existing.vertices=vertices; if(normals.Length==vertices.Length)existing.normals=normals;
                if(tangents.Length==vertices.Length)existing.tangents=tangents; if(colors.Length==vertices.Length)existing.colors=colors;
                for(int c=0;c<8;c++)if(channels[c].Count==vertices.Length)existing.SetUVs(c,channels[c]);
                existing.subMeshCount=triangles.Length; for(int s=0;s<triangles.Length;s++)existing.SetTriangles(triangles[s],s);
                existing.RecalculateBounds(); existing.UploadMeshData(false); EditorUtility.SetDirty(existing);
                if(mesh!=existing)Object.DestroyImmediate(mesh);
            }
            else AssetDatabase.CreateAsset(mesh, path);
        }
        public static void RefreshMeshBuffers()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{Generated+"/Modules"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(mesh)SaveMesh(mesh,path);
            }
            AssetDatabase.SaveAssets();
        }
        private static Material CompatibleFloorMaterial()
        {
            string path=Generated+"/Modules/FloorSurface.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material)return material;
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(!shader)throw new InvalidOperationException("URP/Lit shader missing.");
            material=new Material(shader); material.name="FloorSurface";
            material.SetColor("_BaseColor",new Color(.58f,.61f,.65f)); material.SetFloat("_Smoothness",.18f);
            AssetDatabase.CreateAsset(material,path); return material;
        }
        public static ModularMapPalette Setup()
        {
            EnsureFolder(Root+"/Reports");
            EnsureFolder(Generated + "/Modules");
            EnsureFolder(MapPrefabs);
            MigrateAssetNames();
            var palette = AssetDatabase.LoadAssetAtPath<ModularMapPalette>(PalettePath);
            if (palette && palette.floors.Count >= 14 && palette.floors.All(rule => rule.prefab) && palette.roads.Count >= 6)
            { EnsureTwoTone(palette); RoadModuleAssets.Setup(palette); return palette; }
            if (!palette) { palette = ScriptableObject.CreateInstance<ModularMapPalette>(); AssetDatabase.CreateAsset(palette, PalettePath); }
            palette.floors.Clear();
            var report = new List<string> { "Modular floor source geometry audit", "Unity: " + Application.unityVersion };
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { FloorFolder }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                var prefab = NormalizeModule(source, true, Generated + "/Modules/" + source.name + ".prefab", out var bounds);
                var temporary = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                FloorShape shape;
                try { shape = InferFloor(UsedVertices(temporary)); }
                finally { Object.DestroyImmediate(temporary); }
                palette.floors.Add(new FloorRule { label = source.name, prefab = prefab, exposedEdges = shape.edges, cutCorners = shape.corners });
                report.Add($"{source.name}: source size={bounds.size:F5}; uniform scale={1f / bounds.size.x:F5}; edges={shape.edges}; corners={shape.corners}");
            }
            if (palette.floors.Count == 0) throw new InvalidOperationException("No floor FBX assets found in " + FloorFolder);
            // Fill missing rotational shapes with separate meshes; supplied modules always take priority.
            var sample = new HashSet<GridCell>();
            sample.Add(new GridCell(0, 0));
            for (int mask = 0; mask < 256; mask++)
            {
                sample.Clear(); sample.Add(new GridCell(0, 0));
                for (int i = 0; i < 4; i++)
                {
                    if ((mask & (1 << i)) != 0) sample.Add(GridTopology.Directions[i]);
                    if ((mask & (1 << (i + 4))) != 0) sample.Add(GridTopology.Diagonals[i]);
                }
                var shape = GridTopology.Floor(sample, new GridCell(0, 0));
                if (palette.TryFloor(shape, out _, out _)) continue;
                string name = $"AutoFloor_E{shape.edges}_C{shape.corners}";
                var mesh = ComplementMesh(shape);
                mesh.name = name;
                string meshPath = Generated + "/Modules/" + name + ".asset";
                SaveMesh(mesh, meshPath);
                var go = new GameObject(name);
                GameObject prefab;
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    var sourceRenderer = palette.floors[0].prefab.GetComponentInChildren<MeshRenderer>();
                    go.AddComponent<MeshRenderer>().sharedMaterial = sourceRenderer ? sourceRenderer.sharedMaterial : null;
                    prefab = PrefabUtility.SaveAsPrefabAsset(go, Generated + "/Modules/" + name + ".prefab");
                }
                finally { Object.DestroyImmediate(go); }
                palette.floors.Add(new FloorRule { label = name, prefab = prefab, exposedEdges = shape.edges, cutCorners = shape.corners, generatedComplement = true });
                report.Add(name + ": complementary unit mesh (0.025 bevel; source-style recessed groove). Supplied FBX unchanged.");
            }
            if (palette.roads.Count == 0)
            {
                palette.roads.Add(new RoadRule { label = "단독", connections = 0 });
                palette.roads.Add(new RoadRule { label = "끝점 (+Z)", connections = 1 });
                palette.roads.Add(new RoadRule { label = "직선 (+Z / -Z)", connections = 5 });
                palette.roads.Add(new RoadRule { label = "코너 (+Z / +X)", connections = 3 });
                palette.roads.Add(new RoadRule { label = "T 분기 (+Z / +X / -X)", connections = 11 });
                palette.roads.Add(new RoadRule { label = "교차", connections = 15 });
            }
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            File.WriteAllLines(Root + "/Reports/ModelAudit.txt", report);
            EnsureTwoTone(palette);
            RoadModuleAssets.Setup(palette);
            return palette;
        }
        private static void MigrateAssetNames()
        {
            string oldPalette=Generated+"/Stage01Palette.asset";
            if(AssetDatabase.LoadAssetAtPath<ModularMapPalette>(oldPalette)&&!AssetDatabase.LoadAssetAtPath<ModularMapPalette>(PalettePath))
            {
                string error=AssetDatabase.MoveAsset(oldPalette,PalettePath);
                if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
            }
            var palette=AssetDatabase.LoadAssetAtPath<ModularMapPalette>(PalettePath);
            if(palette && palette.name!="ModularMapPalette")
            { palette.name="ModularMapPalette"; EditorUtility.SetDirty(palette); AssetDatabase.SaveAssetIfDirty(palette); }
            string oldMaterial=Generated+"/Modules/Stage01Floor.mat",newMaterial=Generated+"/Modules/FloorSurface.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(oldMaterial)&&!AssetDatabase.LoadAssetAtPath<Material>(newMaterial))
            {
                string error=AssetDatabase.MoveAsset(oldMaterial,newMaterial);
                if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
                var material=AssetDatabase.LoadAssetAtPath<Material>(newMaterial); material.name="FloorSurface";
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
        }
        public static void EnsureTwoTone(ModularMapPalette palette)
        {
            if(palette.floorColorVersion>=2)return;
            if(palette.floorColorVersion==1) { RefreshFloorModules(); return; }
            var report=new List<string> { "Floor color slots: 0 = upper plate; 1 = lower body; dividing plane is the groove center at unit Y=-0.2." };
            foreach(var rule in palette.floors)
            {
                string prefabPath=AssetDatabase.GetAssetPath(rule.prefab);
                var contents=PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    int index=0;
                    foreach(var filter in contents.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var renderer=filter.GetComponent<MeshRenderer>(); if(!filter.sharedMesh||!renderer)continue;
                        int oldSlots=filter.sharedMesh.subMeshCount;
                        var matrix=contents.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                        var cleaned=ModuleMeshUtility.CleanFloor(filter.sharedMesh,matrix,rule.exposedEdges);
                        var copy=FloorColorMesh.Split(cleaned,matrix); Object.DestroyImmediate(cleaned);
                        string meshPath=Generated+"/Modules/"+rule.prefab.name+"_TwoTone"+index+".asset";
                        SaveMesh(copy,meshPath); filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        var materials=renderer.sharedMaterials;
                        var primary=materials.FirstOrDefault(m=>m);
                        if(!primary)
                        {
                            var shader=Shader.Find(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?"Universal Render Pipeline/Lit":"Standard");
                            if(!shader)throw new InvalidOperationException("No supported floor shader.");
                            string path=Generated+"/Modules/FloorSurface.mat"; primary=AssetDatabase.LoadAssetAtPath<Material>(path);
                            if(!primary) { primary=new Material(shader); AssetDatabase.CreateAsset(primary,path); }
                        }
                        renderer.sharedMaterials=new[] {MapPainterSurfaceAssets.StylizedMaterial(),MapPainterSurfaceAssets.StylizedMaterial()};
                        report.Add(rule.label+": original slots="+oldSlots+"; generated copy slots="+filter.sharedMesh.subMeshCount);
                        index++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            palette.floorColorVersion=2; EditorUtility.SetDirty(palette); AssetDatabase.SaveAssetIfDirty(palette);
            File.WriteAllLines(Root+"/Reports/FloorColors.txt",report);
        }
        public static ModularMapPalette RefreshFloorModules()
        {
            var palette=AssetDatabase.LoadAssetAtPath<ModularMapPalette>(PalettePath);
            if(!palette)return Setup();
            palette.floors.Clear(); palette.floorColorVersion=0;
            EditorUtility.SetDirty(palette); AssetDatabase.SaveAssetIfDirty(palette);
            return Setup();
        }
        // Same proportions as the supplied floor boxes. Separate assets for missing
        // combinations (isolated tile, side + notch, opposing notches, etc.).
        private static Mesh ComplementMesh(FloorShape shape)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            float[] heights = { -1f, -0.215f, -0.2f, -0.185f, -0.025f, 0f };
            var rings = new List<Vector3[]>();
            for (int r = 0; r < heights.Length; r++)
            {
                float groove = r == 2 ? 0.004791f : 0f;
                float n = .5f - groove, e = n, s = -n, w = -n;
                if (r == heights.Length - 1)
                {
                    if ((shape.edges & 1) != 0) n -= .025f;
                    if ((shape.edges & 2) != 0) e -= .025f;
                    if ((shape.edges & 4) != 0) s += .025f;
                    if ((shape.edges & 8) != 0) w += .025f;
                }
                float ne = r==heights.Length-1 && (shape.corners & 1) != 0 ? .025f : 0f;
                float se = r==heights.Length-1 && (shape.corners & 2) != 0 ? .025f : 0f;
                float sw = r==heights.Length-1 && (shape.corners & 4) != 0 ? .025f : 0f;
                float nw = r==heights.Length-1 && (shape.corners & 8) != 0 ? .025f : 0f;
                float y = heights[r];
                rings.Add(new[] { new Vector3(e-ne,y,n), new Vector3(e,y,n-ne), new Vector3(e,y,s+se), new Vector3(e-se,y,s), new Vector3(w+sw,y,s), new Vector3(w,y,s+sw), new Vector3(w,y,n-nw), new Vector3(w+nw,y,n) });
            }
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                if (Vector3.Cross(b-a,c-a).sqrMagnitude < 1e-12f) return;
                int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(a.x+.5f,a.z+.5f)); uv.Add(new Vector2(b.x+.5f,b.z+.5f)); uv.Add(new Vector2(c.x+.5f,c.z+.5f));
                triangles.Add(index); triangles.Add(index+1); triangles.Add(index+2);
            }
            for (int r = 0; r < rings.Count-1; r++)
                for (int i = 0; i < 8; i++)
                {
                    int j = (i+1)%8;
                    Triangle(rings[r][i], rings[r][j], rings[r+1][j]);
                    Triangle(rings[r][i], rings[r+1][j], rings[r+1][i]);
                }
            for (int i = 0; i < 8; i++)
            {
                int j = (i+1)%8;
                Triangle(Vector3.zero, rings[rings.Count-1][i], rings[rings.Count-1][j]);
                Triangle(new Vector3(0,-1,0), rings[0][j], rings[0][i]);
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
