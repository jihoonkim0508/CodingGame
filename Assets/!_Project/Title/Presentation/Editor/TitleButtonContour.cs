using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CodingGame.TitleScreen.Editor
{
    static class TitleButtonContour
    {
        // Read the original PNG without changing its pixels or importer settings.
        public static Vector2[] Extract(Sprite sprite)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
                int w = texture.width, h = texture.height;
                var pixels = texture.GetPixels32();
                bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && pixels[y * w + x].a >= 180;
                var edges = new Dictionary<Vector2Int, Vector2Int>();
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    if (!Solid(x, y)) continue;
                    if (!Solid(x, y - 1)) edges[new Vector2Int(x, y)] = new Vector2Int(x + 1, y);
                    if (!Solid(x + 1, y)) edges[new Vector2Int(x + 1, y)] = new Vector2Int(x + 1, y + 1);
                    if (!Solid(x, y + 1)) edges[new Vector2Int(x + 1, y + 1)] = new Vector2Int(x, y + 1);
                    if (!Solid(x - 1, y)) edges[new Vector2Int(x, y + 1)] = new Vector2Int(x, y);
                }
                var best = new List<Vector2>();
                float bestArea = 0;
                while (edges.Count > 0)
                {
                    var iterator = edges.GetEnumerator(); iterator.MoveNext();
                    Vector2Int start = iterator.Current.Key, current = start;
                    var loop = new List<Vector2>();
                    while (edges.TryGetValue(current, out var next))
                    {
                        edges.Remove(current); loop.Add(current); current = next;
                        if (current == start) break;
                    }
                    if (current != start || loop.Count < 3) continue;
                    float area = 0;
                    for (int i = 0; i < loop.Count; i++)
                    {
                        var a = loop[i]; var b = loop[(i + 1) % loop.Count]; area += a.x * b.y - b.x * a.y;
                    }
                    if (area > bestArea) { best = loop; bestArea = area; }
                }
                if (best.Count < 3) throw new System.InvalidOperationException("No closed outer contour: " + sprite.name);
                // Split the closed contour in two before simplifying; retain source notches.
                int split = best.Count / 2;
                var simple = new List<Vector2>();
                Simplify(best, 0, split, simple);
                best.Add(best[0]); Simplify(best, split, best.Count - 1, simple);
                Rect rect = sprite.rect;
                float sx = (float)sprite.texture.width / w, sy = (float)sprite.texture.height / h;
                for (int i = 0; i < simple.Count; i++)
                    simple[i] = new Vector2((simple[i].x * sx - rect.x) / rect.width, (simple[i].y * sy - rect.y) / rect.height);
                return simple.ToArray();
            }
            finally { Object.DestroyImmediate(texture); }
        }
        static void Simplify(List<Vector2> path, int start, int end, List<Vector2> output)
        {
            Vector2 a = path[start], line = path[end] - a;
            float max = 0; int split = start;
            for (int i = start + 1; i < end; i++)
            {
                float t = Mathf.Clamp01(Vector2.Dot(path[i] - a, line) / Mathf.Max(1, line.sqrMagnitude));
                float distance = (path[i] - a - line * t).sqrMagnitude;
                if (distance > max) { max = distance; split = i; }
            }
            if (max > 16) { Simplify(path, start, split, output); Simplify(path, split, end, output); }
            else output.Add(a);
        }
    }
}
