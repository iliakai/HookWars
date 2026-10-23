using UnityEngine;

[DefaultExecutionOrder(900)]
[RequireComponent(typeof(Camera))]
public class ArenaCamera25D : MonoBehaviour
{
    [SerializeField, Range(25, 80)] private float tilt = 55f;
    [SerializeField, Min(0)] private float margin = 1.5f;
    private Camera view;
    public float Tilt { get => tilt; set => tilt = Mathf.Clamp(value, 25, 80); }
    private void Awake() { view = GetComponent<Camera>(); Fit(); }
    private void LateUpdate() => Fit();
    public void Fit()
    {
        if (view == null) view = GetComponent<Camera>();
        // Camera is south of the field: logical +Y projects UP; +X projects RIGHT.
        transform.rotation = Quaternion.Euler(tilt, 0, 0);
        transform.position = new Vector3(0, 1, 0) - transform.forward * 65f;
        view.orthographic = true;
        view.nearClipPlane = 0.1f; view.farClipPlane = 150;
        float halfWidth = ArenaLayout.OuterEdge + ArenaLayout.MountainWidth;
        float halfDepth = ArenaLayout.FieldHeight * 0.5f;
        // Project all corners of a volume enclosing the ground and 3m-high visuals.
        float extentX = 0, extentY = 0;
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                for (int h = 0; h <= 3; h += 3)
                {
                    Vector3 relative = transform.InverseTransformPoint(new Vector3(x * halfWidth, h, z * halfDepth));
                    extentX = Mathf.Max(extentX, Mathf.Abs(relative.x));
                    extentY = Mathf.Max(extentY, Mathf.Abs(relative.y));
                }
        view.orthographicSize = Mathf.Max(extentY + margin, (extentX + margin) / Mathf.Max(0.01f, view.aspect));
    }
}
