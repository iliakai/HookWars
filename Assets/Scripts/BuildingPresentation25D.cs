using HookWars.Buildings;
using UnityEngine;

// Reads economic state; never produces, consumes or sells anything.
[DefaultExecutionOrder(1150)]
public sealed class BuildingPresentation25D : MonoBehaviour
{
    private GameBuilding source;
    private BuildingVisualController visual;
    public void Bind(GameBuilding building)
    {
        source = building; visual = GetComponent<BuildingVisualController>();
        Refresh();
    }
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (source == null || visual == null) return;
        bool enabled = source.isActiveAndEnabled && source.Owner != null;
        if (source is SausageFactory factory) visual.SetWorking(enabled && factory.RawKg > 1e-9);
        else if (source is MeatShop shop)
        {
            var economy = source.Owner != null ? source.Owner.GetComponent<OwnerEconomy>() : null;
            visual.SetOpen(enabled && economy != null && (economy.ProductKg > 1e-9 || Time.time - shop.LastSaleTime < .25f));
        }
        else visual.SetActiveMode(enabled);
    }
}
