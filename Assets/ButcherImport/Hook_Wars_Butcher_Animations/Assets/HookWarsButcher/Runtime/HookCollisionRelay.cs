using System;
using UnityEngine;
namespace HookWars.Butcher
{
    public sealed class HookCollisionRelay : MonoBehaviour
    {
        [NonSerialized] public PhysicalHookChain owner;
        void OnCollisionEnter(Collision collision)
        {
            if(owner != null) owner.RegisterHit(collision);
        }
    }
}
