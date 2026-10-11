using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.Art.MapPainter
{
    [Serializable]
    public sealed class FloorRule
    {
        public string label;
        public GameObject prefab;
        [Range(0, 15)] public int exposedEdges;
        [Range(0, 15)] public int cutCorners;
        public bool generatedComplement;
    }
    [Serializable]
    public sealed class RoadRule
    {
        public string label;
        public GameObject prefab;
        [Range(0, 15)] public int connections;
        [Range(0, 3)] public int rotationOffset;
    }
    public sealed class ModularMapPalette : ScriptableObject
    {
        public List<FloorRule> floors = new List<FloorRule>();
        public List<RoadRule> roads = new List<RoadRule>();
        [HideInInspector] public int floorColorVersion;
        [HideInInspector] public int roadModelVersion;
        [Min(0)] public float roadLift;
        public bool TryFloor(FloorShape shape, out GameObject prefab, out int turns)
        {
            foreach (var rule in floors)
                for (int i = 0; i < 4; i++)
                    if (rule.prefab && new FloorShape(rule.exposedEdges, rule.cutCorners).Rotate(i).Equals(shape))
                    { prefab = rule.prefab; turns = i; return true; }
            prefab = null; turns = 0; return false;
        }
        public bool TryRoad(int connections, out GameObject prefab, out int turns)
        {
            foreach (var rule in roads)
                for (int i = 0; i < 4; i++)
                    if (rule.prefab && GridTopology.RotateMask(rule.connections, i) == connections)
                    { prefab = rule.prefab; turns = (i + rule.rotationOffset) % 4; return true; }
            prefab = null; turns = 0; return false;
        }
    }
}
