using UnityEngine;

[RequireComponent(typeof(HookVictim), typeof(CircleCollider2D))]
public class Animal : MonoBehaviour
{
    public enum Species { Sheep, Cow, Pig }
    [SerializeField] private Species species;
    public Species Kind => species;
    public Vector2 VoluntaryVelocity { get; private set; }
    [SerializeField, Min(0.01f)] private float massKg = 4;
    [SerializeField, Min(0.01f)] private float moveSpeed = 1;
    [SerializeField, Min(0.01f)] private float radius = 0.35f;
    [SerializeField, Min(0.1f)] private float minimumDirectionInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float maximumDirectionInterval = 2f;
    [SerializeField] private Carcass carcassPrefab;
    private HookVictim victim;
    private bool rightBank, dead;
    private Vector2 direction;
    private float nextDirection;
    private Carcass carcass;
    private float pushedUntil;
    public bool IsDead => dead;
    public float MassKg => massKg;
    public float Radius => radius;
    private void Awake()
    {
        victim = GetComponent<HookVictim>();
        GetComponent<CircleCollider2D>().radius = radius;
        rightBank = transform.position.x > 0;
        PickDirection();
    }
    private void PickDirection()
    {
        direction = Random.insideUnitCircle.normalized;
        nextDirection = Time.time + Random.Range(minimumDirectionInterval, Mathf.Max(minimumDirectionInterval, maximumDirectionInterval));
    }
    private void Update()
    {
        VoluntaryVelocity = Vector2.zero;
        if (Time.deltaTime <= 0) return;
        if (dead || victim.IsCaught || Time.time<pushedUntil) return;
        if (Time.time >= nextDirection) PickDirection();
        Vector3 next = transform.position + (Vector3)direction * moveSpeed * Time.deltaTime;
        bool moving = ArenaLayout.IsInsideBank(next, rightBank, radius) && WorldCollision.CanMove(transform, next, radius);
        if (moving) { VoluntaryVelocity = (Vector2)(next-transform.position)/Time.deltaTime; transform.position = next; Physics2D.SyncTransforms(); }
        else PickDirection();
    }
    public Carcass Kill()
    {
        if (dead) return carcass;
        dead = true;
        GetComponent<Collider2D>().enabled = false;
        carcass = Instantiate(carcassPrefab, Carcass.SafeGroundPosition(transform.position), Quaternion.identity);
        carcass.Initialize(massKg);
        carcass.SetAnimalSpecies(species);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return carcass;
    }
    public bool TryPush(Vector3 destination)
    {
        if(dead||victim.IsCaught||!ArenaLayout.IsInsideBank(destination,rightBank,radius)||!WorldCollision.CanMove(transform,destination,radius))return false;
        transform.position=destination;VoluntaryVelocity=Vector2.zero;pushedUntil=Time.time+.12f;Physics2D.SyncTransforms();return true;
    }
}
