using UnityEngine;

public static class WorldCollision
{
    private static int WalkingMask => LayerMask.GetMask("Buildings", "Mountains", "HookTarget", "PlayerTarget", "Animals");
    public static bool CanMove(Transform mover, Vector3 destination, float radius)
    {
        Vector2 direction = destination - mover.position;
        float distance = direction.magnitude;
        if (distance < 0.000001f) return true;
        Physics2D.SyncTransforms();
        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(mover.position, radius, direction / distance, distance, WalkingMask))
        {
            if (hit.collider.isTrigger || hit.transform == mover || hit.transform.IsChildOf(mover)) continue;
            // A hook may land a hero in an existing overlap. Permit escape, never
            // movement deeper into the obstacle; ordinary incoming sweeps remain blocked.
            Vector2 away = (Vector2)mover.position - (Vector2)hit.collider.bounds.center;
            if (hit.distance <= 0.00001f && Vector2.Dot(direction, away) >= 0 &&
                ((Vector2)destination - (Vector2)hit.collider.bounds.center).sqrMagnitude > away.sqrMagnitude) continue;
            return false;
        }
        return true;
    }
    public static Vector3 Slide(Transform mover, Vector3 destination, float radius)
    {
        if (CanMove(mover, destination, radius)) return destination;
        Vector3 result = mover.position;
        Vector3 x = new Vector3(destination.x, result.y, result.z);
        if (CanMove(mover, x, radius)) result = x;
        Vector3 before = mover.position;
        mover.position = result;
        Vector3 y = new Vector3(result.x, destination.y, result.z);
        if (CanMove(mover, y, radius)) result = y;
        mover.position = before;
        return result;
    }
    public static bool FreeCircle(Vector3 position, float radius)
    {
        Physics2D.SyncTransforms();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, radius))
            if (!hit.isTrigger) return false;
        return true;
    }
}
