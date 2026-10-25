using UnityEngine;

public sealed class WorldBuildingHUD : MonoBehaviour
{
    private void OnGUI()
    {
        if(Camera.main==null||MatchPause.BlocksGameplay)return;
        var display=GetComponent<ArenaPresentation25D>();if(display==null)return;
        var old=GUI.color;
        foreach(var building in FindObjectsByType<GameBuilding>())
        {
            if(building.MaximumHP<=0||!building.isActiveAndEnabled)continue;
            var view=display.VisualFor(building);if(view==null)continue;
            var renderer=view.GetComponentInChildren<Renderer>();if(renderer==null||renderer.forceRenderingOff)continue;
            var point=Camera.main.WorldToScreenPoint(view.position+Vector3.up*(building is SausageFactory?2.4f:building is Barracks?2.1f:1.25f));
            if(point.z<=0)continue;
            var rect=new Rect(point.x-28,Screen.height-point.y-3,56,6);
            GUI.color=new Color(.08f,.08f,.08f,.85f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=building.Owner?.GetComponent<PlayerMovement>()!=null?new Color(.2f,.9f,.3f):new Color(1,.25f,.2f);
            GUI.DrawTexture(new Rect(rect.x+1,rect.y+1,54*Mathf.Clamp01(building.CurrentHP/building.MaximumHP),4),Texture2D.whiteTexture);
        }
        GUI.color=old;
    }
}
