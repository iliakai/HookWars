using UnityEngine;
namespace HookWars.Animals
{
    [DefaultExecutionOrder(1200)]
    public sealed class AnimalMotion : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Read actual world movement. Turn off to call SetSpeed from your movement system.")]
        public bool automaticSpeed = true;
        [Min(.01f)] public float referenceWalkSpeed = .54f;
        [Min(0)] public float startThreshold = .045f;
        [Min(0)] public float stopThreshold = .02f;
        [Min(0)] public float stopDelay = .22f;
        [Min(0)] public float transitionSeconds = .25f;
        [Tooltip("Larger single-frame movement is treated as a teleport.")]
        public float teleportDistance = 2f;
        public bool IsWalking { get; private set; }
        Vector3 previous;
        float requestedSpeed, stillTime;
        bool initialized;
        static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
        static readonly int Graze = Animator.StringToHash("Base Layer.Graze");
        static readonly int Rate = Animator.StringToHash("WalkRate");
        public void SetSpeed(float metresPerSecond) { requestedSpeed = Mathf.Max(0, metresPerSecond); }
        void OnEnable() { previous = transform.position; initialized = false; stillTime = 0; }
        void LateUpdate()
        {
            Vector3 displacement = transform.position - previous; previous = transform.position;
            displacement.y = 0;
            if (animator == null || Time.deltaTime <= 0) return;
            float speed = automaticSpeed ? displacement.magnitude / Time.deltaTime : requestedSpeed;
            if (automaticSpeed && displacement.magnitude > teleportDistance) speed = 0;
            if (!initialized)
            {
                initialized = true; IsWalking = speed > startThreshold;
                animator.applyRootMotion = false;
                animator.Play(IsWalking ? Walk : Graze, 0, IsWalking ? 0 : Random.value);
            }
            bool next = IsWalking;
            if (speed > startThreshold) { next = true; stillTime = 0; }
            else if (speed < stopThreshold)
            {
                stillTime += Time.deltaTime;
                if (stillTime >= stopDelay) next = false;
            }
            else stillTime = 0;
            if (speed < stopThreshold) animator.SetFloat(Rate, 0);
            else animator.SetFloat(Rate, Mathf.Clamp(speed / Mathf.Max(.01f, referenceWalkSpeed), 0, 3f), .10f, Time.deltaTime);
            if (next != IsWalking)
            {
                IsWalking = next;
                animator.CrossFadeInFixedTime(next ? Walk : Graze, transitionSeconds, 0, 0);
            }
        }
    }
}
