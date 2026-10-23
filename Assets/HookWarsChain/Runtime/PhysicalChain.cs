using System;
using System.Collections.Generic;
using UnityEngine;
namespace HookWars.Chain
{
    public sealed partial class PhysicalChain : MonoBehaviour
    {
        [Header("Length and appearance")]
        [Min(.168f)] public float length=4f;
        [Min(.1f)] public float linkScale=1f;
        [Range(2,256)] public int maximumLinks=128;
        public Mesh linkMesh;
        public Material material;
        [Header("Endpoints: Rigidbody gives two-way forces; Transform uses a kinematic follower")]
        public Rigidbody startBody;
        public Vector3 startBodyLocalPoint;
        public Transform startSocket;
        public Rigidbody endBody;
        public Vector3 endBodyLocalPoint;
        public Transform endSocket;
        [Header("Physics")]
        [Min(.001f)] public float massPerLink=.08f;
        [Range(5,85)] public float bendLimit=70f;
        [Range(0,80)] public float twistLimit=40f;
        [Range(6,40)] public int solverIterations=20;
        public bool selfCollision=true;
        public bool buildOnStart=true;
        [Tooltip("Optional emergency correction; not physically exact. Off by default.")]
        public bool emergencyProjection=false;
        [Tooltip("Physical bodies use this layer. Configure collision matrix in your project.")]
        [Range(0,31)] public int physicsLayer=0;
        public int LinkCount {get;private set;}
        public float ActualLength {get;private set;}
        public IReadOnlyList<Rigidbody> Links => streaming ? streamBodies : bodies;
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly List<Collider[]> colliders=new List<Collider[]>();
        GameObject simulation;
        Rigidbody startFollower,endFollower;
        Mesh generatedMesh;
        Material generatedMaterial;
        bool built;
        void Start(){if(buildOnStart)Rebuild();}
        void OnEnable(){if(simulation!=null)simulation.SetActive(true);}
        void OnDisable(){if(simulation!=null)simulation.SetActive(false);StopStreaming();}
        void OnDestroy(){Clear();DisposeStreaming();if(generatedMesh!=null)Destroy(generatedMesh);if(generatedMaterial!=null)Destroy(generatedMaterial);}
        public void SetLength(float metres)
        {
            if(!IsFinite(metres)||metres<=0)throw new ArgumentOutOfRangeException(nameof(metres));
            float old=length;length=metres;
            try{Rebuild();}catch{length=old;throw;}
        }
        static bool IsFinite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public void Rebuild()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Build the chain in Play Mode.");
            if(!IsFinite(length)||!IsFinite(linkScale)||length<=0||linkScale<.1f)throw new InvalidOperationException("Invalid chain dimensions.");
            float pitch=ChainGeometry.Pitch*linkScale;
            double requested=Math.Ceiling((double)length/pitch);
            int cap=Mathf.Clamp(maximumLinks,2,256);
            if(requested>cap)throw new InvalidOperationException("Requested length exceeds maximumLinks. Increase the cap or linkScale.");
            int count=Mathf.Max(2,(int)requested);float actual=count*pitch;
            Vector3 a=StartPosition();bool attachEnd=endBody!=null||endSocket!=null;
            Vector3 b=attachEnd?EndPosition():a+Vector3.down*actual;
            if(Vector3.Distance(a,b)>actual+.0001f)throw new InvalidOperationException("Endpoints are farther apart than the chain length. Increase length or move endpoints closer.");
            if(attachEnd&&Vector3.Distance(a,b)<pitch*.5f)throw new InvalidOperationException("Separate endpoints by at least half a link pitch before building.");
            // Validate everything before destroying an existing chain.
            Clear();LinkCount=count;ActualLength=actual;
            simulation=new GameObject(name+"_PhysicsChain"); // Unparented: moving/scaling the owner must not teleport rigidbodies.
            simulation.layer=physicsLayer;
            if(linkMesh==null){if(generatedMesh==null)generatedMesh=ChainGeometry.CreateMesh();linkMesh=generatedMesh;}
            if(material==null)
            {
                Shader shader=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline==null?Shader.Find("Standard"):Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)throw new InvalidOperationException("Assign a chain material for this render pipeline.");
                generatedMaterial=new Material(shader){name="ChainSteel"};generatedMaterial.color=new Color(.23f,.26f,.29f);material=generatedMaterial;
                if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.8f);
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.45f);
            }
            Rigidbody first=startBody;
            if(first==null)first=startFollower=CreateFollower("StartFollower",a);
            Rigidbody last=endBody;
            if(attachEnd&&last==null)last=endFollower=CreateFollower("EndFollower",b);
            Vector3[] points=InitialPoints(a,b,count,pitch);
            for(int i=0;i<count;i++)
            {
                var link=new GameObject("Link_"+i.ToString("D3"));link.layer=physicsLayer;link.transform.SetParent(simulation.transform,false);
                link.transform.position=(points[i]+points[i+1])*.5f;
                Vector3 tangent=(points[i+1]-points[i]).normalized;
                Vector3 up=Mathf.Abs(Vector3.Dot(tangent,Vector3.up))>.98f?Vector3.right:Vector3.up;
                link.transform.rotation=Quaternion.LookRotation(tangent,up)*Quaternion.AngleAxis(i%2*90,Vector3.forward);
                var visual=new GameObject("Visual");visual.transform.SetParent(link.transform,false);visual.transform.localScale=Vector3.one*linkScale;
                visual.AddComponent<MeshFilter>().sharedMesh=linkMesh;visual.AddComponent<MeshRenderer>().sharedMaterial=material;
                var rb=link.AddComponent<Rigidbody>();rb.mass=Mathf.Max(.001f,massPerLink);rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations=Mathf.Clamp(solverIterations,6,40);rb.solverVelocityIterations=8;rb.maxAngularVelocity=25;rb.maxDepenetrationVelocity=2;
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping=.03f;rb.angularDamping=.12f;
#else
                rb.drag=.03f;rb.angularDrag=.12f;
#endif
                bodies.Add(rb);colliders.Add(ChainGeometry.AddColliders(link,linkScale));
                Connect(rb,i==0?first:bodies[i-1],points[i],i==0);
            }
            if(attachEnd)Connect(bodies[count-1],last,points[count],true);
            // Adjacent pairs overlap by design and are held by joints; ignore their contacts.
            // Non-neighbour contacts retain self-collision when enabled.
            for(int i=0;i<count;i++)for(int j=i+1;j<count;j++)
                if(!selfCollision||j==i+1)foreach(var ca in colliders[i])foreach(var cb in colliders[j])Physics.IgnoreCollision(ca,cb,true);
            // The mounting body should not collide with the terminal link.
            IgnoreMount(first,colliders[0]);if(last!=null)IgnoreMount(last,colliders[count-1]);
            Physics.SyncTransforms();built=true;
        }
        static void IgnoreMount(Rigidbody mount,Collider[] terminal)
        {
            foreach(var c in mount.GetComponentsInChildren<Collider>())
                if(c.attachedRigidbody==mount)foreach(var t in terminal)Physics.IgnoreCollision(c,t,true);
        }
        Rigidbody CreateFollower(string label,Vector3 p)
        {
            var o=new GameObject(label);o.transform.SetParent(simulation.transform,false);o.transform.position=p;
            var rb=o.AddComponent<Rigidbody>();rb.isKinematic=true;rb.interpolation=RigidbodyInterpolation.Interpolate;return rb;
        }
        void Connect(Rigidbody body,Rigidbody other,Vector3 point,bool endpoint)
        {
            var joint=body.gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=other;joint.autoConfigureConnectedAnchor=false;
            joint.anchor=body.transform.InverseTransformPoint(point);joint.connectedAnchor=other.transform.InverseTransformPoint(point);
            joint.axis=Vector3.forward;joint.secondaryAxis=Vector3.up;
            joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
            joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=endpoint?ConfigurableJointMotion.Free:ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit=new SoftJointLimit{limit=-twistLimit};joint.highAngularXLimit=new SoftJointLimit{limit=twistLimit};
            joint.angularYLimit=joint.angularZLimit=new SoftJointLimit{limit=bendLimit};
            joint.enableCollision=false;joint.enablePreprocessing=false;
            joint.projectionMode=emergencyProjection?JointProjectionMode.PositionAndRotation:JointProjectionMode.None;
            joint.projectionDistance=.02f*linkScale;joint.projectionAngle=15;
        }
        Vector3 StartPosition()=>startBody!=null?startBody.transform.TransformPoint(startBodyLocalPoint):startSocket!=null?startSocket.position:transform.position;
        Vector3 EndPosition()=>endBody!=null?endBody.transform.TransformPoint(endBodyLocalPoint):endSocket.position;
        void FixedUpdate()
        {
            if(streaming)return; // Dedicated world prepares after the final visual pose.
            if(!built)return;
            if(startFollower!=null)startFollower.MovePosition(StartPosition());
            if(endFollower!=null&&endSocket!=null)endFollower.MovePosition(endSocket.position);
        }
        void Clear()
        {
            built=false;
            if(simulation!=null){simulation.SetActive(false);Destroy(simulation);}
            simulation=null;startFollower=null;endFollower=null;bodies.Clear();colliders.Clear();LinkCount=0;ActualLength=0;
        }
        // Discrete circular arc with every segment exactly pitch long, avoiding a stretched initial configuration.
        public static Vector3[] InitialPoints(Vector3 a,Vector3 b,int n,float pitch)
        {
            float distance=Vector3.Distance(a,b);var result=new Vector3[n+1];Vector3 direction=(b-a).normalized;
            if(distance>=n*pitch-.00001f){for(int i=0;i<=n;i++)result[i]=Vector3.Lerp(a,b,(float)i/n);return result;}
            float lo=0,hi=2*Mathf.PI/n;
            for(int k=0;k<50;k++)
            {
                float theta=(lo+hi)*.5f;
                float chord=pitch*Mathf.Sin(n*theta*.5f)/Mathf.Sin(theta*.5f);
                if(chord>distance)lo=theta;else hi=theta;
            }
            float step=(lo+hi)*.5f,half=n*step*.5f,radius=pitch/(2*Mathf.Sin(step*.5f));
            Vector3 down=Vector3.ProjectOnPlane(Vector3.down,direction).normalized;
            if(down.sqrMagnitude<.01f)down=Vector3.right;
            Vector3 middle=(a+b)*.5f;
            for(int i=0;i<=n;i++){float phi=-half+i*step;result[i]=middle+direction*(radius*Mathf.Sin(phi))+down*(radius*(Mathf.Cos(phi)-Mathf.Cos(half)));}
            result[0]=a;result[n]=b;return result;
        }
    }
}
