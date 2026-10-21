using UnityEngine;

// A carcass keeps its identity and mass while stored, dropped or hooked.
[RequireComponent(typeof(HookVictim), typeof(Collider2D))]
[DisallowMultipleComponent]
public class Carcass : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float massKg = 6f;
    private HookVictim sourceVictim;
    private Health sourceHealth;
    private CarcassCarrier carrier;

    public float MassKg => massKg;
    public bool IsStored => carrier != null;
    public bool IsReserved => GetComponent<HookVictim>().IsCaught ||
        (sourceVictim != null && sourceHealth != null && sourceHealth.IsDead && sourceVictim.IsCaught);
    public bool IsAvailable => isActiveAndEnabled && !IsStored && !IsReserved;

    public void Initialize(float mass, HookVictim source = null)
    {
        if (mass <= 0f || float.IsNaN(mass) || float.IsInfinity(mass))
            throw new System.ArgumentOutOfRangeException(nameof(mass));
        massKg = mass;
        FollowHero(source);
    }

    internal void FollowHero(HookVictim source)
    {
        sourceVictim = source;
        sourceHealth = source != null ? source.GetComponent<Health>() : null;
    }

    internal bool IsLinkedTo(HookVictim source) => sourceVictim == source;

    private void LateUpdate()
    {
        // The dead hero's own carcass and dropped cargo follow the same capture.
        if (!IsStored && sourceVictim != null && sourceHealth != null &&
            sourceHealth.IsDead && sourceVictim.IsCaught)
            transform.position = sourceVictim.transform.position;
    }

    public void ReleaseFromHero(Vector3 position)
    {
        if (IsStored) return;
        DetachFromHero();
        transform.position = SafeGroundPosition(position);
    }

    public void DetachFromHero()
    {
        sourceVictim = null;
        sourceHealth = null;
    }

    internal bool TryStore(CarcassCarrier newCarrier)
    {
        if (newCarrier == null || !IsAvailable) return false;
        carrier = newCarrier; // Claim before hiding: another carrier cannot take this object.
        sourceVictim = null;
        sourceHealth = null;
        transform.SetParent(newCarrier.transform, false);
        gameObject.SetActive(false);
        return true;
    }

    internal void Drop(Vector3 position)
    {
        carrier = null;
        transform.SetParent(null, true);
        ReleaseFromHero(position);
        gameObject.SetActive(true);
    }

    public static Vector3 SafeGroundPosition(Vector3 position)
    {
        position.z = 0f;
        return ArenaLayout.ClampToBank(position, position.x > 0f);
    }
}
