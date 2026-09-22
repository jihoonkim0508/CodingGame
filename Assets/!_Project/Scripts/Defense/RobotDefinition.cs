using UnityEngine;

namespace CodingGame.Defense
{
    [CreateAssetMenu(menuName = "CodingGame/Defense/Robot")]
    public sealed class RobotDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public Color color = Color.white;
        public RobotSpec stats = new RobotSpec();
        public DefenseActorView prefab;
    }
}
