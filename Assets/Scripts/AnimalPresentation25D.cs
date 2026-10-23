using HookWars.Animals;
using UnityEngine;

[DefaultExecutionOrder(1100)]
public sealed class AnimalPresentation25D : MonoBehaviour
{
    private Animal source;
    private HookVictim victim;
    private AnimalMotion motion;
    public void Bind(Animal animal)
    {
        source=animal; victim=animal.GetComponent<HookVictim>(); motion=GetComponent<AnimalMotion>();
        motion.automaticSpeed=false;
    }
    private void LateUpdate()
    {
        if(source==null||motion==null)return;
        float speed=source.IsDead||victim.IsCaught?0:source.VoluntaryVelocity.magnitude;
        motion.SetSpeed(speed);
        if(speed>.01f) transform.parent.rotation=ArenaSpace25D.Facing(source.VoluntaryVelocity);
        // During capture no walking or grazing: hold the current visual pose.
        motion.enabled=!victim.IsCaught&&!source.IsDead;
        motion.animator.enabled=motion.enabled;
    }
}
