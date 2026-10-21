using UnityEngine;

// Retained for legacy prefab GUIDs and explicit Editor migration only.
public class IncomeBuilding : MonoBehaviour
{
    public enum ResourceType { Gold, Experience }
    [SerializeField] private ResourceType resource = ResourceType.Gold;
    [SerializeField] private float baseBonus = 1f;
    [SerializeField] private float bonusPerUpgrade = 1f;
    [SerializeField] private int firstUpgradeCost = 75;
    [SerializeField] private float costGrowth = 1.5f;
    public PlayerProgression Owner => null;
    public int Level => 1;
    public int UpgradeCost => firstUpgradeCost;
    public float TotalBonus => 0;
    public string DisplayName => resource == ResourceType.Gold ? "Рудник (устарел)" : "Университет (устарел)";
    public string ResourceLabel => resource == ResourceType.Gold ? "золота/с" : "опыта/с";
    public void Initialize(PlayerProgression owner)
    {
        Debug.LogWarning("Legacy income building is disabled; run HookWars/Economy setup.", this);
    }
    public bool TryUpgrade() => false;
    protected virtual void OnDestroy() { }
}
