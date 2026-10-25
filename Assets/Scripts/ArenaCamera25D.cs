using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(900)]
[RequireComponent(typeof(Camera))]
public class ArenaCamera25D : MonoBehaviour
{
    [SerializeField, Range(25, 80)] private float tilt = 55f;
    [SerializeField, Min(0)] private float margin = 1.5f;
    private Camera view;
    public float Zoom {get;private set;}=1;
    private Vector2 center;
    private float targetZoom=1,yaw;
    private bool orbiting;
    private Vector2 lastMouse,zoomAnchor;
    public float Yaw=>yaw;
    public void SetZoom(float zoom){targetZoom=Zoom=Mathf.Clamp(zoom,1,4);if(Zoom==1)center=Vector2.zero;Fit();}
    public float Tilt { get => tilt; set => tilt = Mathf.Clamp(value, 25, 80); }
    private void Awake() { view = GetComponent<Camera>(); Fit(); }
    private void LateUpdate()
    {
        var mouse=Mouse.current;
        if(mouse!=null&&!MatchPause.BlocksGameplay)
        {
            Vector2 position=mouse.position.ReadValue();bool overUI=GamePointer.OverInterface(position);
            if(mouse.middleButton.wasPressedThisFrame&&!overUI){orbiting=true;lastMouse=position;}
            if(!mouse.middleButton.isPressed)orbiting=false;
            if(orbiting){var delta=position-lastMouse;yaw=Mathf.Repeat(yaw+delta.x*.3f,360);Tilt+=delta.y*.25f;lastMouse=position;}
            float scroll=mouse.scroll.ReadValue().y;
            if(!overUI&&Mathf.Abs(scroll)>.01f)
            {
                targetZoom=Mathf.Clamp(targetZoom*Mathf.Pow(1.4f,scroll/120f),1,4);zoomAnchor=position;
            }
            if(Mathf.Abs(Zoom-targetZoom)>.0001f)
            {
                GamePointer.TryLogicalPoint(view,zoomAnchor,out var before);
                Zoom=Mathf.Lerp(Zoom,targetZoom,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
                if(Mathf.Abs(Zoom-targetZoom)<.001f)Zoom=targetZoom;
                Fit();GamePointer.TryLogicalPoint(view,zoomAnchor,out var after);center+=(Vector2)(before-after);
                center.x=Mathf.Clamp(center.x,-ArenaLayout.OuterEdge*(1-1/Zoom),ArenaLayout.OuterEdge*(1-1/Zoom));
                center.y=Mathf.Clamp(center.y,-ArenaLayout.FieldHeight*.5f*(1-1/Zoom),ArenaLayout.FieldHeight*.5f*(1-1/Zoom));
            }
        }
        else orbiting=false;
        Fit();
    }
    public void Fit()
    {
        if (view == null) view = GetComponent<Camera>();
        // Camera is south of the field: logical +Y projects UP; +X projects RIGHT.
        transform.rotation = Quaternion.Euler(tilt, yaw, 0);
        transform.position = new Vector3(center.x, 1, center.y) - transform.forward * 65f;
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
                    Vector3 relative = Quaternion.Inverse(transform.rotation)*(new Vector3(x * halfWidth, h, z * halfDepth)-Vector3.up);
                    extentX = Mathf.Max(extentX, Mathf.Abs(relative.x));
                    extentY = Mathf.Max(extentY, Mathf.Abs(relative.y));
                }
        float usableFraction=Mathf.Max(.1f,1-2*GamePointer.FieldSideInset/Mathf.Max(1,Screen.width));
        view.orthographicSize = Mathf.Max(extentY + margin, (extentX + margin) / Mathf.Max(0.01f, view.aspect*usableFraction))/Zoom;
    }
}
