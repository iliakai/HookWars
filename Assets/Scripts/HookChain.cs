using System.Collections.Generic;
using UnityEngine;

// One sprite strip per segment; reuses the existing Chain material and color.
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(SpriteRenderer))]
public class HookChain : MonoBehaviour
{
    [SerializeField] private HookAbility owner;
    [SerializeField, Min(0.01f)] private float thickness = 0.08f;

    private SpriteRenderer template;
    private readonly List<SpriteRenderer> segments = new List<SpriteRenderer>();

    private void Awake()
    {
        template = GetComponent<SpriteRenderer>();
        template.enabled = false;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void LateUpdate()
    {
        int count = owner == null ? 0 : Mathf.Max(0, owner.ChainPointCount - 1);
        while (segments.Count < count)
        {
            GameObject part = new GameObject("ChainSegment");
            part.transform.SetParent(transform, false);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = template.sprite;
            renderer.sharedMaterial = template.sharedMaterial;
            renderer.color = template.color;
            renderer.sortingLayerID = template.sortingLayerID;
            renderer.sortingOrder = template.sortingOrder;
            segments.Add(renderer);
        }

        for (int i = 0; i < segments.Count; i++)
        {
            SpriteRenderer segment = segments[i];
            segment.enabled = i < count;
            if (i >= count) continue;
            Vector3 start = owner.GetChainPoint(i);
            Vector3 end = owner.GetChainPoint(i + 1);
            Vector3 direction = end - start;
            segment.transform.position = (start + end) * 0.5f;
            segment.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            segment.transform.localScale = new Vector3(direction.magnitude, thickness, 1f);
        }
    }

    private void OnDisable()
    {
        if (template != null) template.enabled = false;
        foreach (SpriteRenderer segment in segments)
            if (segment != null) segment.enabled = false;
    }
}
