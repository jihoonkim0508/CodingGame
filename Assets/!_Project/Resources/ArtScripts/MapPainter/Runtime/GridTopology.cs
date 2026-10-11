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
