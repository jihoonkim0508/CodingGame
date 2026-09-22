using UnityEngine;

namespace CodingGame.Defense
{
    [CreateAssetMenu(menuName = "CodingGame/Defense/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public EnemySpec stats = new EnemySpec();
        public DefenseActorView prefab;
    }
}
