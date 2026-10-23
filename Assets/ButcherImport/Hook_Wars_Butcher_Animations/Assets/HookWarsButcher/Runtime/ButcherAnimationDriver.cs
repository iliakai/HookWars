using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HookWars.Butcher
{
    public sealed class ButcherAnimationDriver : MonoBehaviour
    {
        public Animator animator;
        public Transform bodyRoll, axeHit;
        public PhysicalHookChain hook;
        [Tooltip("Keeps roll angle continuous when changing direction. Disable to view the 8 authored clips instead.")]
        public bool rollByTravelledDistance = true;
        [Min(.01f)] public float sphereRadius = 1.04f;
        [Min(.05f)] public float axeHitRadius = .42f;
        public LayerMask axeHitMask = ~0;
        public UnityEvent<Collider> onAxeHit = new UnityEvent<Collider>();
        public UnityEvent onAxeImpact = new UnityEvent();
        public bool Busy { get; private set; }
        Vector3 previousPosition; Quaternion worldRoll; Coroutine actionRoutine;
        readonly Collider[] hits = new Collider[64];
        void Start()
        {
            previousPosition=transform.position;
            if(bodyRoll!=null) worldRoll=bodyRoll.rotation;
            if(animator!=null) animator.applyRootMotion=false;
        }
        void Update()
        {
            Vector3 travel=transform.position-previousPosition;travel.y=0;
            Vector3 velocity=travel/Mathf.Max(.0001f,Time.deltaTime);
            Vector3 local=transform.InverseTransformDirection(velocity);
            if(animator!=null)
            {
                Vector3 d=local.sqrMagnitude>.0001f?local.normalized:Vector3.zero;
                animator.SetFloat("MoveX",d.x);animator.SetFloat("MoveY",d.z);
                animator.SetFloat("RollRate",velocity.magnitude/(2*Mathf.PI*sphereRadius));
            }
        }
        void LateUpdate()
        {
            Vector3 travel=transform.position-previousPosition;travel.y=0;previousPosition=transform.position;
            if(bodyRoll==null)return;
            if(rollByTravelledDistance)
            {
                if(travel.sqrMagnitude<16 && travel.sqrMagnitude>.0000001f)
                    worldRoll=Quaternion.AngleAxis(travel.magnitude/sphereRadius*Mathf.Rad2Deg,Vector3.Cross(Vector3.up,travel).normalized)*worldRoll;
                bodyRoll.rotation=worldRoll;
            }
            else worldRoll=bodyRoll.rotation;
        }
        public bool Attack()
        {
            if(Busy || animator==null)return false;
            actionRoutine=StartCoroutine(AxeRoutine());return true;
        }
        IEnumerator AxeRoutine()
        {
            Busy=true;animator.Play("Axe_Attack",1,0);
            yield return new WaitForSeconds(.4f);
            onAxeImpact.Invoke();
            if(axeHit!=null)
            {
                int count=Physics.OverlapSphereNonAlloc(axeHit.position,axeHitRadius,hits,axeHitMask,QueryTriggerInteraction.Ignore);
                var seen=new HashSet<int>();
                for(int i=0;i<count;i++)
                {
                    Collider c=hits[i];if(c.transform.IsChildOf(transform))continue;
                    int id=c.attachedRigidbody!=null?c.attachedRigidbody.GetInstanceID():c.GetInstanceID();
                    if(seen.Add(id))onAxeHit.Invoke(c);
                }
            }
            yield return new WaitForSeconds(.4f);Busy=false;actionRoutine=null;
        }
        public bool CastHook(Vector3 worldDirection)
        {
            if(Busy || animator==null || hook==null || hook.State!=PhysicalHookChain.HookState.Ready || worldDirection.sqrMagnitude<.001f)return false;
            actionRoutine=StartCoroutine(HookRoutine(worldDirection.normalized));return true;
        }
        IEnumerator HookRoutine(Vector3 direction)
        {
            Busy=true;animator.Play("Hook_Cast",2,0);
            yield return new WaitForSeconds(7f/30f);
            hook.Throw(direction); // trajectory is chosen at button press, not recomputed every frame
            yield return new WaitForSeconds(11f/30f);Busy=false;actionRoutine=null;
        }
        void OnDisable(){if(actionRoutine!=null)StopCoroutine(actionRoutine);actionRoutine=null;Busy=false;}
        void OnDrawGizmosSelected(){if(axeHit!=null){Gizmos.color=Color.red;Gizmos.DrawWireSphere(axeHit.position,axeHitRadius);}}
    }
}
