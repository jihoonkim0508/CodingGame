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
        [Tooltip("하단 로봇 카드의 얼굴. Tools/Develop/로봇 아이콘 만들기가 prefab을 찍어 채운다. 비우면 글자만.")]
        public Sprite icon;
    }
}
