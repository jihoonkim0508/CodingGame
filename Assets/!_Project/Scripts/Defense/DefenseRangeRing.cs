using System;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed class DefenseRangeRing : MonoBehaviour
    {
        [SerializeField] LineRenderer line;
        readonly Vector3[] points = new Vector3[97];
        void Awake()
        {
            if (!line || !line.sharedMaterial) throw new InvalidOperationException(name + ": 사거리 LineRenderer/Material 참조 누락");
        }
        public void Show(Vector3 center, float radius, Color color)
        {
            line.enabled = true;
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / (points.Length - 1);
                points[i] = center + new Vector3(Mathf.Cos(a) * radius, .065f, Mathf.Sin(a) * radius);
            }
            line.useWorldSpace = true; line.positionCount = points.Length;
            line.SetPositions(points); line.startColor = line.endColor = color;
        }
        public void Hide() { line.enabled = false; }
        public void ShowSemicircle(Vector3 center, Vector3 forward, float radius, Color color)
        {
            forward.y = 0;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < points.Length - 1; i++)
            {
                float angle = -Mathf.PI * .5f + i * Mathf.PI / (points.Length - 2);
                points[i] = center + (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * radius;
            }
            points[points.Length - 1] = points[0];
            line.useWorldSpace = true; line.positionCount = points.Length;
            line.SetPositions(points); line.startColor = line.endColor = color; line.enabled = true;
        }
    }
}
