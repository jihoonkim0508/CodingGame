using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.Art.MapPainter;

static class TopologyCheck
{
    static void Assert(bool value,string message) { if(!value)throw new Exception(message); }
    static void Main()
    {
        var shapes=new HashSet<FloorShape>();
        var canonical=new HashSet<FloorShape>();
        for(int mask=0;mask<256;mask++)
        {
            var cells=new HashSet<GridCell> {new GridCell(0,0)};
            for(int i=0;i<4;i++)
            { if((mask&(1<<i))!=0)cells.Add(GridTopology.Directions[i]); if((mask&(1<<(i+4)))!=0)cells.Add(GridTopology.Diagonals[i]); }
            var shape=GridTopology.Floor(cells,new GridCell(0,0)); shapes.Add(shape);
            for(int i=0;i<4;i++)
            {
                var rotated=new HashSet<GridCell>(cells.Select(c=>GridTopology.RotateCell(c,i)));
                Assert(GridTopology.Floor(rotated,new GridCell(0,0)).Equals(shape.Rotate(i)),"Rotation mismatch");
                Assert(shape.Rotate(i).Rotate(4-i).Equals(shape),"Roundtrip rotation");
                int adjacent=(1<<i)|(1<<((i+1)%4));
                Assert((shape.corners&(1<<i))==0||(shape.edges&adjacent)==0,"Chamfer intrudes into exposed edge");
            }
            var key=Enumerable.Range(0,4).Select(i=>shape.Rotate(i)).OrderBy(s=>s.GetHashCode()).First(); canonical.Add(key);
        }
        int strokes=0;
        for(int x=-10;x<=10;x++)for(int z=-10;z<=10;z++)
        {
            var start=new GridCell(-3,4);var end=new GridCell(x,z); var line=GridTopology.Stroke(start,end).ToArray();
            Assert(line[0].Equals(start)&&line.Last().Equals(end),"Endpoints");
            Assert(line.Length==Math.Abs(x-start.x)+Math.Abs(z-start.z)+1,"Length");
            Assert(line.Distinct().Count()==line.Length,"Repeated cell");
            for(int i=1;i<line.Length;i++)Assert(Math.Abs(line[i].x-line[i-1].x)+Math.Abs(line[i].z-line[i-1].z)==1,"Gap");strokes++;
        }
        for(int mask=0;mask<16;mask++)for(int t=0;t<4;t++)Assert(GridTopology.RotateMask(GridTopology.RotateMask(mask,t),4-t)==mask,"Road rotation");
        Console.WriteLine($"PASS: 256 masks / 1024 floor rotations / 64 road rotations / {strokes} strokes; {shapes.Count} signatures, {canonical.Count} rotational classes.");
    }
}
