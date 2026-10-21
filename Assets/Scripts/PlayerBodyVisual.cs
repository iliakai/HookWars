using UnityEngine;

[DefaultExecutionOrder(50)]
public class PlayerBodyVisual : MonoBehaviour
{
    [SerializeField] private PlayerAim playerAim;
    [SerializeField] private float angleOffset = 0f;

    private void LateUpdate()
    {
        if (playerAim == null)
        {
            return;
        }

        Vector2 direction = playerAim.AimDirection;

        float angle = Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            angle + angleOffset
        );
    }
}