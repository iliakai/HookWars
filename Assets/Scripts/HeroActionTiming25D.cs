using System;
using UnityEngine;

// Optional timing adapter on Arena25D logical heroes. The callbacks remain the
// existing Physics2D attack/throw implementations; Animator never searches hits.
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(HookVictim))]
public sealed class HeroActionTiming25D : MonoBehaviour
{
    public const float AxeLength = 0.8f, AxeImpact = 0.4f;
    public const float CastLength = 0.6f, HookRelease = 7f / 30f;
    private Health health;
    private HookVictim victim;
    private Action axeImpact, hookRelease;
    public int AxeSequence { get; private set; }
    public int HookSequence { get; private set; }
    public int LifeSequence { get; private set; }
    public float AxeStarted { get; private set; }
    public float HookStarted { get; private set; }
    public float AxeSpeed { get; private set; } = 1;
    public Vector2 HookDirection { get; private set; }
    public bool HookPending => hookRelease != null;
    public bool AxePending => axeImpact != null;
    public bool AxeAnimating => CanAct && Time.time < AxeStarted + AxeLength / AxeSpeed && AxeSequence > 0;
    public bool HookAnimating => CanAct && Time.time < HookStarted + CastLength && HookSequence > 0;
    public bool CanAct => isActiveAndEnabled && health != null && !health.IsDead && victim != null && !victim.IsCaught;
    public bool FacingLocked => HookAnimating;
    public Vector2 ResolveFacing(Vector2 direction) => FacingLocked ? HookDirection : direction;
    private void Awake() { health = GetComponent<Health>(); victim = GetComponent<HookVictim>(); }
    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (victim == null) victim = GetComponent<HookVictim>();
        health.Died += OnDied; health.Revived += OnRevived;
        victim.Captured += CancelAll;
    }
    public bool BeginAxe(float cooldown, Action impact)
    {
        if (MatchPause.BlocksGameplay || !CanAct || AxePending || HookPending || impact == null) return false;
        AxeSpeed = Mathf.Max(1f, AxeLength / Mathf.Max(0.1f, cooldown));
        AxeStarted = Time.time; AxeSequence++; axeImpact = impact; return true;
    }
    public bool BeginHook(Vector2 direction, Action release)
    {
        if (MatchPause.BlocksGameplay || !CanAct || HookPending || release == null) return false;
        HookDirection = direction.normalized; HookStarted = Time.time;
        HookSequence++; hookRelease = release; return true;
    }
    private void LateUpdate() => Advance(Time.time);
    public void Advance(float now)
    {
        if (!CanAct) { CancelAll(); return; }
        if(MatchPause.BlocksGameplay)return;
        if (axeImpact != null && now >= AxeStarted + AxeImpact / AxeSpeed)
        { Action hit = axeImpact; axeImpact = null; hit(); }
        if (hookRelease != null && now >= HookStarted + HookRelease)
        { Action release = hookRelease; hookRelease = null; release(); }
    }
    public void CancelHook() { hookRelease = null; HookStarted = float.NegativeInfinity; }
    public void CancelAxe() { axeImpact = null; AxeStarted = float.NegativeInfinity; }
    public void CancelAll()
    {
        axeImpact = null; hookRelease = null;
        AxeStarted = HookStarted = float.NegativeInfinity;
    }
    private void OnDied(Health _) => CancelAll();
    private void OnRevived(Health _) { CancelAll(); LifeSequence++; }
    private void OnDisable()
    {
        CancelAll();
        if (health != null) { health.Died -= OnDied; health.Revived -= OnRevived; }
        if (victim != null) victim.Captured -= CancelAll;
    }
}
