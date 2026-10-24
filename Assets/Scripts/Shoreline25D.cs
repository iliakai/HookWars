using UnityEngine;
using UnityEngine.Rendering;

// A narrow graduated contact shadow creates a readable drop into the water.
public sealed class Shoreline25D : MonoBehaviour
{
    private readonly Material[] materials=new Material[3];
    private Mesh mesh;
    public void Bind(Material overlay)
    {
        mesh=new Mesh{name="Shore strip"};mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
        mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
        float[] widths={.06f,.08f,.12f},alpha={.35f,.20f,.08f};
        for(int i=0;i<3;i++){materials[i]=new Material(overlay);materials[i].SetColor("_BaseColor",new Color(.10f,.13f,.07f,alpha[i]));}
        foreach(int side in new[]{-1,1})
        {
            float offset=0;
            for(int i=0;i<3;i++)
            {
                var strip=new GameObject("Shore contact shadow");strip.transform.SetParent(transform,false);
                strip.transform.position=new Vector3(side*(ArenaLayout.RiverHalfWidth+offset+widths[i]/2),.016f,0);
                strip.transform.localScale=new Vector3(widths[i],1,ArenaLayout.FieldHeight);
                strip.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=strip.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[i];renderer.shadowCastingMode=ShadowCastingMode.Off;
                offset+=widths[i];
            }
        }
    }
    private void OnDestroy(){if(mesh!=null)Destroy(mesh);foreach(var m in materials)if(m!=null)Destroy(m);}
}
