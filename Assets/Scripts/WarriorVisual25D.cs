using UnityEngine;

// Reads gameplay state only. Animation never awards damage or resources.
[DefaultExecutionOrder(1200)]
public sealed class WarriorVisual25D : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform weapon;
    [SerializeField] private float eastSign=1;
    private SausageWarrior source;
    private int serial=-1;
    private bool attacking;
    private Vector3 weaponScale;
    private Quaternion weaponRotation;
    private MaterialPropertyBlock properties;
    public void Bind(SausageWarrior warrior)
    {
        source=warrior;properties=new MaterialPropertyBlock();weaponScale=weapon.localScale;weaponRotation=weapon.localRotation;
        foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.localBounds=new Bounds(skin.localBounds.center,Vector3.one*8*Mathf.Max(1,source.Stats.ClubLength,source.Stats.ClubWidth));
        foreach(var renderer in GetComponentsInChildren<Renderer>())
        {
            for(int i=0;i<renderer.sharedMaterials.Length;i++)
            {
                var material=renderer.sharedMaterials[i];if(material==null)continue;
                Color? color=renderer.name.Contains("Femur_Club")?source.Stats.ClubColor:material.name.StartsWith("Sausage_Tintable")?source.Stats.BodyColor:null;
                if(color.HasValue){renderer.GetPropertyBlock(properties,i);properties.SetColor("_BaseColor",color.Value);renderer.SetPropertyBlock(properties,i);properties.Clear();}
            }
        }
    }
    private void LateUpdate()
    {
        if(source==null||animator==null)return;
        if(source.Attacking)
        {
            if(serial!=source.AttackSerial){serial=source.AttackSerial;animator.Play("Attack_Bone",0,source.AttackPhase);}
            animator.SetFloat("AttackRate",1.2f/source.Stats.AttackPeriod);attacking=true;
        }
        else
        {
            if(attacking){animator.Play("Idle",0,0);attacking=false;}
            Vector3 velocity=ArenaSpace25D.ToWorld(source.Velocity);
            Vector3 local=transform.InverseTransformDirection(velocity);
            animator.SetFloat("MoveX",local.x*eastSign);animator.SetFloat("MoveY",local.z);
            animator.SetFloat("Speed",source.Velocity.magnitude);
            animator.SetFloat("WalkRate",source.Velocity.magnitude/(.84f/2.55f*source.Stats.BodyScale/1.5f));
        }
        // Bone scale runs after Animator, so animation curves cannot undo upgrades.
        weapon.localScale=Vector3.Scale(weaponScale,new Vector3(source.Stats.ClubWidth,source.Stats.ClubLength,source.Stats.ClubWidth));
        weapon.localRotation=weaponRotation;
        // Extend the club outward instead of burying its enlarged tip in the
        // ground. The grip stays in the hand and the authored vertical reach
        // remains unchanged; raised attack poses keep their original direction.
        if(source.Stats.ClubLength>1&&weapon.up.y<0)
        {
            Vector3 original=weapon.up;Vector3 horizontal=Vector3.ProjectOnPlane(original,Vector3.up).normalized;
            if(horizontal.sqrMagnitude<.001f)horizontal=transform.forward;
            float vertical=original.y/source.Stats.ClubLength;
            Vector3 direction=horizontal*Mathf.Sqrt(1-vertical*vertical)+Vector3.up*vertical;
            weapon.rotation=Quaternion.FromToRotation(original,direction)*weapon.rotation;
        }
    }
}
