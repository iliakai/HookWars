using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingUpgradeMenu : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;

    private PlayerProgression progression;
    private PlayerBuilding builder;
    private Health health;
    private HookVictim victim;

    private IncomeBuilding hoveredBuilding;
    private string message = "";
    private float messageUntil;

    private void Awake()
    {
        progression = GetComponent<PlayerProgression>();
        builder = GetComponent<PlayerBuilding>();
        health = GetComponent<Health>();
        victim = GetComponent<HookVictim>();

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void Update()
    {
        hoveredBuilding = null;

        if (progression == null ||
            worldCamera == null ||
            Mouse.current == null ||
            Keyboard.current == null)
        {
            return;
        }

        if ((builder != null && builder.IsBuilding) ||
            (health != null && health.IsDead) ||
            (victim != null && victim.IsCaught))
        {
            return;
        }

        Vector2 mouse = Mouse.current.position.ReadValue();

        Vector3 point = worldCamera.ScreenToWorldPoint(
            new Vector3(
                mouse.x,
                mouse.y,
                -worldCamera.transform.position.z
            )
        );

        Physics2D.SyncTransforms();

        Collider2D hit = Physics2D.OverlapPoint(
            point,
            LayerMask.GetMask("Buildings")
        );

        if (hit == null)
        {
            return;
        }

        IncomeBuilding target = hit.GetComponent<IncomeBuilding>();

        if (target == null || target.Owner != progression)
        {
            return;
        }

        hoveredBuilding = target;

        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            message = target.TryUpgrade()
                ? "Здание улучшено!"
                : "Недостаточно золота.";

            messageUntil = Time.time + 2f;
        }
    }

    private void OnGUI()
    {
        if (hoveredBuilding == null)
        {
            return;
        }

        float left = (Screen.width - 420f) * 0.5f;
        float top = Screen.height - 130f;

        GUI.Box(new Rect(left, top, 420, 118), GUIContent.none);

        GUI.Label(
            new Rect(left + 12, top + 8, 396, 24),
            $"{hoveredBuilding.DisplayName} — уровень {hoveredBuilding.Level}"
        );

        GUI.Label(
            new Rect(left + 12, top + 32, 396, 24),
            $"Доход здания: +{hoveredBuilding.TotalBonus:0.#} " +
            hoveredBuilding.ResourceLabel
        );

        GUI.Label(
            new Rect(left + 12, top + 56, 396, 24),
            $"U — улучшить за {hoveredBuilding.UpgradeCost} золота"
        );

        if (Time.time < messageUntil)
        {
            GUI.Label(
                new Rect(left + 12, top + 82, 396, 24),
                message
            );
        }
    }
}