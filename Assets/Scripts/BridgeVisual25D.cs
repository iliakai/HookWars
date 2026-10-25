using System.Linq;
using UnityEngine;

public sealed class BridgeVisual25D : MonoBehaviour
{
    [SerializeField] private Transform[] stages;
    [SerializeField] private Material ghost;
    private Material[][] completed;
    private Renderer[] surfaces;
    private RiverBridge source;
    private int shown=-1;
    public void Bind(RiverBridge bridge)
    {source=bridge;surfaces=stages.Select(s=>s.GetComponentInChildren<Renderer>()).ToArray();completed=surfaces.Select(r=>r.sharedMaterials).ToArray();Refresh();}
    private void LateUpdate()=>Refresh();
    private void Refresh()
    {
        if(source==null||shown==source.Stage)return;shown=source.Stage;
        for(int i=0;i<3;i++)
        {
            surfaces[i].sharedMaterials=i<shown?completed[i]:Enumerable.Repeat(ghost,completed[i].Length).ToArray();
            surfaces[i].shadowCastingMode=i<shown?UnityEngine.Rendering.ShadowCastingMode.On:UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
