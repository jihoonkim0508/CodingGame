using System;
using System.Linq;
using UnityEngine;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense
{
    public sealed class DefenseRoute : MonoBehaviour
    {
        [SerializeField] Transform[] waypoints = Array.Empty<Transform>();
        [SerializeField, Min(.1f)] float halfWidth = 1.1f;
        public Route Read()
        {
            if (waypoints.Length < 2 || waypoints.Any(p => !p)) throw new InvalidOperationException(name + ": 경로 참조를 연결하세요.");
            return new Route(waypoints.Select(p => new Point(p.position.x, p.position.z)), halfWidth);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .7f, .2f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (!waypoints[i]) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, halfWidth);
                if (i > 0 && waypoints[i - 1]) Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
            }
        }
    }
}
