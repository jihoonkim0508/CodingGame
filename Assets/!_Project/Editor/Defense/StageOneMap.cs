using System.Linq;
using CodingGame.Art.MapPainter;
using CodingGame.Art.MapPainter.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Stage 1's own map: one short straight road from the error gate to the core (play feedback: the workshop map was too big
    // for the first lessons). Built next to the workshop map in the Defense scene with its tiles, colours and wide road, the
    // gate, the core model and the workshop props that stand on its cells; DefenseBattle shows it for Stage 1 only and moves
    // the camera to Stage1View. Rerunnable.
    public static class StageOneMap
    {
        const string Scene = "Assets/!_Project/Scenes/Defense.unity", Name = "Stage1Map", View = "Stage1View";
        // Cells. The base is the workshop's (11, -1), so the core and its health bar stay where they are.
        const int Left = -1, Right = 12, Bottom = -3, Top = 1, Road = -1, Start = 0, Base = 11;

        [MenuItem("Tools/Develop/1스테이지 일자 맵 만들기")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 종료 후 실행하세요.");
            // Opening the scene alone drops unsaved edits in the open scenes, so refuse then and reopen them afterwards.
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, EditorSceneManager.sceneCount).Any(i => EditorSceneManager.GetSceneAt(i).isDirty))
                throw new System.InvalidOperationException("저장하지 않은 씬이 있습니다. 저장 후 실행하세요.");
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var workshop = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ModularMapRoot>(true)).First(m => m.name == "Map");
            var world = workshop.transform.parent;
            var old = world.Find(Name); if (old) Object.DestroyImmediate(old.gameObject);

            var map = MapPainterEditing.CreateRoot(scene, workshop.palette, false);
            map.name = Name; map.transform.SetParent(world, false);
            map.transform.localPosition = workshop.transform.localPosition; map.transform.localRotation = workshop.transform.localRotation;
            map.transform.localScale = workshop.transform.localScale;
            // The workshop's look (colours, shading, backdrop); the cells, groups and routes are this map's own.
            map.cellSize = workshop.cellSize; map.floorHeight = workshop.floorHeight;
            map.useFloorColors = workshop.useFloorColors; map.upperColor = workshop.upperColor; map.lowerColor = workshop.lowerColor;
            map.useRoadColors = workshop.useRoadColors; map.roadInnerColor = workshop.roadInnerColor; map.roadBorderColor = workshop.roadBorderColor;
            map.lightSoftness = workshop.lightSoftness; map.shadowStrength = workshop.shadowStrength; map.bodyGradient = workshop.bodyGradient;
            map.bevelHighlight = workshop.bevelHighlight; map.blueprintBackground = workshop.blueprintBackground; map.blueprintBlue = workshop.blueprintBlue;
            map.blueprintLines = workshop.blueprintLines; map.blueprintSpacing = workshop.blueprintSpacing; map.blueprintLineWidth = workshop.blueprintLineWidth;
            map.blueprintMinorOpacity = workshop.blueprintMinorOpacity; map.blueprintMajorOpacity = workshop.blueprintMajorOpacity; map.blueprintMajorEvery = workshop.blueprintMajorEvery;

            var floor = from x in Enumerable.Range(Left, Right - Left + 1) from z in Enumerable.Range(Bottom, Top - Bottom + 1) select new GridCell(x, z);
            MapPainterEditing.Paint(map, floor.ToList(), false, false, false);
            MapPainterEditing.Paint(map, Enumerable.Range(Start, Base - Start + 1).Select(x => new GridCell(x, Road)).ToList(), true, false, false);
            MapRouteEditing.SetBase(map, new GridCell(Base, Road));
            int route = MapRouteEditing.AddStart(map, new GridCell(Start, Road));
            if (route < 0 || !MapRouteEditing.AutoRoute(map, map.routes[route])) throw new System.InvalidOperationException("일자 길의 경로를 만들 수 없습니다.");
            map.roadWidthScale = workshop.roadWidthScale;
            WideRoadAssets.Apply(map, false);
            // The painter's tiles come with its plain surface material; the workshop's desk wood and cream road are on its renderers.
            Restyle(map.floorGroup, workshop.floorGroup); Restyle(map.roadGroup, workshop.roadGroup);

            // The gate at the start, the core model at the base, and the workshop props that stand on these cells off the road.
            var art = new GameObject("Stage1_Art").transform; art.SetParent(map.transform, false);
            var source = workshop.transform.Find("Stage01_Workshop_Art");
            var cells = new System.Collections.Generic.HashSet<GridCell>(map.floorCells);
            foreach (Transform prop in source)
            {
                var cell = workshop.WorldToCell(prop.position);
                bool keep = prop.name == "CodeBase" || cells.Contains(cell) && cell.z != Road;
                if (prop.name == "ErrorGate") keep = true;
                if (!keep) continue;
                // A plain copy: the props are nested in the workshop map prefab, and instantiating their source spawned that
                // whole map (with no routes) for each prop.
                var copy = Object.Instantiate(prop.gameObject, art);
                copy.name = prop.name; copy.transform.SetPositionAndRotation(prop.position, prop.rotation); copy.transform.localScale = prop.lossyScale.x / art.lossyScale.x * Vector3.one;
                if (prop.name == "ErrorGate")
                    copy.transform.position = map.CellPosition(new GridCell(Start, Road)) + Vector3.up * (prop.position.y - workshop.transform.position.y) - map.transform.right * (map.cellSize * map.transform.lossyScale.x * .5f);
            }
            var camera = Camera.main;
            BlueprintBackdropAssets.Ensure(map, camera, false);
            map.ApplyFloorColors();

            // The battle camera's angle and lens, placed so each map with its props fits the part of a 16:9 screen the HUD leaves
            // free (play feedback: the workshop's monitor went under the top bar and the core's health bar touched the edge).
            // The scene's camera pose is the workshop's view (DefenseBattle keeps it for Stages 2 and 3).
            var view = world.Find(View) ?? new GameObject(View).transform; view.SetParent(world, true);
            view.SetPositionAndRotation(Fit(camera, Visible(map)), camera.transform.rotation);
            camera.transform.position = Fit(camera, Visible(workshop));

            var battle = Object.FindFirstObjectByType<DefenseBattle>(FindObjectsInactive.Include);
            var wiring = new SerializedObject(battle);
            wiring.FindProperty("stageOneMap").objectReferenceValue = map;
            wiring.FindProperty("workshopMap").objectReferenceValue = workshop;
            wiring.FindProperty("stageOneView").objectReferenceValue = view;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            map.gameObject.SetActive(false); // DefenseBattle turns it on for Stage 1
            // Layout groups lay out their children only on a canvas update; saving right after opening wrote them as zeros.
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[StageOneMap] {map.floorCells.Count} floor cells, road {map.roadCells.Count}, props {art.childCount}, Stage 1 view {view.position}, workshop view {camera.transform.position}");
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        // The box corners of every renderer of the map but the blueprint backdrop (far larger than the map). Per renderer, not one
        // box round the map: that box's top corners would lift the tall monitor's height over the whole desk.
        static Vector3[] Visible(ModularMapRoot map) => map.GetComponentsInChildren<Renderer>()
            .Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer) && !r.GetComponentInParent<BlueprintBackdrop>())
            .SelectMany(r => Enumerable.Range(0, 8).Select(i => r.bounds.center + Vector3.Scale(r.bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))).ToArray();
        // Pixels kept clear on every side at 1080p (the core's health bar sits above the core).
        const float Margin = 40;
        // The camera position, at the camera's own angle, that shows all the points inside its view on a 16:9 screen. The battle
        // camera already draws only between the HUD's top bar and robot cards (its viewport rect), so the margin is all it needs.
        static Vector3 Fit(Camera camera, Vector3[] corners)
        {
            var rotation = camera.transform.rotation;
            Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right, up = rotation * Vector3.up;
            var center = corners.Aggregate(Vector3.zero, (sum, p) => sum + p) / corners.Length;
            var view = camera.rect; float width = 1920 * view.width, height = 1080 * view.height;
            var free = Rect.MinMaxRect(Margin / width, Margin / height, 1 - Margin / width, 1 - Margin / height);
            var saved = camera.transform.position; camera.aspect = width / height;
            float tanY = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f), tanX = tanY * camera.aspect, distance = 30;
            Vector2 shift = Vector2.zero;
            for (int pass = 0; pass < 40; pass++)
            {
                camera.transform.position = center - forward * distance + right * shift.x + up * shift.y;
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                foreach (var corner in corners) { Vector2 v = camera.WorldToViewportPoint(corner); min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
                var offset = (min + max) * .5f - free.center;
                shift += new Vector2(offset.x * 2 * distance * tanX, offset.y * 2 * distance * tanY);
                distance *= Mathf.Max((max.x - min.x) / free.width, (max.y - min.y) / free.height);
            }
            var fitted = camera.transform.position;
            camera.transform.position = saved; camera.ResetAspect();
            return fitted;
        }

        static void Restyle(Transform group, Transform from)
        {
            var materials = from.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.sharedMaterials).FirstOrDefault(m => m.Length > 0 && m.All(x => x));
            if (materials == null) throw new System.InvalidOperationException(from.name + ": 작업실 맵의 머티리얼을 찾을 수 없습니다.");
            foreach (var renderer in group.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterials = materials;
        }
    }
}
