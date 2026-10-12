using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodingGame.Art.MapPainter
{
    // One enemy entrance and the cells it walks, in order, toward the base.
    [System.Serializable]
    public sealed class MapRoute
    {
        public GridCell start;
        public List<GridCell> path = new List<GridCell>();
        public GridCell End => path.Count > 0 ? path[path.Count - 1] : start;
    }

    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ModularMapRoot : MonoBehaviour
    {
        public ModularMapPalette palette;
        [Min(1)] public int cellSize=1;
        [Min(.05f)] public float floorHeight=1;
        public bool useFloorColors = true;
        public Color upperColor = new Color(.68f,.72f,.78f,1);
        public Color lowerColor = new Color(.26f,.31f,.39f,1);
        public bool useRoadColors=true;
        public Color roadInnerColor=new Color(.95f,.64f,.29f,1);
        public Color roadBorderColor=new Color(.72f,.75f,.76f,1);
        [Range(0,1)] public float lightSoftness=.65f;
        [Range(0,1)] public float shadowStrength=.35f;
        [Range(0,1)] public float bodyGradient=.18f;
        [Range(0,1)] public float bevelHighlight=.12f;
        public bool blueprintBackground=true;
        public Color blueprintBlue=new Color(.035f,.24f,.58f,1);
        public Color blueprintLines=Color.white;
        [Min(.05f)] public float blueprintSpacing=1;
        [Range(.5f,3)] public float blueprintLineWidth=1;
        [Range(0,1)] public float blueprintMinorOpacity=.18f;
        [Range(0,1)] public float blueprintMajorOpacity=.38f;
        [Min(1)] public int blueprintMajorEvery=5;
        [HideInInspector] public List<GridCell> floorCells = new List<GridCell>();
        [HideInInspector] public List<GridCell> roadCells = new List<GridCell>();
        [HideInInspector] public Transform floorGroup;
        [HideInInspector] public Transform roadGroup;
        [HideInInspector] public bool hasBase;
        [HideInInspector] public GridCell baseCell;
        [HideInInspector] public List<MapRoute> routes = new List<MapRoute>();
        public bool IsComplete(MapRoute route) => hasBase && route.End.Equals(baseCell);
        // World positions at the start, each turn and the base, for a DefenseRoute-style waypoint list.
        public Vector3[] Waypoints(MapRoute route) => GridTopology.Corners(route.start, route.path).Select(CellPosition).ToArray();
        public Vector3 CellPosition(GridCell cell) => transform.position + new Vector3(cell.x*cellSize, 0, cell.z*cellSize);
        public GridCell WorldToCell(Vector3 world)
        {
            Vector3 p=world-transform.position;
            return new GridCell(Mathf.FloorToInt(p.x/cellSize+.5f),Mathf.FloorToInt(p.z/cellSize+.5f));
        }
        public bool IsTile(MapTileMarker tile) => tile.transform.parent==floorGroup || tile.transform.parent==roadGroup;
        public Vector3 TileLocalPosition(GridCell cell,bool road) => new Vector3(cell.x*cellSize,road&&palette?palette.roadLift:0,cell.z*cellSize);
        public Vector3 ModuleScale(bool road) => new Vector3(cellSize,road?cellSize:floorHeight,cellSize);
        private void OnEnable() { ApplyFloorColors(); }
#if UNITY_EDITOR
        private bool refreshColors;
        private void OnValidate() { cellSize=Mathf.Max(1,cellSize); floorHeight=Mathf.Max(.05f,floorHeight); blueprintSpacing=Mathf.Max(.05f,blueprintSpacing); blueprintMajorEvery=Mathf.Max(1,blueprintMajorEvery); refreshColors=true; }
        private void Update() { if(refreshColors) { refreshColors=false; ApplyFloorColors(); } }
#endif
        public void ApplyFloorColors()
        {
            ApplyLayout();
            if(floorGroup)foreach(var renderer in floorGroup.GetComponentsInChildren<MeshRenderer>(true))
            { SetFloorColors(renderer,useFloorColors,upperColor,lowerColor); ApplyStyle(renderer,transform.position.y,floorHeight); }
            if(roadGroup)foreach(var renderer in roadGroup.GetComponentsInChildren<MeshRenderer>(true))
            { SetFloorColors(renderer,useRoadColors,roadInnerColor,roadBorderColor); ApplyStyle(renderer,transform.position.y,cellSize); }
            foreach(var backdrop in GetComponentsInChildren<BlueprintBackdrop>(true))backdrop.Refresh();
        }
        public void ApplyLayout()
        {
            foreach(var tile in GetComponentsInChildren<MapTileMarker>(true))
            {
                if(!IsTile(tile))continue;
                tile.transform.localPosition=TileLocalPosition(tile.cell,tile.road);
                if(tile.transform.childCount>0)tile.transform.GetChild(0).localScale=ModuleScale(tile.road);
            }
        }
        private void ApplyStyle(MeshRenderer renderer,float top,float height)
        {
            for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
            {
                var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block,slot);
                block.SetFloat("_LightSoftness",lightSoftness); block.SetFloat("_ShadowStrength",shadowStrength);
                block.SetFloat("_GradientStrength",bodyGradient); block.SetFloat("_BevelHighlight",bevelHighlight);
                block.SetFloat("_MapTopY",top); block.SetFloat("_MapHeight",height); renderer.SetPropertyBlock(block,slot);
            }
        }
        private static void SetFloorColors(MeshRenderer renderer,bool enabled,Color upper,Color lower)
        {
            var filter=renderer.GetComponent<MeshFilter>();
            if(!filter || !filter.sharedMesh || filter.sharedMesh.subMeshCount<2)return;
            int slots=Mathf.Min(2,renderer.sharedMaterials.Length);
            for(int slot=0;slot<slots;slot++)
            {
                if(!enabled) { renderer.SetPropertyBlock(null,slot); continue; }
                var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block,slot);
                Color color=slot==0?upper:lower; color.a=1;
                block.SetColor("_BaseColor",color); block.SetColor("_Color",color);
                renderer.SetPropertyBlock(block,slot);
            }
        }
    }
}
