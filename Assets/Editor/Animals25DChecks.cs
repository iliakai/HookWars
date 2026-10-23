using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HookWars.Animals;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Animals25DChecks
{
    public const string Key="HookWars.AnimalsChecks";
    static int phase,count; static double ready; static float due;
    static Animal[] animals; static Vector3[] initial; static Carcass[] corpses;
    static Animals25DChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    [MenuItem("HookWars/25D/World/Check animals Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();
        Animals25DSetup.Configure();string before=Signature();Animals25DSetup.Configure();Check(before==Signature(),"Animal setup is byte stable");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static string Signature()=>string.Join("",Directory.GetFiles(Animals25DSetup.Output).OrderBy(p=>p).Select(p=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)))))+File.ReadAllText(Arena25DSetup.ScenePath);
    static void Subscribe(){phase=count=0;ready=EditorApplication.timeSinceStartup+3;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);count++;Debug.Log("ANIMALS_CHECK: "+message);}
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup<ready||EditorApplication.isCompiling)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying)return;
            if(phase==1)
            {
                animals=new Animal[3];initial=new Vector3[3];corpses=new Carcass[3];
                for(int i=0;i<3;i++)
                {
                    animals[i]=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/"+Animals25DSetup.Names[i].ToLowerInvariant()+".prefab"),new Vector3(-9+i*2,-4),Quaternion.identity);
                    initial[i]=animals[i].transform.position;EconomyChecks.Set(animals[i],"direction",Vector2.up);EconomyChecks.Set(animals[i],"nextDirection",float.MaxValue);
                }
                due=Time.time+.4f;phase=2;return;
            }
            if(Time.time<due)return;
            var p=Object.FindAnyObjectByType<ArenaPresentation25D>();
            if(phase==2)
            {
                for(int i=0;i<3;i++)
                {
                    var view=p.VisualFor(animals[i]);var motion=view.GetComponentInChildren<AnimalMotion>();
                    Check(motion!=null&&!motion.automaticSpeed&&motion.IsWalking,"Walk follows voluntary XY movement "+i);
                    Check(animals[i].transform.position.y>initial[i].y && animals[i].VoluntaryVelocity.magnitude>.9f,"Animal actually travels "+i);
                    Check(motion.animator.applyRootMotion==false&&view.GetComponentsInChildren<Collider>().Length==0,"No root motion or demo physics "+i);
                    Butcher25DChecks.CaptureAt(view.position+Vector3.up*.15f,"animal_walk_"+i);
                    animals[i].transform.position=new Vector3(-9+i*2,-4);EconomyChecks.Set(animals[i],"moveSpeed",1000f);
                    foreach(Vector2 side in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
                    {
                        var wall=new GameObject("Animal test cage"); wall.layer=LayerMask.NameToLayer("Buildings");
                        wall.transform.position=animals[i].transform.position+(Vector3)side*.55f;
                        wall.AddComponent<BoxCollider2D>().size=side.x==0?new Vector2(1.3f,.2f):new Vector2(.2f,1.3f);
                    }
                }
                due=Time.time+.5f;phase=3;return;
            }
            if(phase==3)
            {
                for(int i=0;i<3;i++)
                {
                    var motion=p.VisualFor(animals[i]).GetComponentInChildren<AnimalMotion>();
                    Check(animals[i].VoluntaryVelocity==Vector2.zero&&!motion.IsWalking,"Blocked animal selects Graze after pause "+i);
                    var hook=Object.FindAnyObjectByType<PlayerMovement>().GetComponent<HookAbility>();
                    animals[i].GetComponent<HookVictim>().TryCapture(hook);
                    animals[i].transform.position+=Vector3.up*.5f;
                }
                due=Time.time+.1f;phase=4;return;
            }
            if(phase==4)
            {
                for(int i=0;i<3;i++)
                {
                    var motion=p.VisualFor(animals[i]).GetComponentInChildren<AnimalMotion>();Check(!motion.enabled&&!motion.animator.enabled,"Hook movement freezes animation "+i);
                    corpses[i]=animals[i].Kill();Check(animals[i].Kill()==corpses[i],"Exactly one corpse "+i);
                    Check(corpses[i].AnimalSpecies==(Animal.Species)i,"Corpse retains explicit species "+i);
                }
                due=Time.time+.15f;phase=5;return;
            }
            if(phase==5)
            {
                for(int i=0;i<3;i++)
                {
                    var view=p.VisualFor(corpses[i]);Check(view!=null&&view.GetComponentsInChildren<Animator>(true).Length==0&&view.GetComponentsInChildren<AnimalMotion>(true).Length==0,"Static corpse has no active animal/animation scripts "+i);
                    Check(corpses[i].MassKg==new[]{4,10,6}[i],"Species mass preserved "+i);
                    Butcher25DChecks.CaptureAt(view.position+Vector3.up*.1f,"animal_corpse_"+i);
                }
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_ANIMALS_PASS: "+count+" assertions in Unity Play Mode");Stop();
            }
        }
        catch(Exception error){Debug.LogError("HOOKWARS_ANIMALS_FAIL: "+error);Stop();}
    }
    static void Stop(){phase=99;EditorApplication.isPlaying=false;}
}
