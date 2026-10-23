using UnityEngine;

public class HookVictim : MonoBehaviour
{
    public event System.Action Captured;
    public HookAbility Captor { get; private set; }
    public bool IsCaught => Captor != null;

    public bool TryCapture(HookAbility captor)
    {
        if (IsCaught)
        {
            return false;
        }

        Captor = captor;
        Captured?.Invoke();
        return true;
    }

    public void Release(HookAbility captor)
    {
        if (Captor == captor)
        {
            Captor = null;
        }
    }
}
