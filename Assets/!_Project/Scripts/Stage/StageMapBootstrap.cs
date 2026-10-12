using System.Linq;
using UnityEngine;

namespace CodingGame.StageMap
{
    // GameFlow calls this when the Stage scene loads: every renderer named "Stage" becomes a node, numbered left to right.
    public static class StageMapBootstrap
    {
        public static void Install()
        {
            Camera camera = Camera.main;
            if (!camera) { Debug.LogError("[StageMap] Main Camera가 없습니다."); return; }

            StageNode[] nodes = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.gameObject.name == "Stage")
                .OrderBy(renderer => renderer.transform.position.x)
                .Select(renderer => renderer.gameObject.AddComponent<StageNode>())
                .ToArray();
            if (nodes.Length == 0) { Debug.LogError("[StageMap] 이름이 'Stage'인 오브젝트가 없습니다."); return; }

            var palette = Resources.Load<StageMaterialPalette>("StageMaterialPalette");
            for (int i = 0; i < nodes.Length; i++) nodes[i].Configure(i + 1, palette);
            new GameObject("StageMapSystems").AddComponent<StageMapController>().Configure(nodes, camera);
        }
    }
}
