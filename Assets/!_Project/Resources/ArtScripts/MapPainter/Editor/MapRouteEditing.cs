using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    // Base, entrances and per-entrance walking paths on a painted map. Every edit is undoable.
    public static class MapRouteEditing
    {
        static void Record(ModularMapRoot map, string label)
        {
            Undo.RecordObject(map, label);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        }

        public static bool SetBase(ModularMapRoot map, GridCell cell)
        {
            if (!map.floorCells.Contains(cell)) return false;
            Record(map, "Set base");
            map.hasBase = true; map.baseCell = cell;
            return true;
        }

        // Returns the new route's index, or -1 when the cell is off the floor or already an entrance.
        public static int AddStart(ModularMapRoot map, GridCell cell)
        {
            if (!map.floorCells.Contains(cell) || map.routes.Any(r => r.start.Equals(cell))) return -1;
            Record(map, "Add start");
            map.routes.Add(new MapRoute { start = cell });
            return map.routes.Count - 1;
        }

        public static void RemoveRoute(ModularMapRoot map, int index)
        {
            Record(map, "Remove start");
            map.routes.RemoveAt(index);
        }

        public static void ClearPath(ModularMapRoot map, MapRoute route)
        {
            Record(map, "Clear path");
            route.path.Clear();
        }

        // Clicking the start or a cell already on the path cuts the path back to it. Any other floor cell extends the
        // path: along painted road when both ends connect through it, otherwise in a straight grid line.
        public static bool Extend(ModularMapRoot map, MapRoute route, GridCell cell)
        {
            int existing = route.path.IndexOf(cell);
            if (cell.Equals(route.start) || existing >= 0)
            {
                Record(map, "Trim path");
                route.path.RemoveRange(existing + 1, route.path.Count - existing - 1);
                return true;
            }
            var floors = new HashSet<GridCell>(map.floorCells);
            if (map.IsComplete(route) || !floors.Contains(cell)) return false;
            var roads = new HashSet<GridCell>(map.roadCells) { route.End, cell };
            var steps = GridTopology.ShortestPath(roads, route.End, cell) ?? GridTopology.Stroke(route.End, cell).Skip(1).ToList();
            if (map.hasBase && steps.IndexOf(map.baseCell) is int baseIndex && baseIndex >= 0) steps = steps.Take(baseIndex + 1).ToList();
            if (steps.Any(c => !floors.Contains(c) || c.Equals(route.start) || route.path.Contains(c))) return false;
            Record(map, "Extend path");
            route.path.AddRange(steps);
            return true;
        }

        // Shortest road walk from the entrance to the base; false when the road does not connect them.
        public static bool AutoRoute(ModularMapRoot map, MapRoute route)
        {
            if (!map.hasBase) return false;
            var walkable = new HashSet<GridCell>(map.roadCells) { route.start, map.baseCell };
            var path = GridTopology.ShortestPath(walkable, route.start, map.baseCell);
            if (path == null) return false;
            Record(map, "Auto route");
            route.path.Clear(); route.path.AddRange(path);
            return true;
        }

        public static List<string> Problems(ModularMapRoot map)
        {
            var problems = new List<string>();
            var floors = new HashSet<GridCell>(map.floorCells);
            if (!map.hasBase) problems.Add("기지를 지정하세요.");
            else if (!floors.Contains(map.baseCell)) problems.Add("기지 칸의 바닥이 지워졌습니다.");
            if (map.routes.Count == 0) problems.Add("출발 지점을 하나 이상 추가하세요.");
            for (int i = 0; i < map.routes.Count; i++)
            {
                var route = map.routes[i];
                if (!map.IsComplete(route)) problems.Add($"S{i + 1}: 경로가 기지까지 이어지지 않았습니다.");
                if (!floors.Contains(route.start) || route.path.Any(c => !floors.Contains(c))) problems.Add($"S{i + 1}: 바닥이 지워진 칸을 지납니다.");
            }
            return problems;
        }
    }
}
