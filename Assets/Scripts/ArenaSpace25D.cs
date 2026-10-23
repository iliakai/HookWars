using UnityEngine;

// The only mapping between gameplay XY and presentation XZ. One unit is one metre.
public static class ArenaSpace25D
{
    public static Vector3 ToWorld(Vector2 point, float height = 0f) => new Vector3(point.x, height, point.y);
    public static Vector2 ToLogical(Vector3 point) => new Vector2(point.x, point.z);
    public static Quaternion Facing(Vector2 direction) => direction.sqrMagnitude < 0.000001f
        ? Quaternion.identity : Quaternion.LookRotation(ToWorld(direction), Vector3.up);
}
