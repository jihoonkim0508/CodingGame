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
        [Tooltip("군단 트리: 대장 아래 마디의 모델(비우면 prefab)")] public DefenseActorView nodePrefab;
        public Texture icon;
    }
}
