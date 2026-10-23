using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HookWars.Butcher
{
    // Native PhysX point-chain. Free angular joints, spherical maximum-distance limits.
    // No LineRenderer spring/Bezier approximation; the rendered links follow simulated bodies.
    public sealed class PhysicalHookChain : MonoBehaviour
    {
        public enum HookState { Ready, Flying, Latched, Retracting }
        [Header("References")]
        public Transform socket;
        public GameObject projectileVisual;
        public Renderer restingHook;
        public Material chainMaterial;
        public LayerMask hitMask = ~0;
        [Header("Throw / reel (metres, seconds)")]
        [Min(.5f)] public float maxLength = 8;
        [Min(1)] public float launchSpeed = 16;
        [Min(.1f)] public float reelSpeed = 7;
        [Min(0)] public float latchHoldSeconds = .4f;
        [Min(.2f)] public float flightTimeout = 2;
        [Min(1)] public float retractTimeout = 8;
        public bool autoRetractAtRange = true;
        [Header("Physics")]
        [Range(.12f,.5f)] public float segmentLength = .22f;
        [Range(.015f,.08f)] public float nodeRadius = .04f;
        [Min(.01f)] public float segmentMass = .08f;
        [Min(.05f)] public float hookMass = .5f;
        [Range(0,1)] public float projectileGravity = .15f;
        [Range(0,2)] public float chainGravity = 1;
        [Range(8,40)] public int solverIterations = 24;
        [Range(.01f,.1f)] public float linearDrag = .03f;
        [Header("Visual links")]
        [Range(.06f,.2f)] public float visualLinkPitch = .10f;
        [Header("Events: connect game logic here")]
        public UnityEvent<Collider> onHookHit = new UnityEvent<Collider>();
        public UnityEvent onReturned = new UnityEvent();
        public UnityEvent onRetractionTimeout = new UnityEvent();
        public HookState State { get; private set; }
        public float PaidOutLength => nodes.Count * spacing + tailLimit;
        public Rigidbody ProjectileBody => tip;

        struct Node { public Rigidbody body; public ConfigurableJoint joint; }
        readonly List<Node> nodes = new List<Node>(); // tip -> hand, excludes tip
        readonly List<Transform> links = new List<Transform>();
        readonly List<Vector3> path = new List<Vector3>();
        GameObject world; Rigidbody anchor, tip; ConfigurableJoint tipJoint;
        FixedJoint latch; Collider[] ownerColliders; Mesh linkMesh; Material fallbackMaterial;
        float spacing, limit, tailLimit, clock; Vector3 previousSocket; bool initialized;
        ConfigurableJoint TailJoint => nodes.Count == 0 ? tipJoint : nodes[nodes.Count-1].joint;
        Rigidbody TailBody => nodes.Count == 0 ? tip : nodes[nodes.Count-1].body;

        public bool Throw(Vector3 worldDirection)
        {
            if (!isActiveAndEnabled || State != HookState.Ready || socket == null || worldDirection.sqrMagnitude < .001f) return false;
            spacing = Mathf.Max(.12f, segmentLength);
            // Hard safety cap limits physics body count; visible in Inspector via OnValidate.
            limit = Mathf.Clamp(maxLength, .5f, spacing * 128);
            ownerColliders = GetComponentsInChildren<Collider>();
            world = new GameObject(name + "_LiveChain"); // deliberately NOT parented to the moving hero
            anchor = MakeBody("HandAnchor", socket.position, true, 1, 0);
            previousSocket = socket.position;
            tip = MakeBody("Hook", socket.position, false, hookMass, Mathf.Max(nodeRadius, .075f));
            tip.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            if (projectileVisual != null)
            {
                GameObject visual = Instantiate(projectileVisual, tip.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                foreach (Collider c in visual.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            HookCollisionRelay relay = tip.gameObject.AddComponent<HookCollisionRelay>(); relay.owner = this;
            tailLimit = Mathf.Min(spacing, limit);
            tipJoint = Join(tip, anchor, tailLimit);
            SetVelocity(tip, worldDirection.normalized * launchSpeed);
            if (restingHook != null) restingHook.enabled = false;
            clock = 0; State = HookState.Flying; initialized = true;
            return true;
        }
        public void Retract()
        {
            if (State == HookState.Ready || State == HookState.Retracting) return;
            // Dynamic targets stay attached and are pulled; static scenery is released.
            if (latch != null && (latch.connectedBody == null || latch.connectedBody.isKinematic))
            { Destroy(latch); latch = null; }
            State = HookState.Retracting; clock = 0;
        }
        public void RegisterHit(Collision collision)
        {
            if (State != HookState.Flying || (hitMask.value & (1 << collision.gameObject.layer)) == 0) return;
            foreach (Collider c in ownerColliders) if (c == collision.collider) return;
            if (collision.transform.IsChildOf(world.transform)) return;
            Rigidbody hit = collision.rigidbody;
            latch = tip.gameObject.AddComponent<FixedJoint>();
            latch.autoConfigureConnectedAnchor = false; latch.connectedBody = hit; latch.anchor = Vector3.zero;
            latch.connectedAnchor = hit != null ? hit.transform.InverseTransformPoint(tip.position) : tip.position;
            latch.enableCollision = false;
            State = HookState.Latched; clock = 0;
            onHookHit.Invoke(collision.collider);
        }
        void FixedUpdate()
        {
            if (!initialized || socket == null || tip == null) return;
            float dt = Time.fixedDeltaTime; clock += dt;
            Vector3 socketVelocity = (socket.position - previousSocket) / dt;
            // Teleport is not physical motion: cancel rather than exploding the solver.
            if ((socket.position - previousSocket).sqrMagnitude > 16) { ResetChain(); return; }
            previousSocket = socket.position; anchor.MovePosition(socket.position);
            tip.AddForce(Physics.gravity * projectileGravity, ForceMode.Acceleration);
            foreach (Node node in nodes) node.body.AddForce(Physics.gravity * chainGravity, ForceMode.Acceleration);
            if (State == HookState.Flying)
            {
                float remaining = Mathf.Max(.005f, limit - nodes.Count * spacing);
                tailLimit = Mathf.Min(remaining, spacing * 1.8f, tailLimit + launchSpeed * dt);
                // Pay out at the hand. Existing world-space bodies retain their momentum.
                while (Vector3.Distance(TailBody.position, socket.position) > spacing * 1.08f &&
                       tailLimit > spacing * 1.08f && (nodes.Count + 1) * spacing < limit - .03f)
                {
                    Rigidbody old = TailBody; ConfigurableJoint oldJoint = TailJoint;
                    Vector3 towardHand = (socket.position - old.position).normalized;
                    Rigidbody next = MakeBody("ChainNode_" + nodes.Count, old.position + towardHand * spacing, false, segmentMass, nodeRadius);
                    SetVelocity(next, socketVelocity);
                    oldJoint.connectedBody = next; oldJoint.connectedAnchor = Vector3.zero; SetLimit(oldJoint, spacing);
                    tailLimit -= spacing;
                    nodes.Add(new Node { body = next, joint = Join(next, anchor, tailLimit) });
                }
                SetLimit(TailJoint, tailLimit);
                if ((autoRetractAtRange && PaidOutLength >= limit - .025f) || clock >= flightTimeout) Retract();
            }
            else if (State == HookState.Latched)
            {
                // Lock paid-out length. Sideways motion is transmitted through the joints.
                if (clock >= latchHoldSeconds) Retract();
            }
            else if (State == HookState.Retracting)
            {
                tailLimit = Mathf.Max(.015f, tailLimit - reelSpeed * dt); SetLimit(TailJoint, tailLimit);
                // Reel in near-hand nodes only; never teleport the projectile along a curve.
                if (nodes.Count > 0 && tailLimit < .07f && Vector3.Distance(TailBody.position, socket.position) < .12f)
                {
                    Node last = nodes[nodes.Count-1]; nodes.RemoveAt(nodes.Count-1);
                    ConfigurableJoint joint = TailJoint; joint.connectedBody = anchor; joint.connectedAnchor = Vector3.zero;
                    tailLimit += spacing; SetLimit(joint, tailLimit);
                    Destroy(last.body.gameObject);
                }
                if (nodes.Count == 0 && Vector3.Distance(tip.position, socket.position) < .16f)
                { ResetChain(); onReturned.Invoke(); return; }
                if (clock >= retractTimeout) { ResetChain(); onRetractionTimeout.Invoke(); return; }
            }
            // A kinematic player and a hook fixed to scenery cannot be moved arbitrarily apart.
            // Release the latch if the player forces an impossible span; do not stretch the chain indefinitely.
            if (latch != null && Vector3.Distance(tip.position, socket.position) > PaidOutLength * 1.10f + .2f)
            { Destroy(latch); latch = null; Retract(); }
        }
        Rigidbody MakeBody(string label, Vector3 position, bool kinematic, float mass, float radius)
        {
            GameObject go = new GameObject(label); go.transform.SetParent(world.transform, true); go.transform.position = position;
            go.layer = gameObject.layer;
            Rigidbody rb = go.AddComponent<Rigidbody>(); rb.isKinematic = kinematic; rb.useGravity = false;
            rb.mass = mass; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.solverIterations = solverIterations; rb.solverVelocityIterations = 12;
            rb.collisionDetectionMode = kinematic ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linearDrag; rb.angularDamping = .1f;
#else
            rb.drag = linearDrag; rb.angularDrag = .1f;
#endif
            if (radius > 0)
            {
                SphereCollider col = go.AddComponent<SphereCollider>(); col.radius = radius;
                foreach (Collider other in ownerColliders) if (other != null) Physics.IgnoreCollision(col, other);
                // Chain self-collision disabled for stability; all nodes still collide with the world.
                foreach (Collider other in world.GetComponentsInChildren<Collider>())
                    if (other != col) Physics.IgnoreCollision(col, other);
            }
            return rb;
        }
        ConfigurableJoint Join(Rigidbody body, Rigidbody connected, float length)
        {
            ConfigurableJoint j = body.gameObject.AddComponent<ConfigurableJoint>();
            j.autoConfigureConnectedAnchor = false; j.anchor = Vector3.zero; j.connectedAnchor = Vector3.zero; j.connectedBody = connected;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.enableCollision = false; j.enablePreprocessing = true;
            // Projection is deliberately off: it injects non-physical position corrections.
            j.projectionMode = JointProjectionMode.None;
            SetLimit(j, length); return j;
        }
        static void SetLimit(ConfigurableJoint j, float length)
        {
            j.linearLimit = new SoftJointLimit { limit = Mathf.Max(.005f,length), bounciness = 0, contactDistance = .005f };
        }
        static void SetVelocity(Rigidbody rb, Vector3 value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }
        void LateUpdate()
        {
            if (!initialized || tip == null || socket == null) return;
            if (linkMesh == null) linkMesh = MakeLinkMesh();
            path.Clear(); path.Add(socket.position);
            for (int i=nodes.Count-1;i>=0;i--) path.Add(nodes[i].body.transform.position);
            path.Add(tip.transform.position);
            float total=0; for(int i=1;i<path.Count;i++) total += Vector3.Distance(path[i-1],path[i]);
            float pitch=Mathf.Max(visualLinkPitch,total/512f);
            int count=Mathf.Min(512, Mathf.CeilToInt(total / pitch));
            while (links.Count < count)
            {
                GameObject go=new GameObject("VisibleLink"); go.transform.SetParent(world.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh=linkMesh;
                MeshRenderer mr=go.AddComponent<MeshRenderer>();
                if (chainMaterial == null && fallbackMaterial == null)
                {
                    Shader shader=Shader.Find("Universal Render Pipeline/Lit"); if(shader==null) shader=Shader.Find("Standard");
                    fallbackMaterial=new Material(shader); fallbackMaterial.color=new Color(.17f,.19f,.21f);
                }
                mr.sharedMaterial=chainMaterial != null ? chainMaterial : fallbackMaterial; links.Add(go.transform);
            }
            int edge=1; float start=0; float edgeLength=Vector3.Distance(path[0],path[1]);
            for(int i=0;i<links.Count;i++)
            {
                links[i].gameObject.SetActive(i<count); if(i>=count) continue;
                float d=Mathf.Min(total,(i+.5f)*pitch);
                while(edge<path.Count-1 && start+edgeLength<d) {start+=edgeLength;edge++;edgeLength=Vector3.Distance(path[edge-1],path[edge]);}
                Vector3 direction=path[edge]-path[edge-1];
                links[i].position=Vector3.Lerp(path[edge-1],path[edge],edgeLength>.0001f?(d-start)/edgeLength:0);
                if(direction.sqrMagnitude>.00001f) links[i].rotation=Quaternion.LookRotation(direction)*Quaternion.AngleAxis(i%2*90,Vector3.forward);
                links[i].localScale=Vector3.one*(pitch/.10f);
            }
        }
        static Mesh MakeLinkMesh()
        {
            const int n=16,m=6; var v=new Vector3[n*m];var normals=new Vector3[v.Length];var uv=new Vector2[v.Length];var t=new int[n*m*6];int k=0;
            for(int i=0;i<n;i++) for(int j=0;j<m;j++)
            {
                float a=i*Mathf.PI*2/n,b=j*Mathf.PI*2/m; Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 center=new Vector3(.035f*Mathf.Cos(a),0,.065f*Mathf.Sin(a));
                Vector3 normal=radial*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b); int q=i*m+j;
                v[q]=center+normal*.012f;normals[q]=normal;uv[q]=new Vector2((float)i/n,(float)j/m);
                int q1=i*m+(j+1)%m,q2=((i+1)%n)*m+j,q3=((i+1)%n)*m+(j+1)%m;
                t[k++]=q;t[k++]=q1;t[k++]=q2;t[k++]=q1;t[k++]=q3;t[k++]=q2;
            }
            Mesh mesh=new Mesh {name="Runtime_ChainLink",vertices=v,normals=normals,uv=uv,triangles=t};mesh.RecalculateBounds();return mesh;
        }
        public void ResetChain()
        {
            initialized=false;State=HookState.Ready;
            if(world!=null) Destroy(world);
            world=null;tip=null;anchor=null;latch=null;nodes.Clear();links.Clear();tailLimit=0;
            if(restingHook!=null) restingHook.enabled=true;
        }
        void OnDisable(){ ResetChain(); }
        void OnDestroy(){ if(linkMesh!=null) Destroy(linkMesh);if(fallbackMaterial!=null)Destroy(fallbackMaterial); }
        void OnValidate(){ segmentLength=Mathf.Max(.12f,segmentLength);maxLength=Mathf.Clamp(maxLength,.5f,segmentLength*128); }
    }
}
