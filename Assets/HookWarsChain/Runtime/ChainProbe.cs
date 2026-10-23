using UnityEngine;
namespace HookWars.Chain
{
 // Optional diagnostics: attach to a chain owner in your own Play Mode tests.
 public sealed class ChainProbe : MonoBehaviour
 {
  public PhysicalChain chain;
  public float maxObservedAnchorError;
  public bool invalidPosition;
  void FixedUpdate()
  {
   if(chain==null)return;
   foreach(var rb in chain.Links)
   {
    Vector3 p=rb.position;
    if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))invalidPosition=true;
    foreach(var j in rb.GetComponents<ConfigurableJoint>())
    {
     if(j.connectedBody==null)continue;
     if(j.xMotion==ConfigurableJointMotion.Free&&j.yMotion==ConfigurableJointMotion.Free&&j.zMotion==ConfigurableJointMotion.Free)continue;
     float error=Vector3.Distance(rb.position+rb.rotation*j.anchor,j.connectedBody.position+j.connectedBody.rotation*j.connectedAnchor);
     maxObservedAnchorError=Mathf.Max(maxObservedAnchorError,error);
    }
   }
  }
 }
}
