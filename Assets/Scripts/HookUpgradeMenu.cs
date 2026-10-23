using UnityEngine;
using UnityEngine.InputSystem;

public class HookUpgradeMenu : MonoBehaviour
{
    public static Rect ScreenRect => new Rect(Screen.width - 322f, 12, 310, 394);
    [Header("References")]
    [SerializeField] private PlayerProgression progression;
    [SerializeField] private HookAbility hook;

    private GUIStyle textStyle;
    private GUIStyle messageStyle;

    private string message = "";
    private float messageUntil;

    private void Update()
    {
        if(MatchPause.BlocksGameplay)return;
        if (progression == null || hook == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            BuyUpgrade(HookAbility.UpgradeStat.Range);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            BuyUpgrade(HookAbility.UpgradeStat.Damage);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            BuyUpgrade(HookAbility.UpgradeStat.Width);
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            BuyUpgrade(HookAbility.UpgradeStat.Speed);
        }
        else if (keyboard.digit5Key.wasPressedThisFrame)
        {
            BuyUpgrade(HookAbility.UpgradeStat.Cooldown);
        }
        else if (keyboard.digit6Key.wasPressedThisFrame)
        {
            BuyUpgrade(HookAbility.UpgradeStat.Bounces);
        }
        else if (keyboard.digit7Key.wasPressedThisFrame)
        {
            BuyUpgrade(HookAbility.UpgradeStat.BounceDistance);
        }
    }

    private void BuyUpgrade(HookAbility.UpgradeStat stat)
    {
        if(MatchPause.BlocksGameplay)return;
        if (progression.UpgradePoints <= 0)
        {
            ShowMessage("Нужно очко улучшения.");
            return;
        }

        if (hook.ActiveHook != null)
        {
            ShowMessage("Дождись завершения броска.");
            return;
        }

        if (stat == HookAbility.UpgradeStat.Cooldown &&
            hook.Cooldown <= HookAbility.MinimumCooldown)
        {
            ShowMessage("Кулдаун уже минимальный.");
            return;
        }

        if (stat == HookAbility.UpgradeStat.BounceDistance && hook.BounceDistancePercent >= 100f)
        {
            ShowMessage("Дальность после отскока уже 100%.");
            return;
        }

        if (!hook.TryUpgrade(stat))
        {
            ShowMessage("Сейчас улучшение недоступно.");
            return;
        }

        // Очко есть: проверили в начале этого же вызова.
        progression.TrySpendUpgradePoint();

        ShowMessage("Улучшение применено!");
    }

    private void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.time + 2.5f;
    }

    private void OnGUI()
    {
        if(MatchPause.BlocksGameplay)return;
        if (progression == null || hook == null)
        {
            return;
        }

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 17;
            textStyle.normal.textColor = Color.white;

            messageStyle = new GUIStyle(textStyle);
            messageStyle.wordWrap = true;
        }

        float left = Screen.width - 322f;

        GUI.Box(
            ScreenRect,
            GUIContent.none
        );

        GUI.Label(
            new Rect(left + 12, 22, 286, 26),
            $"Прокачка крюка — очки: {progression.UpgradePoints}",
            textStyle
        );

        GUI.Label(
            new Rect(left + 12, 60, 286, 26),
            $"[1] Дальность: {hook.Range:0.#} м  (+1)",
            textStyle
        );

        GUI.Label(
            new Rect(left + 12, 92, 286, 26),
            $"[2] Урон: {hook.Damage:0.#}  (+10)",
            textStyle
        );

        GUI.Label(
            new Rect(left + 12, 124, 286, 26),
            $"[3] Ширина: {hook.Width:0.##} м  (+0,1)",
            textStyle
        );

        GUI.Label(
            new Rect(left + 12, 156, 286, 26),
            $"[4] Скорость: {hook.Speed:0.#} м/с  (+1)",
            textStyle
        );

        GUI.Label(
            new Rect(left + 12, 192, 286, 26),
            $"[5] Кулдаун: {hook.Cooldown:0.##} с  (−0,25)",
            textStyle
        );

        GUI.Label(new Rect(left + 12, 224, 286, 26),
            $"[6] Отскоки: {hook.MaxBounces}  (+1)", textStyle);
        GUI.Label(new Rect(left + 12, 256, 286, 26),
            $"[7] После отскока: {hook.BounceDistancePercent:0}% (+10 п.п.)", textStyle);

        GUI.Label(
            new Rect(left + 12, 296, 286, 26),
            "Каждое улучшение стоит 1 очко",
            textStyle
        );

        if (Time.time < messageUntil)
        {
            GUI.Label(
                new Rect(left + 12, 334, 286, 60),
                message,
                messageStyle
            );
        }
    }
}
