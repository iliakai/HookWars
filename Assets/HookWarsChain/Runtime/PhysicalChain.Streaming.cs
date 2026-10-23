using System;
using System.Collections.Generic;
using UnityEngine;

namespace HookWars.Chain
{
    // Adaptation of the package: pooled payout at the start mount. Existing
    // dynamic links retain both their pose and velocity as link count changes.
    public sealed partial class PhysicalChain
    {
        private bool streaming;
        private GameObject streamRoot;
        private PhysicsMaterial streamContact;
        private Rigidbody streamStart,streamEnd;
        private ConfigurableJoint handJoint;
        private readonly List<Rigidbody> streamPool=new List<Rigidbody>();
        private readonly List<Rigidbody> streamBodies=new List<Rigidbody>();
        private readonly List<ConfigurableJoint> streamJoints=new List<ConfigurableJoint>();
        private readonly List<ConfigurableJoint> payoutJoints=new List<ConfigurableJoint>();
        private Vector3 requestedStart,requestedEnd;
        private Vector3 previousStart,previousEnd;
        private float issuedLength;
        private float previousIssuedLength,currentIssuedLength;
        public float IssuedLength=>issuedLength;
        public int PooledCount=>streamPool.Count;
        public int ActiveBodyCount=>streamBodies.Count;
        public float MountTravel=>streaming&&streamStart!=null?Mathf.Max(Vector3.Distance(previousStart,requestedStart),Vector3.Distance(previousEnd,requestedEnd)):0;
        public float MaximumAnchorError {get;private set;}
        public int environmentLayer=25;
        public float visualThickness=1;
        public void SetStreamingEndpoints(Vector3 start,Vector3 end,float metres)
        {
            if(!IsFinite(metres)||metres<0)throw new ArgumentOutOfRangeException(nameof(metres));
            if(Vector3.Distance(start,end)>metres+.0001f)throw new InvalidOperationException("Streaming endpoints exceed issued physical length");
            if(!streaming && streamStart!=null)
            {
                // A new throw/guide reuses retired objects, not old endpoint poses.
                // Moving inactive kinematic mounts here prevents a 20m sweep from
                // the end of the previous throw through the newly issued chain.
                streamStart.position=start;streamEnd.position=end;
                previousStart=start;previousEnd=end;MaximumAnchorError=0;
            }
            buildOnStart=false;streaming=true;requestedStart=start;requestedEnd=end;issuedLength=metres;
        }
        private void EnsureStream()
        {
            if(streamRoot!=null)return;
            streamRoot=new GameObject(name+" pooled simulation");
            // Owned by the match, detached from rotating/scaling visual roots.
            ChainPhysicsWorld25D.Instance.Own(streamRoot);ChainPhysicsWorld25D.Instance.Register(this);
            streamStart=Mount("Hand mount",requestedStart);streamEnd=Mount("Hook mount",requestedEnd);
            streamContact=new PhysicsMaterial("Chain sliding contact"){dynamicFriction=.05f,staticFriction=.05f,frictionCombine=PhysicsMaterialCombine.Minimum};
            previousStart=requestedStart;previousEnd=requestedEnd;
            if(linkMesh==null)throw new InvalidOperationException("Assign imported ChainLink mesh");
        }
        private Rigidbody Mount(string label,Vector3 position)
        {
            var go=new GameObject(label);go.transform.SetParent(streamRoot.transform,false);go.transform.position=position;
            var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.interpolation=RigidbodyInterpolation.Interpolate;return rb;
        }
        private void ConfigureDistance(ConfigurableJoint joint)
        {
            joint.autoConfigureConnectedAnchor=false;joint.anchor=joint.connectedAnchor=Vector3.zero;
            joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
            joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Free;
            joint.enableCollision=false;joint.enablePreprocessing=false;
            joint.projectionMode=JointProjectionMode.None;
            joint.connectedMassScale=1;
        }
        private Rigidbody Acquire(int index,Vector3 position,Vector3 velocity,Quaternion orientation)
        {
            if(index==streamPool.Count)
            {
                var go=new GameObject("Link "+index.ToString("D4"));go.layer=physicsLayer;go.transform.SetParent(streamRoot.transform,false);
                var meshObject=new GameObject("Metal");meshObject.transform.SetParent(go.transform,false);
                meshObject.transform.localScale=new Vector3(linkScale*visualThickness,linkScale*visualThickness,linkScale);
                meshObject.AddComponent<MeshFilter>().sharedMesh=linkMesh;meshObject.AddComponent<MeshRenderer>().sharedMaterial=material;
                var rb=go.AddComponent<Rigidbody>();rb.mass=massPerLink;rb.solverIterations=solverIterations;rb.solverVelocityIterations=8;
                rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                rb.maxAngularVelocity=25;rb.maxDepenetrationVelocity=2;rb.linearDamping=.15f;rb.angularDamping=.2f;
                var collider=go.AddComponent<CapsuleCollider>();collider.direction=2;collider.height=.24f*linkScale;collider.radius=.04f*linkScale*visualThickness;collider.contactOffset=.001f;
                collider.sharedMaterial=streamContact;
                // A single envelope replaces the original 16-capsule wire collider.
                // Only chain-environment proxies collide; no owner/cargo/link contacts.
                collider.excludeLayers=~(1<<environmentLayer);collider.includeLayers=1<<environmentLayer;
                var joint=go.AddComponent<ConfigurableJoint>();ConfigureDistance(joint);
                var payout=go.AddComponent<ConfigurableJoint>();ConfigureDistance(payout);payout.connectedBody=streamStart;
                payout.xMotion=payout.yMotion=payout.zMotion=ConfigurableJointMotion.Free;payoutJoints.Add(payout);
                streamPool.Add(rb);streamJoints.Add(joint);
            }
            Rigidbody body=streamPool[index];body.gameObject.SetActive(true);body.position=position;body.rotation=orientation;
            body.linearVelocity=velocity;body.angularVelocity=Vector3.zero;body.WakeUp();return body;
        }
        private void StepStreaming()
        {
            if(issuedLength<.003f){StopStreaming();return;}
            EnsureStream();
            previousStart=streamStart.position;previousEnd=streamEnd.position;
            previousIssuedLength=currentIssuedLength;
        }
        private void UpdateStreaming(float metres,Vector3 start,Vector3 end)
        {
            float pitch=ChainGeometry.Pitch*linkScale;
            int desired=Mathf.Max(1,Mathf.CeilToInt(metres/pitch));
            // No maximumLinks truncation. Gameplay range belongs to HookAbility.
            bool topologyChanged=desired!=streamBodies.Count;
            if(topologyChanged&&handJoint!=null){handJoint.xMotion=handJoint.yMotion=handJoint.zMotion=ConfigurableJointMotion.Free;}
            while(streamBodies.Count>desired)
            {
                int i=streamBodies.Count-1;streamPool[i].gameObject.SetActive(false);streamBodies.RemoveAt(i);
            }
            while(streamBodies.Count<desired)
            {
                int i=streamBodies.Count;
                Vector3 velocity=i>0?streamBodies[i-1].linearVelocity:Vector3.zero;
                Vector3 axis=end-start;if(axis.sqrMagnitude<.00001f)axis=Vector3.forward;
                Quaternion orientation=i==0?Quaternion.LookRotation(axis.normalized):streamBodies[i-1].rotation*Quaternion.AngleAxis(90,Vector3.forward);
                Vector3 position=i==0?end-orientation*Vector3.forward*pitch*.5f:streamBodies[i-1].position-orientation*Vector3.forward*pitch;
                var rb=Acquire(i,position,velocity,orientation);
                var joint=streamJoints[i];joint.connectedBody=i==0?streamEnd:streamBodies[i-1];
                joint.anchor=Vector3.forward*pitch*.5f;
                joint.connectedAnchor=i==0?Vector3.zero:Vector3.back*pitch*.5f;
                streamBodies.Add(rb);
            }
            // Locked coincident anchors make each physical link an actual rod;
            // the hand attachment slides through the final, partially issued link.
            if(topologyChanged)
            {
                handJoint=payoutJoints[streamBodies.Count-1];handJoint.connectedBody=streamStart;
                handJoint.xMotion=handJoint.yMotion=handJoint.zMotion=ConfigurableJointMotion.Locked;
            }
            float remainder=metres-(streamBodies.Count-.5f)*pitch;
            handJoint.anchor=Vector3.back*remainder;handJoint.connectedAnchor=Vector3.zero;
            LinkCount=streamBodies.Count;ActualLength=LinkCount*pitch;
        }
        public void MeasureStreamError()
        {
            if(!streaming)return;
            foreach(var joint in streamJoints)
                if(joint.gameObject.activeInHierarchy&&joint.connectedBody!=null)
                {
                    var body=joint.GetComponent<Rigidbody>();
                    float distance=Vector3.Distance(body.position+body.rotation*joint.anchor,joint.connectedBody.position+joint.connectedBody.rotation*joint.connectedAnchor);
                    MaximumAnchorError=Mathf.Max(MaximumAnchorError,distance);
                }
        }
        public void StreamSubstep(float fraction)
        {
            if(!streaming||streamStart==null)return;
            Vector3 start=Vector3.Lerp(previousStart,requestedStart,fraction),end=Vector3.Lerp(previousEnd,requestedEnd,fraction);
            // Length and mounts advance together. Shortening to the final length
            // before moving the old mounts made a temporarily impossible chain.
            currentIssuedLength=Mathf.Max(Vector3.Distance(start,end),Mathf.Lerp(previousIssuedLength,issuedLength,fraction));
            UpdateStreaming(currentIssuedLength,start,end);
            streamStart.MovePosition(start);streamEnd.MovePosition(end);
        }
        public void StopStreaming()
        {
            streaming=false;issuedLength=previousIssuedLength=currentIssuedLength=0;LinkCount=0;ActualLength=0;
            foreach(var body in streamPool)if(body!=null)body.gameObject.SetActive(false);
            streamBodies.Clear();
            if(handJoint!=null){handJoint.connectedBody=null;handJoint.xMotion=handJoint.yMotion=handJoint.zMotion=ConfigurableJointMotion.Free;}
        }
        private void DisposeStreaming(){StopStreaming();if(streamRoot!=null)Destroy(streamRoot);if(streamContact!=null)Destroy(streamContact);streamPool.Clear();streamJoints.Clear();payoutJoints.Clear();}
    }
}
