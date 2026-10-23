using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HookWars.Chain;

// Dedicated PhysX scene: finer chain steps do not change global Physics2D or
// project timing. It contains only chain links, mounts and collision proxies.
[DefaultExecutionOrder(2000)]
public sealed class ChainPhysicsWorld25D : MonoBehaviour
{
    public static ChainPhysicsWorld25D Instance {get;private set;}
    private Scene scene;
    private PhysicsScene physics;
    private readonly List<PhysicalChain> chains=new List<PhysicalChain>();
    [Range(1,32)] public int substeps=16;
    public double LastSimulationMilliseconds {get;private set;}
    public double TotalSimulationMilliseconds {get;private set;}
    public double PeakSimulationMilliseconds {get;private set;}
    public int SimulationSteps {get;private set;}
    private void Awake()
    {
        Instance=this;scene=SceneManager.CreateScene("Arena25D chain physics",new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();
    }
    public void Own(GameObject root)=>SceneManager.MoveGameObjectToScene(root,scene);
    public void Register(PhysicalChain chain){if(!chains.Contains(chain))chains.Add(chain);}
    private void LateUpdate()
    {
        if(Time.deltaTime<=0)return;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        foreach(var chain in chains)if(chain!=null)chain.PrepareStreamingStep();
        int steps=Mathf.Max(1,Mathf.CeilToInt(Time.deltaTime/(Time.fixedDeltaTime/Mathf.Max(1,substeps))));
        foreach(var chain in chains)if(chain!=null)steps=Mathf.Max(steps,Mathf.Min(64,Mathf.CeilToInt(chain.MountTravel/.025f)));
        for(int i=1;i<=steps;i++)
        {
            foreach(var chain in chains)if(chain!=null)chain.StreamSubstep((float)i/steps);
            physics.Simulate(Time.deltaTime/steps);
            foreach(var chain in chains)if(chain!=null)chain.MeasureStreamError();
        }
        watch.Stop();LastSimulationMilliseconds=watch.Elapsed.TotalMilliseconds;
        if(chains.Exists(c=>c!=null&&c.ActiveBodyCount>0))
        {SimulationSteps++;TotalSimulationMilliseconds+=LastSimulationMilliseconds;PeakSimulationMilliseconds=System.Math.Max(PeakSimulationMilliseconds,LastSimulationMilliseconds);}
    }
    private void OnDestroy()
    {
        if(Instance==this)Instance=null;chains.Clear();
        if(Application.isPlaying&&scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
    }
}
