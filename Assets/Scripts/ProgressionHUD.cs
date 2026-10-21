using UnityEngine;

public class ProgressionHUD : MonoBehaviour
{
    [SerializeField] private PlayerProgression progression;
    private void OnGUI()
    {
        if (progression == null) return;
        OwnerEconomy economy = progression.GetComponent<OwnerEconomy>();
        GUI.Box(new Rect(12, 164, 310, 200), GUIContent.none);
        GUI.Label(new Rect(24, 174, 290, 26), $"Золото: {progression.Gold:0.##} (+{progression.GoldPerSecond:0.##}/с)");
        GUI.Label(new Rect(24, 204, 290, 26), $"Уровень {progression.Level}; опыт {progression.Experience:0.##}/{progression.ExperienceToNextLevel}");
        GUI.Label(new Rect(24, 234, 290, 26), $"Очки улучшений: {progression.UpgradePoints}");
        if (economy != null)
        {
            GUI.Label(new Rect(24, 264, 290, 26), $"Продукция: {economy.ProductKg:0.##} кг");
            GUI.Label(new Rect(24, 294, 290, 26), $"Кости: {economy.Bones} (остаток {economy.BoneRemainder:0.##})");
        }
        GUI.Label(new Rect(24, 326, 290, 26), "B — здания; клик по своему — улучшения");
    }
}
