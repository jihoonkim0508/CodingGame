using System;
using System.Linq;
using CodingGame.Art.MapPainter;
using UnityEngine;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense
{
    public sealed class DefenseRoute : MonoBehaviour
    {
        [SerializeField] Transform[] waypoints = Array.Empty<Transform>();
        [SerializeField, Min(.1f)] float halfWidth = 1.1f;
        [Tooltip("Optional road mesh shown only while a wave uses this route; leave empty to always show.")]
        [SerializeField] GameObject visual;
        // Found in the scene rather than linked, so swapping the map prefab (the painter saves a new one) needs no relinking
        // and can never fall back to the stale waypoints above.
        [Tooltip("Use the scene's painted map (Modular Map Painter): its start → base path number mapRoute gives the waypoints instead of the list above.")]
        [SerializeField] bool useMap;
        [SerializeField, Min(0)] int mapRoute;
        public ModularMapRoot Map => useMap ? FindAnyObjectByType<ModularMapRoot>() : null;
        public void ShowVisual(bool show) { if (visual && visual.activeSelf != show) visual.SetActive(show); }
        public Route Read()
        {
            if (useMap)
            {
                var map = Map;
                if (!map) throw new InvalidOperationException(name + ": 씬에 Modular Map이 없습니다.");
                if (mapRoute >= map.routes.Count || !map.IsComplete(map.routes[mapRoute]))
                    throw new InvalidOperationException(name + ": 맵의 S" + (mapRoute + 1) + " 경로가 기지까지 이어지지 않았습니다. Modular Map Painter에서 출발·경로·기지를 지정하세요.");
                return new Route(map.Waypoints(map.routes[mapRoute]).Select(p => new Point(p.x, p.z)), halfWidth);
            }
            if (waypoints.Length < 2 || waypoints.Any(p => !p)) throw new InvalidOperationException(name + ": 경로 참조를 연결하세요.");
            return new Route(waypoints.Select(p => new Point(p.position.x, p.position.z)), halfWidth);
        }
        // Draws the path the game walks: the map's when useMap is on, else the waypoint list.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .7f, .2f);
            var map = Map;
            var points = useMap
                ? map && mapRoute < map.routes.Count ? map.Waypoints(map.routes[mapRoute]) : Array.Empty<Vector3>()
                : waypoints.Where(p => p).Select(p => p.position).ToArray();
            for (int i = 0; i < points.Length; i++)
            {
                Gizmos.DrawWireSphere(points[i], halfWidth);
                if (i > 0) Gizmos.DrawLine(points[i - 1], points[i]);
            }
        }
    }
}
