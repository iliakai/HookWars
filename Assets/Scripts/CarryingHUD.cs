using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CarcassCarrier), typeof(PlayerProgression))]
[DisallowMultipleComponent]
public class CarryingHUD : MonoBehaviour
{
    public static Rect ScreenRect => new Rect(12, 374, 310, 118);
    private CarcassCarrier carrier;
    private PlayerProgression progression;
    private void Awake()
    {
        carrier = GetComponent<CarcassCarrier>();
        progression = GetComponent<PlayerProgression>();
    }

    private void Update()
    {
        if(MatchPause.BlocksGameplay)return;
    }

    private void OnGUI()
    {
        if(MatchPause.BlocksGameplay)return;
        GUI.Box(ScreenRect, GUIContent.none);
        GUI.Label(new Rect(24, 382, 290, 26),
            $"Груз: {carrier.CurrentMassKg:0.##} / {carrier.CapacityKg:0.##} кг");
        GUI.Label(new Rect(24, 408, 290, 26),
            "Улучшение вместимости — в меню развития");
        GUI.Label(new Rect(24, 434, 290, 26), $"Туш в грузе: {carrier.Count}");
        var hp=GetComponent<Health>();
        GUI.Label(new Rect(24,460,290,26),$"Вес / здоровье: {hp.CurrentKg:0.0} / {hp.MaximumKg:0.0} кг");
    }
}
