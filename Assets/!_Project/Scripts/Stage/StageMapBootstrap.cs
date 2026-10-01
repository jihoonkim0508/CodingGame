using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.StageMap
{
    public static class StageMapBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, "Stage", System.StringComparison.OrdinalIgnoreCase)) return;
            if (Object.FindAnyObjectByType<StageMapController>()) return;

            Camera camera = Camera.main;
            if (!camera) { Debug.LogError("[StageMap] Main Camera가 없습니다."); return; }

            StageNode[] nodes = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.gameObject.name == "Stage")
                .OrderBy(renderer => renderer.transform.position.x)
                .Select(renderer => renderer.gameObject.AddComponent<StageNode>())
                .ToArray();
            if (nodes.Length == 0) { Debug.LogError("[StageMap] 이름이 'Stage'인 오브젝트가 없습니다."); return; }

            StageDefinition[] definitions = CreateDefaultDefinitions(nodes.Length);
            StageMaterialPalette palette = Resources.Load<StageMaterialPalette>("StageMaterialPalette");
            for (int i = 0; i < nodes.Length; i++) nodes[i].Configure(i, definitions[i], palette);

            GameObject systems = new GameObject("StageMapSystems");
            systems.AddComponent<StageMapController>().Configure(definitions, nodes, camera);
        }

        static StageDefinition[] CreateDefaultDefinitions(int count)
        {
            string[] titles = { "개발자의 작업실", "데이터 분기 구역", "자동화 생산 공장", "시스템 코어" };
            string[] descriptions =
            {
                "코딩의 시작, 기본 명령어를 배우는 공간",
                "다양한 특성을 가진 유닛 코드를 분석하고 조건에 맞게 대응하는 공간",
                "반복해서 생성되는 오류 코드를 효율적으로 처리하는 공간",
                "개별 도구의 핵심 시스템을 지켜내는 최종 관문"
            };
            string[] scenes = { "IDE", "CodeBlock", "Defense", "SampleScene" };
            Color[] colors =
            {
                new Color(0.25f, 0.85f, 0.38f), new Color(0.15f, 0.62f, 1f),
                new Color(1f, 0.48f, 0.08f), new Color(0.7f, 0.25f, 1f)
            };
            StageDefinition[] result = new StageDefinition[count];
            for (int i = 0; i < count; i++)
            {
                int preset = Mathf.Min(i, titles.Length - 1);
                result[i] = new StageDefinition
                {
                    Id = $"stage-{i + 1}", Number = i + 1, Title = titles[preset],
                    Description = descriptions[preset], SceneName = scenes[preset], ClearedColor = colors[preset],
                    Preview = Resources.Load<Texture2D>($"StagePreviews/Stage{i + 1}")
                };
            }
            return result;
        }
    }
}
