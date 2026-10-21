using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CarcassCarrier), typeof(PlayerProgression))]
[DisallowMultipleComponent]
public class CarryingHUD : MonoBehaviour
{
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
        GUI.Box(new Rect(12, 348, 310, 92), GUIContent.none);
        GUI.Label(new Rect(24, 356, 290, 26),
            $"Груз: {carrier.CurrentMassKg:0.##} / {carrier.CapacityKg:0.##} кг");
        GUI.Label(new Rect(24, 382, 290, 26),
            $"[8] Вместимость +{carrier.CapacityPerUpgrade:0.##} кг — 1 очко");
        GUI.Label(new Rect(24, 408, 290, 26), $"Туш в грузе: {carrier.Count}");
    }
}
