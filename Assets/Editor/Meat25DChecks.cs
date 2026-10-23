using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Meat25DChecks
{
    public const string Key="HookWars.MeatChecks";
    static int phase,count;
    static double ready,deadline;
    static float due;
    static Carcass[] drops;
    static float[] bottoms,masses;
    static Quaternion[] rotations;
    static int[] vertexIndices;
    static Vector3[] vertices;
    static Health hero;
    static Meat25DChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    [MenuItem("HookWars/25D/Meat/Check spinning drops Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();Meat25DSetup.Configure();string before=Signature();Meat25DSetup.Configure();
        if(before!=Signature())throw new InvalidOperationException("Repeated meat setup changed asset bytes");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static string Signature()=>File.ReadAllText(Arena25DSetup.ScenePath)+string.Join("",Directory.GetFiles(Meat25DSetup.Output).OrderBy(p=>p).Select(p=>Convert.ToBase64String(File.ReadAllBytes(p))));
    static void Subscribe(){phase=count=0;ready=EditorApplication.timeSinceStartup+3;deadline=ready+90;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);count++;Debug.Log("MEAT_CHECK: "+message);}
    static Bounds BoundsOf(Transform view)
    {
        Bounds bounds=default;bool first=true;
        foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=Animals25DSetup.BakeWorld(skin,view);var b=mesh.bounds;Object.DestroyImmediate(mesh);
            if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
        }
        if(first)throw new InvalidOperationException("Missing imported meat geometry");return bounds;
    }
    static Transform Spin(Transform view)=>view.GetComponentsInChildren<Transform>().Single(t=>t.name=="Spin");
    public static bool SafeVisual(Transform view)=>view!=null&&view.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&
        view.GetComponentsInChildren<Animator>(true).All(a=>!a.applyRootMotion&&a.runtimeAnimatorController!=null&&
            a.runtimeAnimatorController.animationClips.All(c=>Butcher25DSetup.Matches(c.name,"Meat_Spin")&&c.events.Length==0));
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Meat checks timed out");
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            var display=Object.FindAnyObjectByType<ArenaPresentation25D>();
            if(phase==1)
            {
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                hero=Object.FindAnyObjectByType<PlayerMovement>().GetComponent<Health>();
                foreach(var carrier in Object.FindObjectsByType<CarcassCarrier>())carrier.enabled=false;
                drops=new Carcass[5];bottoms=new float[5];masses=new float[5];rotations=new Quaternion[5];
                vertexIndices=new int[5];vertices=new Vector3[5];
                for(int i=0;i<3;i++)
                {
                    var animal=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/"+Animals25DSetup.Names[i].ToLowerInvariant()+".prefab"),new Vector3(-9+i*2,-4),Quaternion.identity);
                    float mass=animal.MassKg;drops[i]=animal.Kill();Check(drops[i].MassKg==mass,"Animal kill retains original kg: "+i);
                }
                var heroes=new[]{hero,Object.FindAnyObjectByType<EnemyChase>().GetComponent<Health>()};
                for(int i=0;i<2;i++){float mass=heroes[i].MaximumKg;heroes[i].TakeDamage(100000);drops[3+i]=heroes[i].GetComponent<HeroCarcassSpawner>().CurrentCarcass;Check(drops[3+i].MassKg==mass,"Hero kill retains maximum kg: "+i);}
                due=Time.time+.2f;phase=2;return;
            }
            if(phase==2)
            {
                for(int i=0;i<drops.Length;i++)
                {
                    var view=display.VisualFor(drops[i]);var bounds=BoundsOf(view);bottoms[i]=bounds.min.y;masses[i]=drops[i].MassKg;rotations[i]=Spin(view).localRotation;
                    var baked=Animals25DSetup.BakeWorld(view.GetComponentInChildren<SkinnedMeshRenderer>(),view);var points=baked.vertices;
                    vertexIndices[i]=Enumerable.Range(0,points.Length).OrderByDescending(n=>points[n].x*points[n].x+points[n].z*points[n].z).First();
                    vertices[i]=points[vertexIndices[i]];Object.DestroyImmediate(baked);
                    Check(Mathf.Abs(bounds.min.y-Meat25DSetup.HoverGap)<.005f,"Meat hovers at fixed .25m ground clearance: "+i);
                    Check(view.GetComponentsInChildren<MonoBehaviour>().Length==0&&view.GetComponentsInChildren<Collider>().Length==0&&view.GetComponentsInChildren<Collider2D>().Length==0,"Meat visual contains no gameplay scripts/physics: "+i);
                    var animator=view.GetComponentInChildren<Animator>();Check(animator!=null&&!animator.applyRootMotion&&animator.GetCurrentAnimatorClipInfo(0).Single().clip.events.Length==0,"Only visual spin Animator, no root motion or combat events: "+i);
                }
                var small=BoundsOf(display.VisualFor(drops[0])).size;var large=BoundsOf(display.VisualFor(drops[3])).size;
                Check(large.y>small.y*1.7f,"Hero meat is visibly larger than animal meat");
                due=Time.time+.65f;phase=3;return;
            }
            if(phase==3)
            {
                for(int i=0;i<drops.Length;i++)
                {
                    var view=display.VisualFor(drops[i]);Check(Quaternion.Angle(rotations[i],Spin(view).localRotation)>25,"Authored animation actually rotates the meat: "+i);
                    var baked=Animals25DSetup.BakeWorld(view.GetComponentInChildren<SkinnedMeshRenderer>(),view);
                    Check(Vector3.Distance(vertices[i],baked.vertices[vertexIndices[i]])>.02f,"Actual skinned meat vertices rotate, not just an unused bone: "+i);Object.DestroyImmediate(baked);
                    Check(Mathf.Abs(BoundsOf(view).min.y-bottoms[i])<.005f&&drops[i].MassKg==masses[i],"Spin does not bob or change gameplay mass: "+i);
                }
                Butcher25DChecks.CaptureAt(ArenaSpace25D.ToWorld(new Vector2(-7,-4),.4f),"meat_animal",2);
                Butcher25DChecks.CaptureAt(ArenaSpace25D.ToWorld(drops[3].transform.position,.6f),"meat_hero",2);
                hero.Revive();var carrier=hero.GetComponent<CarcassCarrier>();
                Check(carrier.TryCollect(drops[1]),"Existing pickup accepts the complete 10kg cow meat");
                due=Time.time+.15f;phase=4;return;
            }
            if(phase==4)
            {
                var carrier=hero.GetComponent<CarcassCarrier>();Check(!display.VisualFor(drops[1]).gameObject.activeSelf,"Stored meat disappears from ground");
                Check(display.VisualFor(hero).Find("Cargo").GetComponentsInChildren<Animator>().Length==1,"Carried cargo also uses the new spinning meat model");
                var workshop=hero.GetComponent<OwnerEconomy>().Factory;
                Check(carrier.DeliverTo(workshop)==10&&Math.Abs(workshop.RawKg-10)<.001,"Existing factory accepts unchanged meat mass");
                Directory.CreateDirectory("docs/validation");File.WriteAllText("docs/validation/010-meat.txt",$"PASS: {count} actual Unity Play Mode assertions\nByte-stable setup; real animal/hero deaths; small/large models; authored spin; fixed clearance; no combat scripts/events/physics; complete mass, pickup, cargo and factory delivery preserved.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_MEAT_PASS: "+count+" assertions");phase=5;due=Time.time+.2f;return;
            }
            if(phase==5)Stop();
        }
        catch(Exception error){Debug.LogError("HOOKWARS_MEAT_FAIL: "+error);Stop();}
    }
    static void Stop(){phase=99;EditorApplication.isPlaying=false;}
}
