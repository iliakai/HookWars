using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HelperDeliveryChecks
{
    public const string Key="HookWars.HelperDeliveryChecks";
    static int phase,count,bank;
    static float due,started;
    static double ready,deadline;
    static string report;
    static PlayerProgression owner;
    static SausageFactory factory;
    static ButcherHelper helper;
    static Animal prey;
    static Carcass meat;
    static bool sawCargo,sawKill;
    static Vector3 rest;
    static int recoveries;
    static HelperDeliveryChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run(){ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();}
    static void Subscribe(){phase=count=bank=0;due=0;report="";ready=EditorApplication.timeSinceStartup+3;deadline=ready+100;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);count++;report+="PASS "+text+"\n";Debug.Log("HELPER_DELIVERY_CHECK: "+text);}
    static Vector3 FreePoint(float distance)
    {
        for(int i=0;i<32;i++)
        {
            float a=i*Mathf.PI/16;var p=factory.transform.position+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*distance;
            if(ArenaLayout.IsInsideBank(p,p.x>0,.4f)&&WorldCollision.FreeCircle(p,.4f))return p;
        }
        throw new InvalidOperationException("No free fixture point on factory bank");
    }
    static double Accepted=>factory.RawKg+owner.GetComponent<OwnerEconomy>().ProductKg*2;
    static void Finish(bool ok,string text){SessionState.SetBool(Key+".Passed",ok);File.WriteAllText("docs/validation/027-helper-delivery.txt",report+text+"\n");phase=99;EditorApplication.isPlaying=false;}
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Delivery phase "+phase);
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                Application.runInBackground=true;
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                foreach(var a in Object.FindObjectsByType<Animal>())a.gameObject.SetActive(false);
                var hero=Object.FindAnyObjectByType<PlayerMovement>();hero.enabled=false;owner=hero.GetComponent<PlayerProgression>();owner.enabled=false;owner.AddGold(5000);
                factory=owner.GetComponent<OwnerEconomy>().Factory;
                Check(factory.isActiveAndEnabled&&factory.GetComponent<BoxCollider2D>().enabled&&!factory.GetComponent<BoxCollider2D>().isTrigger,"Real starting factory retains enabled solid footprint and live processing");
                Check(factory.TryOrderHelper(),"Helper is bought and spawned by the ordinary factory API");helper=Object.FindObjectsByType<ButcherHelper>().Single();
                Check(helper.GetComponent<CarcassCarrier>().enabled&&helper.transform.parent==factory.transform,"Ordinary carrier stays enabled and helper remains a child of its real factory");
                prey=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),FreePoint(4.5f),Quaternion.identity);
                started=Time.time;sawCargo=sawKill=false;phase=2;return;
            }
            if(phase==2)
            {
                sawKill|=prey==null||prey.IsDead;sawCargo|=helper.Returning;
                if(Accepted<9.99&&Time.time-started<15)return;
                Check(sawKill,"Worker actually kills a moving animal on its own bank");Check(sawCargo,"Worker actually collects the animal meat before delivery");
                Check(Accepted>=9.99&&!helper.Returning,"Animal meat reaches the live factory with its solid collider enabled; p="+helper.transform.position+" cargo="+helper.GetComponent<CarcassCarrier>().CurrentMassKg);
                rest=helper.transform.position;recoveries=helper.IdleRecoveries;phase=3;due=Time.time+2.5f;return;
            }
            if(phase==3)
            {
                Check(Vector3.Distance(rest,helper.transform.position)<.02f&&helper.IdleRecoveries==recoveries,"Completed delivery does not trigger periodic sideways movement or reassignment");
                meat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath),FreePoint(5),Quaternion.identity);meat.Initialize(6);
                started=Time.time;sawCargo=false;phase=4;return;
            }
            if(phase==4)
            {
                sawCargo|=helper.Returning;if((Accepted<15.99||helper.Returning)&&Time.time-started<15)return;
                Check(sawCargo&&Accepted>=15.99&&!helper.Returning,"Worker picks up loose meat on its own bank and completes a second delivery to the solid factory: p="+helper.transform.position+" factory="+factory.transform.position+" cargo="+helper.GetComponent<CarcassCarrier>().CurrentMassKg+" accepted="+Accepted+" meatParent="+(meat!=null?meat.transform.parent?.name:"gone")+" heroCargo="+owner.GetComponent<CarcassCarrier>().CurrentMassKg);
                Check(factory.GetComponent<BoxCollider2D>().enabled,"Solid factory collision stayed enabled throughout both full harvesting cycles");
                if(bank++==0)
                {
                    helper.gameObject.SetActive(false);owner=Object.FindAnyObjectByType<EnemyChase>().GetComponent<PlayerProgression>();owner.enabled=false;owner.AddGold(5000);
                    factory=owner.GetComponent<OwnerEconomy>().Factory;
                    Check(factory.transform.position.x>0&&factory.GetComponent<BoxCollider2D>().enabled,"Opposite owner retains a solid factory on the right bank");
                    Check(factory.TryOrderHelper(),"Opposite owner orders its own helper normally");helper=Object.FindObjectsByType<ButcherHelper>().Single();
                    prey=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),FreePoint(4.5f),Quaternion.identity);
                    started=Time.time;sawCargo=sawKill=false;phase=2;due=0;return;
                }
                string summary="HOOKWARS_HELPER_DELIVERY_PASS: "+count+" assertions in Unity Play Mode";Debug.Log(summary);Finish(true,summary);
            }
        }
        catch(Exception e){Debug.LogException(e);Finish(false,"FAIL "+e);}
    }
}
