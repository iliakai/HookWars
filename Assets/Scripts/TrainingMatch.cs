using System.Collections;
using UnityEngine;

public class TrainingMatch : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health enemyHealth;
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerHook playerHook;

    [Header("Respawn")]
    [SerializeField, Min(0.1f)] private float respawnDelay = 3f;

    private Vector3 enemySpawnPosition;
    private Quaternion enemySpawnRotation;

    private int kills;
    private int deaths;
    private bool waitingForRespawn;
    private GUIStyle scoreStyle;

    private void Start()
    {
        if (enemyHealth == null ||
            playerHealth == null ||
            playerHook == null)
        {
            Debug.LogError(
                "Назначь Enemy Health, Player Health и Player Hook " +
                "в Training Match.",
                this
            );

            enabled = false;
            return;
        }

        enemySpawnPosition = enemyHealth.transform.position;
        enemySpawnRotation = enemyHealth.transform.rotation;

        enemyHealth.Died += OnEnemyDied;
        playerHealth.Died += OnPlayerDied;
    }

    private void OnEnemyDied(Health target)
    {
        if (waitingForRespawn)
        {
            return;
        }

        kills++;
        waitingForRespawn = true;

        StartCoroutine(RespawnEnemy());
    }

    private void OnPlayerDied(Health target)
    {
        deaths++;
    }

    private IEnumerator RespawnEnemy()
    {
        yield return new WaitForSeconds(respawnDelay);

        while (playerHook != null &&
               enemyHealth != null &&
               playerHook.IsPullingTarget(enemyHealth.transform))
        {
            yield return null;
        }

        if (enemyHealth != null)
        {
            enemyHealth.transform.SetPositionAndRotation(
                enemySpawnPosition,
                enemySpawnRotation
            );

            enemyHealth.Revive();
        }

        waitingForRespawn = false;
    }

    private void OnGUI()
    {
        if (scoreStyle == null)
        {
            scoreStyle = new GUIStyle(GUI.skin.label);
            scoreStyle.fontSize = 24;
            scoreStyle.fontStyle = FontStyle.Bold;
            scoreStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(12, 12, 280, 140), GUIContent.none);

        GUI.Label(
            new Rect(24, 20, 255, 32),
            "Убийства: " + kills,
            scoreStyle
        );

        GUI.Label(
            new Rect(24, 52, 255, 32),
            "Смерти: " + deaths,
            scoreStyle
        );

        if (waitingForRespawn)
        {
            GUI.Label(
                new Rect(24, 90, 255, 24),
                "Враг возрождается..."
            );
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            GUI.Label(
                new Rect(24, 114, 255, 24),
                "Ты возрождаешься..."
            );
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= OnEnemyDied;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }
    }
}