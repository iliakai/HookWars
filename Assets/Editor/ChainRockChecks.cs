using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ChainRockChecks
{
    public const string Key="HookWars.ChainRockChecks";
    static int phase,round,frames,bodies,contacts;
    static double ready,deadline,physicsTotal,physicsPeak;
    static float due,maxSpeed,maxError,maxDistance;
    static HookAbility[] hooks;
    static ButcherVisual25D[] visuals;
    static PermanentRock[] rocks;
    static bool baseline;
    static ChainRockChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run(bool before)
    {
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key+".Baseline",before);SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe()
    {
        baseline=SessionState.GetBool(Key+".Baseline",false);phase=round=frames=bodies=contacts=0;physicsTotal=physicsPeak=0;maxSpeed=maxError=maxDistance=0;
        ready=EditorApplication.timeSinceStartup+3;deadline=ready+150;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Chain rock probe timed out");
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying)return;
            if(phase==1)
            {
                foreach(var a in Object.FindObjectsByType<EnemyHookAI>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyChase>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyMelee>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<PlayerMovement>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<HookAnchor>())Object.Destroy(a.gameObject);
                hooks=Object.FindObjectsByType<HookAbility>().Where(h=>h.GetComponent<PlayerProgression>()!=null).ToArray();
                var display=Object.FindAnyObjectByType<ArenaPresentation25D>();
                if(hooks.Any(h=>display.VisualFor(h.GetComponent<Health>())==null))return;
                visuals=hooks.Select(h=>display.VisualFor(h.GetComponent<Health>()).GetComponentInChildren<ButcherVisual25D>()).ToArray();
                rocks=new PermanentRock[hooks.Length];
                for(int i=0;i<hooks.Length;i++)
                {
                    var aim=hooks[i].GetComponentInChildren<PlayerAim>();if(aim!=null){aim.enabled=false;EconomyChecks.Set(aim,"<AimDirection>k__BackingField",Vector2.right);}
                    rocks[i]=Object.Instantiate(AssetDatabase.LoadAssetAtPath<PermanentRock>("Assets/Prefabs/PermanentRock.prefab"));
                    EconomyChecks.Set(hooks[i],"targetLayers",(LayerMask)0);EconomyChecks.Set(hooks[i],"carcassLayers",(LayerMask)0);EconomyChecks.Set(hooks[i],"range",20f);
                }
                phase=2;due=Time.time+.25f;return;
            }
            if(phase==2&&Time.time>=due)
            {
                for(int i=0;i<hooks.Length;i++)
                {
                    hooks[i].CancelHook();EconomyChecks.Set(hooks[i],"nextThrowTime",0f);
                    Vector2 away=ArenaSpace25D.ToLogical(visuals[i].Socket.position)-(Vector2)hooks[i].transform.position;
                    if(away.sqrMagnitude<.001f)away=Vector2.up;away.Normalize();
                    float low=0,high=2;
                    for(int n=0;n<20;n++){float mid=(low+high)/2;Vector2 c=away*mid;float d=new Vector2(Mathf.Max(0,Mathf.Abs(c.x)-.5f),Mathf.Max(0,Mathf.Abs(c.y)-.5f)).magnitude;if(d<.505f)low=mid;else high=mid;}
                    rocks[i].transform.position=hooks[i].transform.position+(Vector3)(away*high);
                    EconomyChecks.Set(rocks[i],"collisionHeight",round==1?1.5f:.62927854f);
                    hooks[i].TryThrow(hooks[i].transform.position.x<0?Vector2.right:Vector2.left);
                }
                Physics2D.SyncTransforms();ChainEnvironment25D.Instance.Refresh();phase=3;due=Time.time+4.5f;return;
            }
            if(phase==3)
            {
                var world=ChainPhysicsWorld25D.Instance;physicsTotal+=world.LastSimulationMilliseconds;physicsPeak=Math.Max(physicsPeak,world.LastSimulationMilliseconds);frames++;
                var active=Object.FindObjectsByType<Rigidbody>().Where(b=>!b.isKinematic&&b.gameObject.layer==24).ToArray();bodies=Mathf.Max(bodies,active.Length);
                foreach(var body in active)
                {
                    if(!float.IsFinite(body.position.x)||!float.IsFinite(body.linearVelocity.x))throw new InvalidOperationException("Non-finite chain body");
                    maxSpeed=Mathf.Max(maxSpeed,body.linearVelocity.magnitude);
                    maxDistance=Mathf.Max(maxDistance,hooks.Min(h=>Vector3.Distance(body.position,ArenaSpace25D.ToWorld(h.transform.position))));
                }
                foreach(var chain in Object.FindObjectsByType<HookChain25D>())foreach(var span in chain.Spans)maxError=Mathf.Max(maxError,span.MaximumAnchorError);
                foreach(var visual in visuals)foreach(var box in ChainEnvironment25D.Instance.Obstacles)
                    if(box!=null&&box.enabled&&Vector3.Distance(box.ClosestPoint(visual.Socket.position),visual.Socket.position)<.08f)contacts++;
                if(Time.time<due)return;
                foreach(var hook in hooks)hook.CancelHook();
                if(++round<3){phase=2;due=Time.time+.1f;return;}
                string report=$"Actual Unity PhysX Play Mode; baseline={baseline}\nTwo simultaneous 20m hooks, three casts each. Native .629m rocks and adversarial 1.5m hand-contact proxy.\nPeak bodies={bodies}; peak colliders={bodies}; maximum speed={maxSpeed:F3}m/s; maximum joint error={maxError:F3}m; maximum distance to nearest owner={maxDistance:F3}m\nHand/proxy contact observations={contacts}; physics mean={physicsTotal/Math.Max(1,frames):F3}ms, peak={physicsPeak:F3}ms\n";
                File.WriteAllText("docs/validation/016-chain-"+(baseline?"before":"after")+".txt",report);
                if(!baseline&&(maxSpeed>80||maxError>5||maxDistance>25))throw new InvalidOperationException("Chain instability remains: "+report);
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_CHAIN_ROCK_PASS: "+report);Stop();
            }
        }
        catch(Exception e){Debug.LogError("HOOKWARS_CHAIN_ROCK_FAIL: "+e);Stop();}
    }
    static void Stop(){phase=99;EditorApplication.isPlaying=false;}
}
