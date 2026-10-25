using UnityEngine;

public class ProgressionHUD : MonoBehaviour
{
    public static Rect ScreenRect => new Rect(12, 164, 310, 210);
    [SerializeField] private PlayerProgression progression;
    private void OnGUI()
    {
        if (progression == null) return;
        OwnerEconomy economy = progression.GetComponent<OwnerEconomy>();
        GUI.Box(ScreenRect, GUIContent.none);
        GUI.Label(new Rect(24, 174, 290, 26), new GUIContent($"Золото: {System.Math.Floor(progression.Gold):0} (+{progression.CurrentGoldIncomePerSecond:0.#}/с)",
            "Фактические поступления за секунду: пассивный доход и продажи. Покупки не уменьшают доход."));
        GUI.Label(new Rect(24, 204, 290, 26), $"Уровень {progression.Level}; опыт {progression.Experience:0.##}/{progression.ExperienceToNextLevel}");
        GUI.Label(new Rect(24, 234, 290, 26), $"Очки улучшений: {progression.UpgradePoints}");
        if (economy != null)
        {
            GUI.Label(new Rect(24, 264, 290, 26), $"Готовое мясо: {economy.ProductKg:0.#} кг · ∞");
            GUI.Label(new Rect(24, 294, 290, 26), $"Кости: {economy.BoneKg:0.#} кг");
        }
        GUI.Label(new Rect(24, 326, 290, 26), "Клик по своему зданию — меню / снос");
        var hook = progression.GetComponent<HookAbility>();
        if(hook!=null)GUI.Label(new Rect(24,348,290,26),hook.SelectedPullMode==HookAbility.PullMode.TargetToOwner
            ? "E: крюк — притянуть цель" : "E: крюк — лететь к цели");
    }
}
