using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapPainterEditing
    {
        static bool IsOutermostPrefabRoot(ModularMapRoot root) =>
            PrefabUtility.IsPartOfPrefabInstance(root) && PrefabUtility.GetOutermostPrefabInstanceRoot(root.gameObject) == root.gameObject;
        public static bool CanEdit(ModularMapRoot root) => root && root.palette && !EditorApplication.isPlayingOrWillChangePlaymode
            && root.gameObject.scene.path == MapPainterAssets.ArtScene
            && (!PrefabUtility.IsPartOfPrefabInstance(root) || IsOutermostPrefabRoot(root))
            && Quaternion.Angle(root.transform.rotation, Quaternion.identity) < .001f
            && Vector3.Distance(root.transform.lossyScale, Vector3.one) < .001f;

        public static ModularMapRoot CreateRoot(Scene scene, ModularMapPalette palette, bool undo = true)
        {
            var go = new GameObject("Modular Map");
            SceneManager.MoveGameObjectToScene(go, scene);
            var root = go.AddComponent<ModularMapRoot>();
            root.palette = palette;
            EnsureGroups(root, false);
            MapBuildAssets.Ensure(root,false);
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Create modular map");
            return root;
        }
        private static void EnsureGroups(ModularMapRoot root, bool undo)
        {
            if (IsOutermostPrefabRoot(root))
                PrefabUtility.UnpackPrefabInstance(root.gameObject, PrefabUnpackMode.OutermostRoot, undo ? InteractionMode.UserAction : InteractionMode.AutomatedAction);
            if (undo) Undo.RecordObject(root, "Update map groups");
            if (!root.floorGroup) root.floorGroup = Group("Floors", root.transform, undo);
            if (!root.roadGroup) root.roadGroup = Group("Roads", root.transform, undo);
        }
        private static Transform Group(string name, Transform parent, bool undo)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Create map layer");
            return go.transform;
        }
        public static int Paint(ModularMapRoot root, IEnumerable<GridCell> cells, bool road, bool erase, bool undo = true)
        {
            if (!root || !root.palette) throw new InvalidOperationException("A map and palette are required.");
            EnsureGroups(root, undo);
            if (undo) Undo.RecordObject(root, "Paint map cells");
            var floors = new HashSet<GridCell>(root.floorCells);
            var roads = new HashSet<GridCell>(root.roadCells);
            var changed = new HashSet<GridCell>();
            foreach (var cell in cells)
            {
                if (road)
                {
                    if (erase ? roads.Remove(cell) : floors.Contains(cell) && roads.Add(cell)) changed.Add(cell);
                }
                else if (erase)
                {
                    if (floors.Remove(cell)) { roads.Remove(cell); changed.Add(cell); }
                }
                else if (floors.Add(cell)) changed.Add(cell);
            }
            if (changed.Count == 0) return 0;
            root.floorCells = floors.OrderBy(c => c.z).ThenBy(c => c.x).ToList();
            root.roadCells = roads.OrderBy(c => c.z).ThenBy(c => c.x).ToList();
            var affected = new HashSet<GridCell>();
            foreach (var cell in changed)
                for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++) affected.Add(cell + new GridCell(x, z));
            Rebuild(root, affected, false, undo);
            root.ApplyFloorColors();
            EditorUtility.SetDirty(root);
            if (!EditorSceneManager.IsPreviewScene(root.gameObject.scene)) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return changed.Count;
        }
        public static void RebuildAll(ModularMapRoot root, bool undo = true)
        {
            EnsureGroups(root, undo);
            var cells = new HashSet<GridCell>(root.floorCells); cells.UnionWith(root.roadCells);
            foreach (var tile in root.GetComponentsInChildren<MapTileMarker>(true)) cells.Add(tile.cell);
            Rebuild(root, cells, true, undo);
            root.ApplyFloorColors();
        }
        private static void Rebuild(ModularMapRoot root, IEnumerable<GridCell> cells, bool force, bool undo)
        {
            var floors = new HashSet<GridCell>(root.floorCells); var roads = new HashSet<GridCell>(root.roadCells);
            var existing = new Dictionary<(GridCell, bool), MapTileMarker>();
            foreach (var tile in root.GetComponentsInChildren<MapTileMarker>(true))
            {
                if (!root.IsTile(tile)) continue;
                var key = (tile.cell, tile.road);
                if (existing.ContainsKey(key)) Destroy(tile.gameObject, undo);
                else existing.Add(key, tile);
            }
            foreach (var cell in cells)
            {
                GameObject floor = null, road = null; int floorTurns = 0, roadTurns = 0;
                if (floors.Contains(cell) && !root.palette.TryFloor(GridTopology.Floor(floors, cell), out floor, out floorTurns))
                    throw new InvalidOperationException("Missing floor shape at " + cell + ". Check the palette.");
                if (roads.Contains(cell)) root.palette.TryRoad(GridTopology.Connections(roads, cell), out road, out roadTurns);
                UpdateTile(root, cell, false, floor, floorTurns, existing, force, undo);
                UpdateTile(root, cell, true, road, roadTurns, existing, force, undo);
            }
        }
        private static void UpdateTile(ModularMapRoot root, GridCell cell, bool road, GameObject prefab, int turns,
            Dictionary<(GridCell, bool), MapTileMarker> existing, bool force, bool undo)
        {
            existing.TryGetValue((cell, road), out var tile);
            if (tile && !force && tile.source == prefab && tile.turns == turns) return;
            if (tile) Destroy(tile.gameObject, undo);
            if (!prefab) return;
            var go = new GameObject($"{(road ? "Road" : "Floor")}_{cell.x}_{cell.z}");
            go.transform.SetParent(road ? root.roadGroup : root.floorGroup, false);
            go.transform.localPosition = root.TileLocalPosition(cell, road);
            var marker = go.AddComponent<MapTileMarker>(); marker.cell = cell; marker.road = road; marker.source = prefab; marker.turns = turns;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, turns * 90f, 0);
            model.transform.localScale = root.ModuleScale(road);
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Place map module");
        }
        private static void Destroy(GameObject go, bool undo)
        {
            if (undo) Undo.DestroyObjectImmediate(go); else Object.DestroyImmediate(go);
        }
        public static Bounds MapBounds(ModularMapRoot root)
        {
            if (root.floorCells.Count == 0) return new Bounds(root.transform.position + Vector3.down * .5f, new Vector3(16,1,10));
            var first = root.CellPosition(root.floorCells[0]) + Vector3.down * root.floorHeight*.5f;
            var size=new Vector3(root.cellSize,root.floorHeight,root.cellSize);
            var bounds = new Bounds(first, size);
            foreach (var cell in root.floorCells) bounds.Encapsulate(new Bounds(root.CellPosition(cell) + Vector3.down * root.floorHeight*.5f, size));
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                if(!renderer.GetComponent<BlueprintBackdrop>())bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        public static void FitCamera(Camera camera, Bounds bounds, float pitch = 55, bool undo = true)
        {
            if (!camera) throw new InvalidOperationException("No camera found in ArtTestScene.");
            if (undo) { Undo.RecordObject(camera, "Frame map camera"); Undo.RecordObject(camera.transform, "Frame map camera"); }
            Quaternion rotation = Quaternion.Euler(pitch, 0, 0);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
            float width = 0, height = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = Vector3.Scale(bounds.extents, new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                width = Mathf.Max(width, Mathf.Abs(Vector3.Dot(right,p)));
                height = Mathf.Max(height, Mathf.Abs(Vector3.Dot(up,p)));
            }
            float aspect = camera.aspect > .01f ? camera.aspect : 16f/9f;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1, Mathf.Max(height,width/aspect)*1.15f);
            camera.transform.rotation = rotation;
            float distance = Mathf.Max(30,bounds.size.magnitude*2);
            camera.transform.position = bounds.center - rotation*Vector3.forward*distance;
            camera.nearClipPlane = .1f; camera.farClipPlane = Mathf.Max(200,distance*3);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BlueprintBackdrop.DefaultCameraColor;
            foreach(var go in camera.gameObject.scene.GetRootGameObjects())
                foreach(var backdrop in go.GetComponentsInChildren<BlueprintBackdrop>(true))
                { backdrop.targetCamera=camera; backdrop.Refresh(); }
            if (undo && PrefabUtility.IsPartOfPrefabInstance(camera))
            { PrefabUtility.RecordPrefabInstancePropertyModifications(camera); PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform); }
        }
        public static void FitSceneView(ModularMapRoot root)
        {
            var bounds = MapBounds(root);
            var view = SceneView.lastActiveSceneView;
            if (view) view.LookAt(bounds.center, Quaternion.Euler(55,0,0), Mathf.Max(4,bounds.size.magnitude*.6f), true);
        }
        public static ModularMapRoot FindMap(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ModularMapRoot>(true)).FirstOrDefault();
        public static Camera FindCamera(Scene scene)
        {
            var cameras = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)).ToArray();
            return cameras.FirstOrDefault(c => c.CompareTag("MainCamera")) ?? cameras.FirstOrDefault();
        }
    }
}
