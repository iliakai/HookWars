using UnityEngine;

// Logical XY dimensions are applied before movement, construction and respawn Awake.
[DefaultExecutionOrder(-1000)]
public class ArenaLayout : MonoBehaviour
{
    [SerializeField, Range(4,20)] private int bankDepth=MatchConfiguration.DefaultDepth;
    [SerializeField, Min(.1f)] private float mountainWidth=1;
    [SerializeField] private Transform player,enemy;
    [SerializeField] private BoxCollider2D leftBoundary,rightBoundary;
    public const float RiverWidth=4,FieldHeight=20,HeroRadius=.5f,RiverHalfWidth=RiverWidth*.5f;
    public static ArenaLayout Instance {get;private set;}
    private int appliedDepth;
    private float appliedMountainWidth;
    public static float Depth=>Instance!=null?Instance.appliedDepth:MatchConfiguration.DefaultDepth;
    public static float MountainWidth=>Instance!=null?Instance.appliedMountainWidth:1;
    public static float OuterEdge=>RiverHalfWidth+Depth;
    public static Rect BankBounds(bool right,float inset=HeroRadius)
    {
        float left=right?RiverHalfWidth:-OuterEdge;
        return new Rect(left+inset,-FieldHeight*.5f+inset,Depth-2*inset,FieldHeight-2*inset);
    }
    public static Vector3 ClampToBank(Vector3 position,bool right,float inset=HeroRadius)
    {
        Rect bounds=BankBounds(right,inset);position.x=Mathf.Clamp(position.x,bounds.xMin,bounds.xMax);position.y=Mathf.Clamp(position.y,bounds.yMin,bounds.yMax);return position;
    }
    public static bool IsInsideBank(Vector3 position,bool right,float inset=HeroRadius)
    {
        Rect b=BankBounds(right,inset);return position.x>=b.xMin&&position.x<=b.xMax&&position.y>=b.yMin&&position.y<=b.yMax;
    }
    private void Awake()
    {
        if(Instance!=null&&Instance!=this)throw new System.InvalidOperationException("Only one ArenaLayout per match");
        if(player==null||enemy==null||player==enemy||leftBoundary==null||rightBoundary==null)throw new System.InvalidOperationException("Assign heroes and logical mountain boundaries");
        appliedDepth=MatchConfiguration.ConsumeDepth(bankDepth);appliedMountainWidth=Mathf.Max(.1f,mountainWidth);Instance=this;
        ResizeBoundary(leftBoundary,-OuterEdge-appliedMountainWidth*.5f);ResizeBoundary(rightBoundary,OuterEdge+appliedMountainWidth*.5f);
        bool right=player.position.x>0;PlaceHero(player,right);PlaceHero(enemy,!right);Physics2D.SyncTransforms();
    }
    private void ResizeBoundary(BoxCollider2D boundary,float x)
    {
        boundary.transform.SetPositionAndRotation(new Vector3(x,0,0),Quaternion.identity);boundary.transform.localScale=Vector3.one;
        boundary.autoTiling=false;boundary.isTrigger=false;boundary.offset=Vector2.zero;boundary.size=new Vector2(appliedMountainWidth,FieldHeight);
    }
    private static void PlaceHero(Transform hero,bool right)
    {
        float distance=RiverHalfWidth+Mathf.Min(4,Depth*.5f);
        hero.position=ClampToBank(new Vector3(distance*(right?1:-1),0,0),right);
    }
    private void OnDestroy(){if(Instance==this)Instance=null;}
}
