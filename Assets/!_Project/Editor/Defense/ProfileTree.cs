using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace CodingGame.Defense.Editor
{
    // The main-thread call tree of one frame in the Profiler's history, for the frame probes: samples of 4 ms or more, longest
    // first, then the sample names with the most time of their own across the frame.
    static class ProfileTree
    {
        public static void Append(StringBuilder s, int frame, float ms, string label)
        {
            using var h = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                HierarchyFrameDataView.columnTotalTime, false);
            s.AppendLine($"=== 프로파일 프레임 {frame} · {ms:0.0}ms{label}");
            int lines = 0;
            Dump(s, h, h.GetRootItemID(), 0, ref lines);
            var self = new Dictionary<string, (float ms, int calls)>();
            Collect(h, h.GetRootItemID(), self);
            s.AppendLine("  자기 시간 상위: " + string.Join(" · ", self.OrderByDescending(x => x.Value.ms).Take(12)
                .Select(x => $"{x.Key} {x.Value.ms:0.0}ms x{x.Value.calls}")));
        }

        static void Dump(StringBuilder s, HierarchyFrameDataView h, int id, int depth, ref int lines)
        {
            var kids = new List<int>();
            h.GetItemChildren(id, kids);
            foreach (var k in kids)
            {
                float total = h.GetItemColumnDataAsSingle(k, HierarchyFrameDataView.columnTotalTime);
                if (total < 4 || lines >= 90) break; // children come sorted by total time, longest first
                s.AppendLine($"  {new string(' ', depth * 2)}{h.GetItemName(k)}  {total:0.0}ms (자기 {h.GetItemColumnDataAsSingle(k, HierarchyFrameDataView.columnSelfTime):0.0}, " +
                    $"x{h.GetItemColumnDataAsSingle(k, HierarchyFrameDataView.columnCalls):0})");
                lines++;
                if (depth < 40) Dump(s, h, k, depth + 1, ref lines);
            }
        }

        static void Collect(HierarchyFrameDataView h, int id, Dictionary<string, (float ms, int calls)> self)
        {
            var kids = new List<int>();
            h.GetItemChildren(id, kids);
            foreach (var k in kids)
            {
                var n = h.GetItemName(k);
                self.TryGetValue(n, out var v);
                self[n] = (v.ms + h.GetItemColumnDataAsSingle(k, HierarchyFrameDataView.columnSelfTime),
                    v.calls + (int)h.GetItemColumnDataAsSingle(k, HierarchyFrameDataView.columnCalls));
                Collect(h, k, self);
            }
        }
    }
}
