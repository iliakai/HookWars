using System.Collections.Generic;
using UnityEngine;

public sealed class CargoPresentation25D : MonoBehaviour
{
    private CarcassCarrier source;
    private ArenaPresentation25D presentation;
    private readonly Dictionary<Carcass,GameObject> views=new Dictionary<Carcass,GameObject>();
    private readonly List<Carcass> removed=new List<Carcass>();
    public void Bind(CarcassCarrier carrier,ArenaPresentation25D display){source=carrier;presentation=display;}
    private void LateUpdate()
    {
        if(source==null)return;
        int i=0;
        foreach(var carcass in source.StoredCargo)
        {
            if(carcass==null)continue;
            if(!views.TryGetValue(carcass,out var view))
            {
                var prefab=presentation.ModelFor(carcass);if(prefab==null)continue;
                view=Instantiate(prefab,transform,false);views.Add(carcass,view);
                // A rotating, attached meat mesh can otherwise project an unstable
                // long cascade shadow. Carried meat keeps lighting, but casts none.
                foreach(var renderer in view.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            view.transform.localScale=Vector3.one*.5f*Mathf.Pow(carcass.VisualMassFraction,1f/3f);
            view.transform.localPosition=new Vector3((i%2-.5f)*.32f,(i/2)*.16f,0);i++;
        }
        removed.Clear();foreach(var pair in views)if(pair.Key==null||!Contains(pair.Key)){Destroy(pair.Value);removed.Add(pair.Key);}
        foreach(var key in removed)views.Remove(key);
    }
    private bool Contains(Carcass item){foreach(var c in source.StoredCargo)if(c==item)return true;return false;}
    private void OnDisable(){foreach(var v in views.Values)if(v!=null)Destroy(v);views.Clear();}
}
