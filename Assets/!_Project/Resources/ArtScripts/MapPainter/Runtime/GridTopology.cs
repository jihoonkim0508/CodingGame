using System;
using System.Collections.Generic;

namespace CodingGame.Art.MapPainter
{
    [Serializable]
    public struct GridCell : IEquatable<GridCell>
    {
        public int x;
        public int z;
        public GridCell(int x, int z) { this.x = x; this.z = z; }
        public bool Equals(GridCell other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => unchecked(x * 397 ^ z);
        public override string ToString() => $"({x}, {z})";
        public static GridCell operator +(GridCell a, GridCell b) => new GridCell(a.x + b.x, a.z + b.z);
    }

    public readonly struct FloorShape : IEquatable<FloorShape>
    {
        public readonly int edges;
        public readonly int corners;
        public FloorShape(int edges, int corners) { this.edges = edges & 15; this.corners = corners & 15; }
        public bool Equals(FloorShape other) => edges == other.edges && corners == other.corners;
        public override bool Equals(object obj) => obj is FloorShape other && Equals(other);
        public override int GetHashCode() => edges | (corners << 4);
        public FloorShape Rotate(int turns) => new FloorShape(GridTopology.RotateMask(edges, turns), GridTopology.RotateMask(corners, turns));
    }

    // Clockwise: north (+Z), east (+X), south (-Z), west (-X).
    // Corner bits: NE, SE, SW, NW. No Unity dependencies: exhaustively testable.
    public static class GridTopology
    {
        public static readonly GridCell[] Directions = { new GridCell(0, 1), new GridCell(1, 0), new GridCell(0, -1), new GridCell(-1, 0) };
        public static readonly GridCell[] Diagonals = { new GridCell(1, 1), new GridCell(1, -1), new GridCell(-1, -1), new GridCell(-1, 1) };
        public static int RotateMask(int mask, int turns)
        {
            turns = ((turns % 4) + 4) % 4;
            return ((mask << turns) | (mask >> (4 - turns))) & 15;
        }
        public static GridCell RotateCell(GridCell cell, int turns)
        {
            turns = ((turns % 4) + 4) % 4;
            while (turns-- > 0) cell = new GridCell(cell.z, -cell.x);
            return cell;
        }
        public static int Connections(HashSet<GridCell> cells, GridCell cell)
        {
            int mask = 0;
            for (int i = 0; i < 4; i++) if (cells.Contains(cell + Directions[i])) mask |= 1 << i;
            return mask;
        }
        // Bits 0-3 add the orthogonal neighbours, bits 4-7 the diagonals, around (0,0).
        public static HashSet<GridCell> Neighbourhood(int mask)
        {
            var cells = new HashSet<GridCell> { new GridCell(0, 0) };
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0) cells.Add(Directions[i]);
                if ((mask & (1 << (i + 4))) != 0) cells.Add(Diagonals[i]);
            }
            return cells;
        }
        public static FloorShape Floor(HashSet<GridCell> cells, GridCell cell)
        {
            int connections = Connections(cells, cell), corners = 0;
            for (int i = 0; i < 4; i++)
            {
                int adjacent = (1 << i) | (1 << ((i + 1) % 4));
                if ((connections & adjacent) == adjacent && !cells.Contains(cell + Diagonals[i])) corners |= 1 << i;
            }
            return new FloorShape(connections ^ 15, corners);
        }
        // Shortest 4-connected walk from `from` to `to` over walkable cells; excludes `from`, ends at `to`. Null when unreachable.
        public static List<GridCell> ShortestPath(HashSet<GridCell> walkable, GridCell from, GridCell to)
        {
            var previous = new Dictionary<GridCell, GridCell> { [from] = from };
            var queue = new Queue<GridCell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell.Equals(to))
                {
                    var path = new List<GridCell>();
                    for (var c = to; !c.Equals(from); c = previous[c]) path.Add(c);
                    path.Reverse();
                    return path;
                }
                foreach (var d in Directions)
                {
                    var next = cell + d;
                    if (walkable.Contains(next) && !previous.ContainsKey(next)) { previous[next] = cell; queue.Enqueue(next); }
                }
            }
            return null;
        }
        // The start, every cell where the walk turns, and the last cell: the waypoints a mover needs.
        public static List<GridCell> Corners(GridCell start, IReadOnlyList<GridCell> path)
        {
            var corners = new List<GridCell> { start };
            for (int i = 0; i < path.Count; i++)
            {
                var before = i == 0 ? start : path[i - 1];
                if (i == path.Count - 1 || path[i].x - before.x != path[i + 1].x - path[i].x || path[i].z - before.z != path[i + 1].z - path[i].z)
                    corners.Add(path[i]);
            }
            return corners;
        }
        public static IEnumerable<GridCell> Stroke(GridCell from, GridCell to)
        {
            int dx = Math.Abs(to.x - from.x), dz = Math.Abs(to.z - from.z);
            int sx = Math.Sign(to.x - from.x), sz = Math.Sign(to.z - from.z), ix = 0, iz = 0;
            yield return from;
            while (ix < dx || iz < dz)
            {
                if (ix < dx && (iz == dz || (1L + 2L * ix) * dz <= (1L + 2L * iz) * dx)) { from.x += sx; ix++; }
                else { from.z += sz; iz++; }
                yield return from;
            }
        }
    }
}
