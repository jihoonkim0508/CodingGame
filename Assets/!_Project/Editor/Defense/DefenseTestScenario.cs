using System;
using CodingGame.BlockCoding;
using UnityEngine;
using Newtonsoft.Json;

namespace CodingGame.Defense.Editor
{
    [Serializable]
    public sealed class TestRobotPlacement
    {
        public int definitionIndex;
        public Vector3 position;
        public string functionName = "robot";
        public int level = 1;
        public float health = -1;
        [TextArea(3,12)] public string blocksJson = "[]";
        public CodeBlock[] Blocks { get => JsonConvert.DeserializeObject<CodeBlock[]>(blocksJson) ?? Array.Empty<CodeBlock>(); set => blocksJson=JsonConvert.SerializeObject(value); }
    }
    [CreateAssetMenu(menuName = "CodingGame/Tests/Scenario")]
    public sealed class DefenseTestScenario : ScriptableObject
    {
        public int stage = 1, wave = 1, coins = 100;
        [UnityEngine.Serialization.FormerlySerializedAs("accumulateSupply")] public bool previousStageRewards = true;
        public float timeoutSeconds = 180;
        public RobotStock[] robots = Array.Empty<RobotStock>();
        public BlockStock[] blocks = Array.Empty<BlockStock>();
        public TestRobotPlacement[] placements = Array.Empty<TestRobotPlacement>();
        public DefenseTestOptions options = new DefenseTestOptions();
    }
}
