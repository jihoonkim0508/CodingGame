using UnityEngine;

namespace CodingGame.BlockCoding
{
    public sealed class Enemy : MonoBehaviour
    {
        public float get_distance(float x, float y)
        {
            Vector3 position = transform.position;
            return Vector2.Distance(new Vector2(position.x, position.y), new Vector2(x, y));
        }
    }
}
