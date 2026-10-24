using UnityEngine;

// Permanent ground fixture; never a damageable hero or economic building.
[RequireComponent(typeof(CircleCollider2D))]
public sealed class HookAnchor : MonoBehaviour { }
