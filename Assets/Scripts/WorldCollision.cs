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
        PushAnimals(mover,destination,radius);
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
    private static void PushAnimals(Transform mover,Vector3 destination,float radius)
    {
        var hero=mover.GetComponent<Health>();if(hero==null||hero.IsDead)return;
        Vector2 travel=destination-mover.position;if(travel.sqrMagnitude<.0000001f)return;
        Physics2D.SyncTransforms();
        foreach(var hit in Physics2D.CircleCastAll(mover.position,radius,travel.normalized,travel.magnitude,LayerMask.GetMask("Animals")))
        {
            var animal=hit.collider.GetComponent<Animal>();if(animal==null||animal.IsDead)continue;
            Vector2 away=(Vector2)animal.transform.position-(Vector2)destination;
            float overlap=radius+animal.Radius+.01f-away.magnitude;if(overlap<=0)continue;
            Vector2 push=away.sqrMagnitude>.0001f?away.normalized:travel.normalized;
            float strength=Mathf.Clamp(hero.CurrentKg/Mathf.Max(1,animal.MassKg),.25f,1);
            Vector3 target=animal.transform.position+(Vector3)(push*overlap*strength);
            if(animal.TryPush(target))continue;
            Vector2 side=new Vector2(-push.y,push.x);
            for(int sign=-1;sign<=1;sign+=2)
                if(animal.TryPush(animal.transform.position+(Vector3)((push+side*sign).normalized*(overlap+.12f))))break;
        }
    }
    public static bool FreeCircle(Vector3 position, float radius)
    {
        Physics2D.SyncTransforms();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, radius))
            if (!hit.isTrigger) return false;
        return true;
    }
    public static bool FreeBox(Vector2 center,Vector2 size)
    {
        Physics2D.SyncTransforms();
        foreach(var hit in Physics2D.OverlapBoxAll(center,size-Vector2.one*.0002f,0))
        {
            if(hit.isTrigger)continue;Bounds b=hit.bounds;
            float x=Mathf.Min(center.x+size.x*.5f,b.max.x)-Mathf.Max(center.x-size.x*.5f,b.min.x);
            float y=Mathf.Min(center.y+size.y*.5f,b.max.y)-Mathf.Max(center.y-size.y*.5f,b.min.y);
            if(x>.00001f&&y>.00001f)return false;
        }
        return true;
    }
}
