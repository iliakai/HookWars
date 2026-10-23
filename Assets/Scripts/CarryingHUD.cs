using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CarcassCarrier), typeof(PlayerProgression))]
[DisallowMultipleComponent]
public class CarryingHUD : MonoBehaviour
{
    public static Rect ScreenRect => new Rect(12, 374, 310, 92);
    private CarcassCarrier carrier;
    private PlayerProgression progression;
    private void Awake()
    {
        carrier = GetComponent<CarcassCarrier>();
        progression = GetComponent<PlayerProgression>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.digit8Key.wasPressedThisFrame)
            carrier.TryUpgradeCapacity(progression);
    }

    private void OnGUI()
    {
        GUI.Box(ScreenRect, GUIContent.none);
        GUI.Label(new Rect(24, 382, 290, 26),
            $"Груз: {carrier.CurrentMassKg:0.##} / {carrier.CapacityKg:0.##} кг");
        GUI.Label(new Rect(24, 408, 290, 26),
            $"[8] Вместимость +{carrier.CapacityPerUpgrade:0.##} кг — 1 очко");
        GUI.Label(new Rect(24, 434, 290, 26), $"Туш в грузе: {carrier.Count}");
    }
}
