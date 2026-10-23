using UnityEngine;

// Terrain, not a damageable/buildable entity. Physics2D owns gameplay occupancy.
public sealed class PermanentRock : MonoBehaviour
{
    [SerializeField,Min(.05f)] private float collisionHeight=.6f;
    public float CollisionHeight=>collisionHeight;
}
