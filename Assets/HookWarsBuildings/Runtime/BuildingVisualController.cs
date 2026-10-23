using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HookWars.Buildings
{
    public enum BuildingKind { AnimalFarm, BoneWall, Factory, MeatShop }
    public sealed class BuildingVisualController : MonoBehaviour
    {
        public BuildingKind kind;
        public Animator animator;
        public ParticleSystem[] effects;
        public Light[] workingLights;
        public bool activeOnStart = true;
        [Min(0)] public float transitionSeconds = .45f;
        [Min(.05f)] public float playbackSpeed = 1;
        public bool randomizeAmbientPhase = true;
        public UnityEvent<bool> stateChanged = new UnityEvent<bool>();
        public bool IsActive { get; private set; }
        Coroutine transition;
        bool started;
        string ActiveClip => kind == BuildingKind.AnimalFarm ? "Farm_Loop" :
                             kind == BuildingKind.BoneWall ? "Wall_Ambient" :
                             kind == BuildingKind.Factory ? "Factory_Work" : "Shop_OpenLoop";
        string ClosedClip => kind == BuildingKind.Factory ? "Factory_Closed" : "Shop_Closed";
        bool HasClosed => kind == BuildingKind.Factory || kind == BuildingKind.MeatShop;

        void Start() { started = true; Apply(activeOnStart, true); }
        void OnEnable() { if (started) Apply(activeOnStart, true); }
        void OnDisable()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            if (animator != null) animator.speed = 0;
            StopEffects();
        }
        public void SetWorking(bool working) { SetActiveMode(working); }
        public void SetOpen(bool open) { SetActiveMode(open); }
        public void SetActiveMode(bool active)
        {
            activeOnStart = active;
            if (started && isActiveAndEnabled) Apply(active, false);
        }
        public void Toggle() { SetActiveMode(!activeOnStart); }
        void Apply(bool active, bool immediate)
        {
            if (animator == null) return;
            if (!immediate && active == IsActive) return;
            if (transition != null) StopCoroutine(transition);
            transition = null;
            IsActive = active;
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.speed = playbackSpeed;
            if (active)
            {
                float phase = !HasClosed && randomizeAmbientPhase ? Random.value : 0;
                if (immediate) animator.Play("Base Layer." + ActiveClip, 0, phase);
                else animator.CrossFadeInFixedTime("Base Layer." + ActiveClip, transitionSeconds, 0, 0);
                if (effects != null) foreach (var p in effects) if (p != null) p.Play(true);
                if (workingLights != null) foreach (var l in workingLights) if (l != null) l.enabled = true;
            }
            else
            {
                StopEffects();
                if (!HasClosed) { animator.speed = 0; }
                else if (immediate || transitionSeconds <= 0)
                {
                    animator.Play("Base Layer." + ClosedClip, 0, 0);
                    animator.Update(0); animator.speed = 0;
                }
                else
                {
                    animator.CrossFadeInFixedTime("Base Layer." + ClosedClip, transitionSeconds, 0, 0);
                    transition = StartCoroutine(FreezeClosed());
                }
            }
            stateChanged.Invoke(active);
        }
        IEnumerator FreezeClosed()
        {
            // Wait in game time, matching Animator's default update mode.
            yield return new WaitForSeconds(transitionSeconds / Mathf.Max(.05f, playbackSpeed) + .05f);
            if (!IsActive)
            {
                animator.Play("Base Layer." + ClosedClip, 0, 0);
                animator.Update(0); animator.speed = 0;
            }
            transition = null;
        }
        void StopEffects()
        {
            if (effects != null) foreach (var p in effects)
                if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (workingLights != null) foreach (var l in workingLights) if (l != null) l.enabled = false;
        }
    }
}
