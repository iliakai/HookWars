using UnityEngine;
namespace HookWars.Chain
{
    // All dimensions in metres. Link lies in XZ, long axis +Z, centre at origin.
    public static class ChainGeometry
    {
        public const float Radius = .052f, StraightHalf = .05f, Wire = .018f, Pitch = .168f;
        public static Vector3 Point(float t)
        {
            float perimeter = 4 * StraightHalf + 2 * Mathf.PI * Radius;
            float s = Mathf.Repeat(t, 1f) * perimeter;
            float arc = Mathf.PI * Radius;
            if (s < arc) { float a = s / Radius; return new Vector3(Radius * Mathf.Cos(a), 0, StraightHalf + Radius * Mathf.Sin(a)); }
            s -= arc;
            if (s < 2 * StraightHalf) return new Vector3(-Radius, 0, StraightHalf-s);
            s -= 2 * StraightHalf;
            if (s < arc) { float a = Mathf.PI + s / Radius; return new Vector3(Radius * Mathf.Cos(a),0,-StraightHalf + Radius * Mathf.Sin(a)); }
            s -= arc; return new Vector3(Radius,0,-StraightHalf+s);
        }
        public static Mesh CreateMesh(int rings=48, int sides=10)
        {
            var vertices=new Vector3[(rings+1)*(sides+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new int[rings*sides*6];
            for(int i=0;i<=rings;i++)
            {
                float t=(float)i/rings;Vector3 c=Point(t), tangent=(Point(t+.0001f)-Point(t-.0001f)).normalized;
                Vector3 n=Vector3.Cross(tangent,Vector3.up).normalized;
                for(int j=0;j<=sides;j++)
                {
                    float a=2*Mathf.PI*j/sides;Vector3 normal=n*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a);int k=i*(sides+1)+j;
                    vertices[k]=c+normal*Wire;normals[k]=normal;uv[k]=new Vector2(t,(float)j/sides);
                }
            }
            int at=0;
            for(int i=0;i<rings;i++)for(int j=0;j<sides;j++)
            {
                int a=i*(sides+1)+j,b=a+sides+1;
                triangles[at++]=a;triangles[at++]=b;triangles[at++]=a+1;triangles[at++]=b;triangles[at++]=b+1;triangles[at++]=a+1;
            }
            var mesh=new Mesh {name="OvalChainLink"};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        // Closed hole: compound capsules follow the ring; no solid convex hull across its opening.
        public static Collider[] AddColliders(GameObject link,float scale,int segments=16)
        {
            var result=new Collider[segments];
            for(int i=0;i<segments;i++)
            {
                Vector3 a=Point((float)i/segments)*scale,b=Point((float)(i+1)/segments)*scale;
                var part=new GameObject("WireCollider_"+i);part.layer=link.layer;part.transform.SetParent(link.transform,false);
                part.transform.localPosition=(a+b)*.5f;part.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
                var c=part.AddComponent<CapsuleCollider>();c.direction=1;c.radius=Wire*scale;c.height=(b-a).magnitude+2*c.radius;c.contactOffset=.001f*scale;result[i]=c;
            }
            return result;
        }
    }
}
