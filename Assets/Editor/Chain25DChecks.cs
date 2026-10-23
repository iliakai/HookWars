using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HookWars.Chain;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Chain25DChecks
{
    public const string Key="HookWars.ChainChecks";
    static int phase,count,peakBodies,peakColliders,series;
    static double ready,deadline;
    static float due;
    static HookAbility[] hooks;
    static BoneWall obstacle;
    static readonly List<ProfilerRecorder> recorders=new List<ProfilerRecorder>();
    static readonly List<string> recorderNames=new List<string>();
    static readonly Dictionary<string,List<long>> samples=new Dictionary<string,List<long>>();
    static readonly List<double> localCosts=new List<double>();
    static float maximumError;
    static Chain25DChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    [MenuItem("HookWars/25D/World/Check chain stress Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();
        Chain25DSetup.Configure();string before=Signature();Chain25DSetup.Configure();Check(before==Signature(),"Chain setup is byte stable");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static string Signature()=>string.Join("",Directory.GetFiles(Chain25DSetup.Output).OrderBy(p=>p).Select(p=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)))))+File.ReadAllText(Arena25DSetup.ScenePath);
    static void Subscribe(){phase=count=series=peakBodies=peakColliders=0;maximumError=0;ready=EditorApplication.timeSinceStartup+3;deadline=ready+150;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);count++;Debug.Log("CHAIN_CHECK: "+message);}
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup<ready||EditorApplication.isCompiling)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Chain validation timed out");
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying)return;
            for(int ri=0;ri<recorders.Count;ri++)if(recorders[ri].Valid&&recorders[ri].LastValue>0)samples[recorderNames[ri]].Add(recorders[ri].LastValue);
            var chains=Object.FindObjectsByType<HookChain25D>();
            foreach(var chain in chains)foreach(var span in chain.Spans)
            {
                if(span.MaximumAnchorError>maximumError+.2f)Debug.Log("CHAIN_CHECK_ERROR_DIAGNOSTIC: phase="+phase+" chain="+chain.name+" max="+span.MaximumAnchorError+" issued="+span.IssuedLength+" links="+span.ActiveBodyCount);
                maximumError=Mathf.Max(maximumError,span.MaximumAnchorError);
            }
            if(ChainPhysicsWorld25D.Instance!=null)localCosts.Add(ChainPhysicsWorld25D.Instance.LastSimulationMilliseconds);
            if(phase==1)
            {
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                hooks=new[]{Object.FindAnyObjectByType<PlayerMovement>().GetComponent<HookAbility>(),Object.FindAnyObjectByType<EnemyChase>().GetComponent<HookAbility>()};
                for(int i=0;i<2;i++)
                {
                    hooks[i].transform.position=new Vector3(i==0?-16:16,i==0?-6:6);
                    EconomyChecks.Set(hooks[i],"range",30f);EconomyChecks.Set(hooks[i],"speed",6f);EconomyChecks.Set(hooks[i],"cooldown",0f);
                    EconomyChecks.Set(hooks[i],"targetLayers",(LayerMask)0);EconomyChecks.Set(hooks[i],"obstacleLayers",(LayerMask)0);EconomyChecks.Set(hooks[i],"carcassLayers",(LayerMask)0);
                    Throw(i);
                }
                var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
                foreach(var handle in handles)
                {
                    var d=ProfilerRecorderHandle.GetDescription(handle);
                    if(d.Name.Contains("Physics")&&(d.Name.Contains("Simulate")||d.Name.Contains("Processing")||d.Name.Contains("FetchResults")))
                    {var r=ProfilerRecorder.StartNew(d.Category,d.Name,256);if(r.Valid){recorders.Add(r);recorderNames.Add(d.Name);samples[d.Name]=new List<long>();}}
                }
                due=Time.time+4.95f;phase=2;return;
            }
            if(chains.Length>0)
            {
                var bodies=chains.SelectMany(c=>c.Spans).SelectMany(s=>s.Links).Where(b=>b.gameObject.activeInHierarchy).ToArray();
                peakBodies=Mathf.Max(peakBodies,bodies.Length);peakColliders=Mathf.Max(peakColliders,bodies.Sum(b=>b.GetComponentsInChildren<Collider>().Length));
                if(bodies.Any(b=>!float.IsFinite(b.position.x)||!float.IsFinite(b.position.y)||!float.IsFinite(b.position.z)))throw new Exception("Non-finite physical link");
            }
            if(Time.time<due)
            {
                if(phase==2)for(int i=0;i<2;i++){var position=hooks[i].transform.position;position.y=(i==0?-6:6)+Mathf.Sin(Time.time*2)*.4f;hooks[i].transform.position=position;}
                return;
            }
            if(phase==2)
            {
                Check(chains.Length==2&&chains.All(c=>c.ActiveLinks>160),"Two near-full 30m hooks pay out more than 160 physical links each");
                foreach(var c in chains)
                {
                    Debug.Log("CHAIN_CHECK: Long throw maximum constraint error="+c.Spans.Max(s=>s.MaximumAnchorError));
                    Check(c.MaximumGameLength==30&&c.LogicalLength<=30.001f,"Physical rounding never extends gameplay range");
                    var links=c.Spans.SelectMany(s=>s.Links).ToArray();
                    string diagnostic=$"{c.name} logical={c.LogicalLength} issued={c.IssuedPhysicalLength} count={links.Length}\n";
                    for(int j=0;j<links.Length;j++)
                    {
                        var joint=links[j].GetComponent<ConfigurableJoint>();
                        diagnostic+=$"{j} p={links[j].position:F3} velocity={links[j].linearVelocity:F3} other={joint.connectedBody.position:F3} limit={joint.linearLimit.limit:F3}\n";
                    }
                    File.WriteAllText("Temp/HookWarsAutomation/chain-positions-"+c.name+".txt",diagnostic);
                    float worst=links.Max(b=>{var j=b.GetComponent<ConfigurableJoint>();return Vector3.Distance(b.position+b.rotation*j.anchor,j.connectedBody.position+j.connectedBody.rotation*j.connectedAnchor);});
                    Check(worst<.05f,"Long chain remains connected: worst anchor gap="+worst);
                    Check(links.All(b=>!b.isKinematic),"All issued links are dynamic PhysX bodies");
                    Check(links.All(b=>b.GetComponentsInChildren<Collider>().Length==1),"One simplified collider per volumetric link");
                    Check(links.Any(b=>b.position.y<.6f),"Gravity produces real sag below attachment height");
                    Check(links.Any(b=>b.linearVelocity.magnitude>.01f),"Issued links carry simulated velocity");
                    Butcher25DChecks.CaptureAt(links[links.Length/2].position,"physical_chain_close_"+c.name,.7f);
                }
                Butcher25DChecks.CaptureAt(Vector3.zero,"physical_chains_long",18);
                // Return along the actual gameplay route and observe pooled retraction.
                foreach(var h in hooks)EconomyChecks.Set(h,"state",Enum.Parse(typeof(HookAbility).GetNestedType("HookState",BindingFlags.NonPublic),"Returning"));
                due=Time.time+6f;phase=3;return;
            }
            if(phase==3)
            {
                Check(hooks.All(h=>h.ActiveHook==null)&&chains.All(c=>c.ActiveLinks==0),"Both hooks return and all issued links retire");
                int pooled=chains.Sum(c=>c.PooledLinks);SessionState.SetInt(Key+".Pool",pooled);
                SessionState.SetInt(Key+".Bodies",Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length);
                SessionState.SetInt(Key+".Joints",Object.FindObjectsByType<ConfigurableJoint>(FindObjectsInactive.Include).Length);
                foreach(var h in hooks)EconomyChecks.Set(h,"range",5f);
                Throw(0);Throw(1);due=Time.time+2;phase=4;return;
            }
            if(phase==4)
            {
                foreach(var h in hooks)h.CancelHook();
                Check(chains.Sum(c=>c.PooledLinks)==SessionState.GetInt(Key+".Pool",0),"Repeated short throws reuse the existing pool");
                Check(Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length==SessionState.GetInt(Key+".Bodies",0)&&Object.FindObjectsByType<ConfigurableJoint>(FindObjectsInactive.Include).Length==SessionState.GetInt(Key+".Joints",0),"Native Rigidbody and joint counts do not grow across the throw series");
                if(++series<5){Throw(0);Throw(1);due=Time.time+2;return;}
                EconomyChecks.Set(hooks[0],"range",10f);EconomyChecks.Set(hooks[0],"speed",4f);EconomyChecks.Set(hooks[0],"maxBounces",1);
                EconomyChecks.Set(hooks[0],"obstacleLayers",(LayerMask)LayerMask.GetMask("Buildings"));
                obstacle=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BoneWall>(EconomySetup.WallPath),hooks[0].transform.position+Vector3.right*2,Quaternion.identity);
                obstacle.Initialize(hooks[1].GetComponent<PlayerProgression>());
                Throw(0);due=Time.time+.3f;phase=5;return;
            }
            if(phase==5)
            {
                if(hooks[0].ChainPointCount<3)return;
                Check(obstacle.CurrentHealth<obstacle.MaxHealth,"Existing HookAbility damages and reflects from a real wall");
                due=Time.time+.08f;phase=6;return;
            }
            if(phase==6)
            {
                Check(chains.Any(c=>c.Spans.Count>=2&&c.Spans[1].ActiveBodyCount>0),"Logical bend creates a physical guide and independent simulated spans");
                Butcher25DChecks.CaptureAt(obstacle.transform.position==Vector3.zero?Vector3.zero:ArenaSpace25D.ToWorld(obstacle.transform.position,.5f),"physical_chain_bounce",2);
                obstacle.TakeHookDamage(hooks[0].GetComponent<PlayerProgression>(),10000);
                due=Time.time+.12f;phase=7;return;
            }
            if(phase==7)
            {
                Check(obstacle==null,"Obstacle destroyed during active real hook");
                Check(!Object.FindObjectsByType<BoxCollider>().Any(b=>b.name.Contains("BoneWall")),"Destroyed obstacle also removes its chain collision proxy");
                Check(hooks[0].ActiveHook!=null,"Destroying obstacle preserves authoritative hook flight");
                EconomyChecks.Set(hooks[0],"state",Enum.Parse(typeof(HookAbility).GetNestedType("HookState",BindingFlags.NonPublic),"Returning"));
                due=Time.time+3;phase=8;return;
            }
            if(phase==8)
            {
                Check(hooks[0].ActiveHook==null&&chains.All(c=>c.ActiveLinks==0),"Return passes guide and retires all spans");
                Throw(0);due=Time.time+.4f;phase=9;return;
            }
            if(phase==9)
            {
                hooks[0].GetComponent<Health>().TakeDamage(1000);due=Time.time+.15f;phase=10;return;
            }
            if(phase==10)
            {
                Check(chains.All(c=>c.ActiveLinks==0),"Death cancels issued physical chain bodies");
                Check(maximumError<.35f,"Whole-scenario constraint error stays below 0.35m including payout/hand attachment: "+maximumError);
                string report=$"Peak dynamic links={peakBodies}; link colliders={peakColliders}; total rigidbodies including pooled/kinematic={Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length}\n";
                var world=ChainPhysicsWorld25D.Instance;
                report+=$"Dedicated local PhysX base substeps={world.substeps} (adaptive up to 64): actual active steps={world.SimulationSteps}, mean={world.TotalSimulationMilliseconds/Math.Max(1,world.SimulationSteps):F4}ms, peak={world.PeakSimulationMilliseconds:F4}ms\n";
                foreach(var pair in samples)report+=pair.Key+": samples="+pair.Value.Count+", mean ns="+(pair.Value.Count>0?pair.Value.Average():0)+", peak ns="+(pair.Value.Count>0?pair.Value.Max():0)+"\n";
                report+="maximum measured constraint excess across all throws="+maximumError+"m\n";
                File.WriteAllText("Temp/HookWarsAutomation/chain-profile.txt",report);Debug.Log("HOOKWARS_CHAIN_PROFILE: "+report);
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_CHAIN_PASS: "+count+" assertions, real Physics and pooled retraction");Stop();
            }
        }
        catch(Exception error){Debug.LogError("HOOKWARS_CHAIN_FAIL: "+error);Stop();}
    }
    static void Throw(int i){EconomyChecks.Set(hooks[i],"nextThrowTime",0f);hooks[i].TryThrow(i==0?Vector2.right:Vector2.left);}
    static void Stop(){foreach(var r in recorders)r.Dispose();recorders.Clear();recorderNames.Clear();samples.Clear();localCosts.Clear();phase=99;EditorApplication.isPlaying=false;}
}
