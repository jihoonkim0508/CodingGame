using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter
{
    // The editor and game use the same deterministic sampler. All changes are restored.
    public sealed class MapBuildPlayback
    {
        private sealed class Tile
        {
            public MapTileMarker marker; public Vector3 position,scale; public float order;
            public RendererState[] renderers;
        }
        private sealed class RendererState
        {
            public Renderer renderer; public bool enabled; public MaterialPropertyBlock all;
            public MaterialPropertyBlock[] slots;
            private MaterialPropertyBlock[] working;
            public RendererState(Renderer r)
            {
                renderer=r; enabled=r.enabled; all=new MaterialPropertyBlock(); r.GetPropertyBlock(all);
                slots=new MaterialPropertyBlock[r.sharedMaterials.Length];
                working=new MaterialPropertyBlock[slots.Length];
                for(int i=0;i<slots.Length;i++)
                {slots[i]=new MaterialPropertyBlock();r.GetPropertyBlock(slots[i],i);working[i]=new MaterialPropertyBlock();r.GetPropertyBlock(working[i],i);}
            }
            public void Apply(bool visible,float glow)
            {
                if(!renderer)return;if(renderer.enabled!=(enabled&&visible))renderer.enabled=enabled&&visible;
                for(int i=0;i<slots.Length;i++)
                {
                    var block=working[i];
                    block.SetFloat("_BuildGlow",glow); renderer.SetPropertyBlock(block,i);
                }
            }
            public void Restore()
            {
                if(!renderer)return; renderer.enabled=enabled;renderer.SetPropertyBlock(all.isEmpty?null:all);
                for(int i=0;i<slots.Length;i++)renderer.SetPropertyBlock(slots[i].isEmpty?null:slots[i],i);
            }
        }
        private readonly ModularMapRoot map;
        private readonly MapBuildSequence settings;
        private readonly List<Tile> tiles=new List<Tile>();
        private readonly List<RendererState> decorations=new List<RendererState>();
        private readonly List<LineRenderer> outlines=new List<LineRenderer>();
        private readonly List<Vector3> outlineEnds=new List<Vector3>();
        private GameObject temporary;
        public MapBuildPlayback(ModularMapRoot root,MapBuildSequence sequence)
        {
            map=root;settings=sequence;
            if(!map)throw new ArgumentException("Map root is required.");
            var roadOrder=RoadOrder(new HashSet<GridCell>(map.roadCells));
            var markers=map.GetComponentsInChildren<MapTileMarker>(true);
            int minX=markers.Length==0?0:markers.Min(m=>m.cell.x),maxX=markers.Length==0?0:markers.Max(m=>m.cell.x);
            var tileRenderers=new HashSet<Renderer>();
            foreach(var marker in markers)
            {
                if(marker.transform.parent!=map.floorGroup&&marker.transform.parent!=map.roadGroup)continue;
                var states=marker.GetComponentsInChildren<Renderer>(true).Select(r=>new RendererState(r)).ToArray();
                foreach(var state in states)tileRenderers.Add(state.renderer);
                float order=marker.road?(roadOrder.TryGetValue(marker.cell,out float index)?index:0):Mathf.InverseLerp(minX,maxX,marker.cell.x);
                tiles.Add(new Tile{marker=marker,position=marker.transform.localPosition,scale=marker.transform.localScale,order=order,renderers=states});
            }
            foreach(var renderer in map.GetComponentsInChildren<Renderer>(true))
                if(!tileRenderers.Contains(renderer)&&!renderer.GetComponent<BlueprintBackdrop>())decorations.Add(new RendererState(renderer));
            BuildOutline();
        }
        public static Dictionary<GridCell,float> RoadOrder(HashSet<GridCell> cells)
        {
            var result=new Dictionary<GridCell,float>();var remaining=new HashSet<GridCell>(cells);int index=0;
            while(remaining.Count>0)
            {
                // Prefer the leftmost endpoint. Branches and disconnected paths use breadth-first order.
                var endpoints=remaining.Where(c=>GridTopology.Directions.Count(d=>remaining.Contains(c+d))<=1).ToArray();
                var first=(endpoints.Length>0?endpoints:remaining.ToArray()).OrderBy(c=>c.x).ThenBy(c=>c.z).First();
                var queue=new Queue<GridCell>();queue.Enqueue(first);remaining.Remove(first);
                while(queue.Count>0)
                {
                    var cell=queue.Dequeue();result[cell]=index++;
                    foreach(var d in GridTopology.Directions)if(remaining.Remove(cell+d))queue.Enqueue(cell+d);
                }
            }
            foreach(var cell in cells)result[cell]/=Mathf.Max(1,cells.Count-1);
            return result;
        }
        private void BuildOutline()
        {
            if(!settings.outlineMaterial)return;
            temporary=new GameObject("Map Build Preview Effects"){hideFlags=HideFlags.HideAndDontSave};
            SceneManager.MoveGameObjectToScene(temporary,map.gameObject.scene);
            var occupied=new HashSet<GridCell>(map.floorCells);float half=map.cellSize*.5f;
            foreach(var cell in occupied.OrderBy(c=>c.x).ThenBy(c=>c.z))
            {
                Vector3 center=map.CellPosition(cell)+Vector3.up*.012f;
                for(int side=0;side<4;side++)
                {
                    var d=GridTopology.Directions[side];if(occupied.Contains(cell+d))continue;
                    Vector3 normal=new Vector3(d.x,0,d.z)*half;
                    Vector3 along=new Vector3(d.z,0,-d.x)*half;
                    var go=new GameObject("Outline"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(temporary.transform,false);
                    var line=go.AddComponent<LineRenderer>();line.sharedMaterial=settings.outlineMaterial;
                    line.useWorldSpace=true;line.positionCount=2;line.SetPosition(0,center+normal-along);line.SetPosition(1,center+normal-along);
                    line.widthMultiplier=map.cellSize*.014f;line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                    outlines.Add(line);outlineEnds.Add(center+normal+along);
                }
            }
        }
        public void Sample(float time)
        {
            time=Mathf.Clamp01(time);
            for(int i=0;i<outlines.Count;i++)
            {
                if(!outlines[i])continue;
                float draw=Mathf.Clamp01((time-i/(float)Mathf.Max(1,outlines.Count)*.12f)/.1f);
                var line=outlines[i];line.SetPosition(1,Vector3.Lerp(line.GetPosition(0),outlineEnds[i],draw));
                Color color=settings.outlineColor;color.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,.92f,time));
                line.startColor=line.endColor=color;line.enabled=time<.92f;
            }
            foreach(var tile in tiles)
            {
                if(!tile.marker)continue;
                float start=tile.marker.road?.58f+tile.order*.2f:.12f+tile.order*.27f;
                float span=tile.marker.road?.2f:.25f;
                float progress=Mathf.Clamp01((time-start)/span),smooth=Mathf.SmoothStep(0,1,progress);
                float jitter=settings.glitch*(1-smooth)*Mathf.Sin(progress*55+tile.marker.cell.x*3+tile.marker.cell.z*7)*.035f*map.cellSize;
                float lift=tile.marker.road?Mathf.Min(settings.riseHeight,map.cellSize*.02f):settings.riseHeight;
                tile.marker.transform.localPosition=tile.position+new Vector3(jitter,-lift*(1-smooth),0);
                tile.marker.transform.localScale=tile.scale*Mathf.Lerp(.85f,1,smooth);
                foreach(var renderer in tile.renderers)renderer.Apply(progress>0,(1-smooth)*.7f);
            }
            foreach(var renderer in decorations)renderer.Apply(time>.86f,(1-Mathf.InverseLerp(.86f,1,time))*.5f);
        }
        public void Restore()
        {
            foreach(var tile in tiles)
            {
                if(tile.marker){tile.marker.transform.localPosition=tile.position;tile.marker.transform.localScale=tile.scale;}
                foreach(var renderer in tile.renderers)renderer.Restore();
            }
            foreach(var renderer in decorations)renderer.Restore();
            if(temporary)
            {
                if(Application.isPlaying){temporary.SetActive(false);UnityEngine.Object.Destroy(temporary);}
                else UnityEngine.Object.DestroyImmediate(temporary);
            }
            temporary=null;
        }
    }
}
