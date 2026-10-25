using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HelperRecoveryChecks
{
    public const string Key="HookWars.HelperRecoveryChecks";
    static int phase,count,recoveries;
    static float due,started;
    static double ready,deadline;
    static string report;
    static PlayerProgression owner;
    static SausageFactory factory;
    static ButcherHelper helper;
    static Carcass meat;
    static GameObject obstacle;
    static Vector3 before;
    static HelperRecoveryChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe(){phase=count=0;due=0;report="";ready=EditorApplication.timeSinceStartup+3;deadline=ready+120;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);count++;report+="PASS "+text+"\n";Debug.Log("HELPER_RECOVERY_CHECK: "+text);}
    static bool Near(double a,double b)=>Math.Abs(a-b)<.001;
    static Carcass Meat(Vector3 at,float mass){var c=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath),at,Quaternion.identity);c.Initialize(mass);return c;}
    static void Next(int next,float wait){phase=next;due=Time.time+wait;}
    static void Finish(bool passed,string text)
    {
        Time.timeScale=1;SessionState.SetBool(Key+".Passed",passed);File.WriteAllText("docs/validation/026-helper-recovery.txt",report+text+"\n");phase=99;EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Recovery phase "+phase);
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                Application.runInBackground=true;
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                foreach(var a in Object.FindObjectsByType<Animal>())a.gameObject.SetActive(false);
                foreach(var c in Object.FindObjectsByType<Carcass>())Object.Destroy(c.gameObject);
                foreach(var c in Object.FindObjectsByType<Collider2D>())if(c.GetComponent<GameBuilding>()!=null||c.GetComponent<PermanentRock>()!=null||c.GetComponent<HookAnchor>()!=null)c.enabled=false;
                var hero=Object.FindAnyObjectByType<PlayerMovement>();hero.enabled=false;owner=hero.GetComponent<PlayerProgression>();owner.transform.position=new Vector3(-17,8);
                owner.GetComponent<CarcassCarrier>().enabled=false;
                factory=owner.GetComponent<OwnerEconomy>().Factory;factory.enabled=false;factory.transform.position=new Vector3(-14,-5);
                helper=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ButcherHelper>(GameplayRevisionSetup.HelperPath),new Vector3(-10,0),Quaternion.identity);
                helper.Bind(factory,10);helper.GetComponent<CarcassCarrier>().enabled=false;before=helper.transform.position;Next(2,3.3f);return;
            }
            if(phase==2)
            {
                Check(helper.IdleRecoveries>=3,"Idle worker repeatedly resets its task once per second, not just once");
                Check(Vector3.Distance(before,helper.transform.position)<.001f,"No available work does not cause random wandering");
                meat=Meat(helper.transform.position,4);Check(helper.GetComponent<CarcassCarrier>().TryCollect(meat),"Real stored cargo fixture collected");
                Object.Destroy(meat.gameObject);Next(3,.2f);return;
            }
            if(phase==3)
            {
                Check(!helper.Returning&&helper.GetComponent<CarcassCarrier>().Count==0,"Destroyed stored meat cannot leave a phantom delivery task");
                meat=Meat(helper.transform.position+Vector3.right*2,3);started=Time.time;Next(4,0);return;
            }
            if(phase==4)
            {
                if(factory.RawKg<3&&Time.time-started<8)return;
                Check(Near(factory.RawKg,3),"Worker automatically finds and delivers new meat after phantom cargo removal");
                helper.enabled=false;helper.transform.position=new Vector3(-10,0);
                var timing=helper.GetComponent<HeroActionTiming25D>();timing.enabled=false;
                EconomyChecks.Set(timing,"axeImpact",(Action)(()=>{throw new InvalidOperationException("Stale swing must be cancelled");}));
                EconomyChecks.Set(timing,"<AxeStarted>k__BackingField",Time.time-2);
                Check(timing.AxePending,"Fault fixture contains an actual stale pending callback");
                recoveries=helper.IdleRecoveries;helper.enabled=true;Next(5,2.3f);return;
            }
            if(phase==5)
            {
                Check(helper.IdleRecoveries>recoveries&&!helper.GetComponent<HeroActionTiming25D>().AxePending,"Independent idle watchdog cancels stale swing that would block Update forever");
                helper.GetComponent<HeroActionTiming25D>().enabled=true;
                meat=Meat(helper.transform.position+Vector3.right*2,2);started=Time.time;Next(6,0);return;
            }
            if(phase==6)
            {
                if(factory.RawKg<5&&Time.time-started<8)return;
                Check(Near(factory.RawKg,5),"Worker resumes gathering after stale animation cancellation");
                helper.enabled=false;helper.transform.position=new Vector3(-11,0);
                obstacle=new GameObject("Detour wall");obstacle.layer=LayerMask.NameToLayer("Buildings");obstacle.transform.position=new Vector3(-8,0);obstacle.AddComponent<BoxCollider2D>().size=new Vector2(1,5);
                meat=Meat(new Vector3(-5,0),2);helper.enabled=true;started=Time.time;Next(7,0);return;
            }
            if(phase==7)
            {
                if(factory.RawKg<7&&Time.time-started<15)return;
                Check(Near(factory.RawKg,7),"Worker follows an existing detour around a wall through pickup and delivery");
                Object.Destroy(obstacle);helper.enabled=false;helper.transform.position=new Vector3(-10,-5);
                Check(helper.GetComponent<CarcassCarrier>().TryCollect(Meat(helper.transform.position,2)),"Blocked delivery fixture keeps real meat");
                obstacle=new GameObject("Crowd overlap");obstacle.layer=LayerMask.NameToLayer("PlayerTarget");obstacle.transform.position=helper.transform.position;obstacle.AddComponent<CircleCollider2D>().radius=.4f;
                before=helper.transform.position;recoveries=helper.IdleRecoveries;helper.enabled=true;started=Time.time;Next(8,0);return;
            }
            if(phase==8)
            {
                if(factory.RawKg<9&&Time.time-started<9)return;
                Check(Vector3.Distance(before,helper.transform.position)>.6f&&Near(factory.RawKg,9),"Worker escapes an overlapping crowd collider and delivers without teleporting or dropping meat");
                Object.Destroy(obstacle);helper.enabled=false;helper.transform.position=factory.transform.position+Vector3.right*1.5f;
                EconomyChecks.Invoke(factory,"TryAcceptMass",3d);
                Check(helper.GetComponent<CarcassCarrier>().TryCollect(Meat(helper.transform.position,2)),"Full-storage fixture stores two kg");
                recoveries=helper.IdleRecoveries;before=helper.transform.position;helper.enabled=true;Next(9,2.3f);return;
            }
            if(phase==9)
            {
                Check(helper.IdleRecoveries==recoveries&&Near(helper.GetComponent<CarcassCarrier>().CurrentMassKg,2)&&Vector3.Distance(before,helper.transform.position)<.001f,"Full raw storage is legitimate waiting and watchdog preserves its cargo and position");
                factory.Advance(4);Next(10,.2f);return;
            }
            if(phase==10)
            {
                Check(!helper.Returning&&Near(factory.RawKg,12),"Delivery resumes immediately after storage processing frees room");
                helper.enabled=false;factory.Advance(2);
                Check(helper.GetComponent<CarcassCarrier>().TryCollect(Meat(helper.transform.position,.00001f)),"Tiny positive meat residue is a real stored item");
                helper.enabled=true;Next(12,.2f);return;
            }
            if(phase==12)
            {
                Check(!helper.Returning&&Math.Abs(factory.RawKg-11.00001)<.0000001,"Tiny positive cargo is delivered instead of keeping an endless return task");
                recoveries=helper.IdleRecoveries;MatchPause.Instance.SetPaused(true);phase=11;ready=EditorApplication.timeSinceStartup+1.3;return;
            }
            if(phase==11)
            {
                Check(helper.IdleRecoveries==recoveries,"Paused game does not repeatedly reset worker tasks");MatchPause.Instance.SetPaused(false);
                string summary="HOOKWARS_HELPER_RECOVERY_PASS: "+count+" assertions in Unity Play Mode";Debug.Log(summary);Finish(true,summary);
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(false,"FAIL "+error);}
    }
}
