using UnityEngine;

[RequireComponent(typeof(HookVictim), typeof(CircleCollider2D))]
public class Animal : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float massKg = 4;
    [SerializeField, Min(0.01f)] private float moveSpeed = 1;
    [SerializeField, Min(0.01f)] private float animationReferenceSpeed = 1;
    [SerializeField, Min(0.01f)] private float radius = 0.35f;
    [SerializeField, Min(0.1f)] private float minimumDirectionInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float maximumDirectionInterval = 2f;
    [SerializeField] private Carcass carcassPrefab;
    [SerializeField] private SpriteRenderer visual;
    private Animator animator;
    private HookVictim victim;
    private bool rightBank, dead;
    private Vector2 direction;
    private float nextDirection;
    private Carcass carcass;
    public bool IsDead => dead;
    public float MassKg => massKg;
    public float Radius => radius;
    private void Awake()
    {
        animator = GetComponent<Animator>(); victim = GetComponent<HookVictim>();
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
        if (dead || victim.IsCaught) { if (animator != null) animator.speed = 0; return; }
        if (Time.time >= nextDirection) PickDirection();
        Vector3 next = transform.position + (Vector3)direction * moveSpeed * Time.deltaTime;
        bool moving = ArenaLayout.IsInsideBank(next, rightBank, radius) && WorldCollision.CanMove(transform, next, radius);
        if (moving) { transform.position = next; Physics2D.SyncTransforms(); }
        else PickDirection();
        if (visual != null && Mathf.Abs(direction.x) > 0.01f) visual.flipX = direction.x < 0;
        if (animator != null) animator.speed = moving ? moveSpeed / animationReferenceSpeed : 0;
    }
    public Carcass Kill()
    {
        if (dead) return carcass;
        dead = true;
        if (animator != null) animator.enabled = false;
        GetComponent<Collider2D>().enabled = false;
        carcass = Instantiate(carcassPrefab, Carcass.SafeGroundPosition(transform.position), Quaternion.identity);
        carcass.Initialize(massKg);
        SpriteRenderer renderer = carcass.GetComponent<SpriteRenderer>();
        renderer.sprite = visual.sprite; renderer.flipX = visual.flipX; renderer.color = new Color(0.7f, 0.4f, 0.35f);
        float dimension = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        carcass.transform.localScale = Vector3.one * (0.65f / dimension);
        carcass.GetComponent<CircleCollider2D>().radius = dimension * 0.5f;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return carcass;
    }
}
