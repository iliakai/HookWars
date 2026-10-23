using UnityEngine;

// Uses authored animation clips only. SkinOrientation is an extra, unanimated
// skin bone AFTER BodyRoll: it changes the orientation basis on turns/stops,
// never generates rolling angles or overwrites the animated BodyRoll bone.
[DefaultExecutionOrder(1100)]
public sealed class ButcherVisual25D : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform bodyRoll, skinOrientation, hookSocket, visualRig;
    [SerializeField] private Renderer restingHook;
    [SerializeField] private SkinnedMeshRenderer bodyMesh;
    [SerializeField] private float effectiveRadius = 0.4992f;
    [SerializeField] private float maximumVoluntarySpeed = 8f;
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    private Health hero;
    private HookVictim victim;
    private HookAbility hook;
    private HeroActionTiming25D timing;
    private Vector3 previousPosition;
    private Quaternion previousFacing, ballWorld;
    private bool positioned, wasAxe, wasHook;
    private int direction = -1, life = -1;
    private ArenaPresentation25D presentation;
    private Transform axeSizeRoot;
    private Vector3 initialAxeScale;
    public Transform Socket => hookSocket;
    public Health Owner => hero;
    public Transform BodyRoll => bodyRoll;
    public Transform SkinOrientation => skinOrientation;
    public Animator Animator => animator;
    public float EffectiveRadius => effectiveRadius;
    public int DirectionIndex => direction;
    public float RollRate { get; private set; }
    public void Bind(Health owner)
    {
        hero = owner; victim = owner.GetComponent<HookVictim>(); hook = owner.GetComponent<HookAbility>();
        timing = owner.GetComponent<HeroActionTiming25D>();
        presentation = GetComponentInParent<ArenaPresentation25D>();
        // An unanimated skin bone scales the two actual axe meshes together
        // around AxePivot; the animated pivot and logical hit range stay separate.
        var pivot=transform.Find("RigVisual/ButcherRig/Root/AxePivot");
        if(pivot!=null)
        {
            axeSizeRoot=pivot.Find("AxeVisualScale");
            if(axeSizeRoot==null){axeSizeRoot=new GameObject("AxeVisualScale").transform;axeSizeRoot.SetParent(pivot,false);}
            initialAxeScale=Vector3.one;
            foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.name.StartsWith("05_Axe")||skin.name.StartsWith("06_Axe"))
                {var bones=skin.bones;for(int i=0;i<bones.Length;i++)if(bones[i]==pivot)bones[i]=axeSizeRoot;skin.bones=bones;}
        }
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = false; // One controlled evaluation, after XY -> XZ presentation.
        ResetPose();
    }
    public static int LocalDirection(Vector3 localTravel)
    {
        int octant = Mathf.RoundToInt(Mathf.Atan2(localTravel.x, localTravel.z) * Mathf.Rad2Deg / 45f);
        return (octant + 8) % 8;
    }
    public void ResetPose()
    {
        animator.Rebind(); animator.SetFloat("RollRate", 0); animator.SetFloat("AxeSpeed", 1);
        animator.Play("Idle.Idle", 0, 0); animator.Play("Body.Roll_N", 1, 0);
        animator.Play("Axe.Rest", 2, 0); animator.Play("Hook.Rest", 3, 0);
        skinOrientation.localRotation = Quaternion.identity; animator.Update(0);
        direction = 0; positioned = false; wasAxe = wasHook = false;
        life = timing != null ? timing.LifeSequence : 0; ballWorld = skinOrientation.rotation;
        previousFacing = transform.rotation;
        if (restingHook != null) restingHook.enabled = true;
    }
    private void LateUpdate() => Tick(Time.deltaTime);
    public void Tick(float deltaTime)
    {
        if (hero == null || hero.IsDead) return;
        if (timing != null && life != timing.LifeSequence) ResetPose();
        bool firstPosition = !positioned;
        Vector3 position = transform.position;
        Vector3 travel = positioned ? position - previousPosition : Vector3.zero; travel.y = 0;
        previousPosition = position; positioned = true;
        if (firstPosition) { previousFacing = transform.rotation; ballWorld = skinOrientation.rotation; }
        float speed = deltaTime > 0 ? travel.magnitude / deltaTime : 0;
        float allowedSpeed=Mathf.Max(maximumVoluntarySpeed,(hero.GetComponent<PlayerMovement>()?.MoveSpeed??0)*1.5f);
        bool ordinary = victim != null && !victim.IsCaught && travel.magnitude < 2f && speed <= allowedSpeed;
        RollRate = ordinary && speed > 0.01f ? speed / (2f * Mathf.PI * Mathf.Max(0.01f, effectiveRadius)) : 0;
        if (Quaternion.Angle(previousFacing, transform.rotation) > 0.0001f) skinOrientation.rotation = ballWorld;
        previousFacing = transform.rotation;
        if (ordinary && speed > 0.01f)
        {
            int selected = LocalDirection(transform.InverseTransformDirection(travel));
            if (selected != direction)
            {
                Quaternion keep = skinOrientation.rotation;
                float phase = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(1).normalizedTime, 1);
                animator.Play("Body.Roll_" + Directions[selected], 1, phase); animator.Update(0);
                skinOrientation.rotation = keep; direction = selected;
            }
        }
        animator.SetFloat("RollRate", RollRate);
        animator.SetFloat("AxeSpeed", timing != null ? timing.AxeSpeed : 1);
        bool axe = timing != null && timing.AxeAnimating, cast = timing != null && timing.HookAnimating;
        if (!axe && wasAxe) animator.CrossFadeInFixedTime("Axe.Rest", 0.06f, 2);
        if (!cast && wasHook) animator.CrossFadeInFixedTime("Hook.Rest", 0.06f, 3);
        animator.Update(Mathf.Max(0, deltaTime));
        // Clock synchronization evaluates the accepted action at its actual elapsed
        // time (not frame zero); no AnimationEvents or hit searches exist here.
        if (axe) animator.Play("Axe.Axe_Attack", 2, Mathf.Clamp01((Time.time - timing.AxeStarted) * timing.AxeSpeed / HeroActionTiming25D.AxeLength));
        if (cast) animator.Play("Hook.Hook_Cast", 3, Mathf.Clamp01((Time.time - timing.HookStarted) / HeroActionTiming25D.CastLength));
        if (axe || cast) animator.Update(0);
        wasAxe = axe; wasHook = cast; ballWorld = skinOrientation.rotation;
        if (restingHook != null) restingHook.enabled = hook == null || hook.ActiveHook == null;
        if(axeSizeRoot!=null)axeSizeRoot.localScale=initialAxeScale*(hero.GetComponent<PlayerMelee>()?.VisualSize??1);
        if (presentation != null) presentation.UpdateHookPresentation(hook, this);
    }
    private void OnDisable() { positioned = false; if (restingHook != null) restingHook.enabled = true; }
}
