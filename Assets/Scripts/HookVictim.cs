using UnityEngine;

public class HookVictim : MonoBehaviour
{
    public HookAbility Captor { get; private set; }
    public bool IsCaught => Captor != null;

    public bool TryCapture(HookAbility captor)
    {
        if (IsCaught)
        {
            return false;
        }

        Captor = captor;
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