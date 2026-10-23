using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerRespawn : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float respawnDelay = 3f;

    private Health health;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private Behaviour[] controls;
    private bool[] previousStates;

    private void Awake()
    {
        health = GetComponent<Health>();

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        controls = new Behaviour[]
        {
            GetComponent<PlayerMovement>(),
            GetComponent<PlayerHook>(),
            GetComponent<PlayerMelee>(),
            GetComponentInChildren<PlayerAim>()
        };

        previousStates = new bool[controls.Length];
    }

    private void Start()
    {
        health.Died += OnPlayerDied;
    }

    private void OnPlayerDied(Health target)
    {
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] == null)
            {
                continue;
            }

            previousStates[i] = controls[i].enabled;
            controls[i].enabled = false;
        }

        StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);

        HookVictim victim = GetComponent<HookVictim>();

        while (victim != null && victim.IsCaught)
        {
            yield return null;
        }

        transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );

        health.Revive();

        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] != null)
            {
                controls[i].enabled = previousStates[i];
            }
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnPlayerDied;
        }
    }
}
