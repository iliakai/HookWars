using UnityEngine;

public class ProgressionHUD : MonoBehaviour
{
    [SerializeField] private PlayerProgression progression;

    private GUIStyle textStyle;

    private void OnGUI()
    {
        if (progression == null)
        {
            return;
        }

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 18;
            textStyle.normal.textColor = Color.white;
        }

        // Под существующим табло убийств и смертей.
        GUI.Box(new Rect(12, 164, 280, 174), GUIContent.none);

        GUI.Label(
            new Rect(24, 174, 255, 26),
            $"Золото: {Mathf.FloorToInt(progression.Gold)}",
            textStyle
        );

        GUI.Label(
            new Rect(24, 202, 255, 26),
            $"Уровень: {progression.Level}",
            textStyle
        );

        GUI.Label(
            new Rect(24, 230, 255, 26),
            $"Опыт: {Mathf.FloorToInt(progression.Experience)}" +
            $" / {progression.ExperienceToNextLevel}",
            textStyle
        );

        GUI.Label(
            new Rect(24, 258, 255, 26),
            $"Очки улучшения: {progression.UpgradePoints}",
            textStyle
        );

        GUI.Label(
            new Rect(24, 290, 255, 30),
            $"Доход: {progression.GoldPerSecond:0.#} зол./с, " +
            $"{progression.ExperiencePerSecond:0.#} опыта/с",
            textStyle
        );
    }
}