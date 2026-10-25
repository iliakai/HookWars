using System.Collections.Generic;
using UnityEngine;

public static class WalkNavigation25D
{
    private static readonly Vector2Int[] steps={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    public static bool Segment(Vector2 from,Vector2 to,float radius)
    {
        int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.15f));
        for(int i=0;i<=count;i++)if(!RiverBridge.Walkable(Vector2.Lerp(from,to,i/(float)count),radius))return false;
        return true;
    }
    public static void Build(Transform mover,Vector3 goal,float radius,float arrival,List<Vector3> path,Collider2D target=null,bool includeActors=false)
    {
        path.Clear();Physics2D.SyncTransforms();
        Vector2 origin=new Vector2(-ArenaLayout.OuterEdge+radius+.03f,-ArenaLayout.FieldHeight*.5f+radius+.03f);
        const float cell=.5f;int width=Mathf.FloorToInt((2*ArenaLayout.OuterEdge-2*radius-.06f)/cell)+1;
        int height=Mathf.FloorToInt((ArenaLayout.FieldHeight-2*radius-.06f)/cell)+1;
        Vector3 Point(int i)=>new Vector3(origin.x+i%width*cell,origin.y+i/width*cell);
        int sx=Mathf.Clamp(Mathf.RoundToInt((mover.position.x-origin.x)/cell),0,width-1),sy=Mathf.Clamp(Mathf.RoundToInt((mover.position.y-origin.y)/cell),0,height-1);
        int start=sy*width+sx,found=-1;var previous=new int[width*height];System.Array.Fill(previous,-2);
        bool Arrived(Vector3 p)=>Vector2.Distance(p,target!=null?target.ClosestPoint(p):(Vector2)goal)<=(target!=null?arrival:Mathf.Max(arrival,.36f));
        if(Arrived(mover.position))return;
        var queue=new Queue<int>();
        var free=new sbyte[previous.Length];
        bool Free(int n)
        {
            if(free[n]!=0)return free[n]>0;
            if(!RiverBridge.Walkable(Point(n),radius)){free[n]=-1;return false;}
            foreach(var hit in Physics2D.OverlapCircleAll(Point(n),radius+.01f,WorldCollision.WalkingLayers))
                if(!hit.isTrigger&&hit.transform!=mover&&(includeActors||hit.GetComponent<Health>()==null)){free[n]=-1;return false;}
            free[n]=1;return true;
        }
        // Connect the real position to reachable nearby grid nodes. A snapped node
        // inside a building must not invalidate an otherwise usable route.
        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
        {
            int x=sx+dx,y=sy+dy;if(x<0||y<0||x>=width||y>=height)continue;
            int n=y*width+x;
            if(!Free(n)||!Segment(mover.position,Point(n),radius)||!WorldCollision.CanMove(mover,Point(n),radius))continue;
            previous[n]=-1;queue.Enqueue(n);
        }
        while(queue.Count>0)
        {
            int i=queue.Dequeue();var current=Point(i);
            if(Arrived(current)&&WorldCollision.ClearAttackLine(current,target!=null?target.ClosestPoint(current):(Vector2)goal,target)){found=i;break;}
            foreach(var step in steps)
            {
                int x=i%width+step.x,y=i/width+step.y;
                if(x<0||y<0||x>=width||y>=height)continue;int n=y*width+x;
                if(previous[n]!=-2||!Free(n)||!Segment(current,Point(n),radius))continue;
                bool blocked=false;
                foreach(var hit in Physics2D.CircleCastAll(current,radius,(Point(n)-current).normalized,cell,WorldCollision.WalkingLayers))
                    if(!hit.collider.isTrigger&&hit.transform!=mover&&(includeActors||hit.collider.GetComponent<Health>()==null)){blocked=true;break;}
                if(blocked)continue;previous[n]=i;queue.Enqueue(n);
            }
        }
        if(found<0)return;
        for(int i=found;i>=0;i=previous[i])path.Add(Point(i));path.Reverse();
        if(path.Count>0&&!WorldCollision.CanMove(mover,path[0],radius))path.Clear();
    }
}
