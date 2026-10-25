using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class EconomySetup
{
    public const string FactoryPath="Assets/Prefabs/SausageFactory.prefab",ShopPath="Assets/Prefabs/MeatShop.prefab",FarmPath="Assets/Prefabs/AnimalFarm.prefab",WallPath="Assets/Prefabs/BoneWall.prefab";
    [MenuItem("HookWars/Economy/Validate current economy wiring")]
    public static void Stage5()
    {
        CarryingSetup.Configure();
        foreach(string path in new[]{FactoryPath,ShopPath,FarmPath,WallPath,BarracksSetup.Building})
        {
            var building=AssetDatabase.LoadAssetAtPath<GameBuilding>(path);
            if(building==null||building.GetComponent<BoxCollider2D>()?.size!=(Vector2)building.Footprint||building.transform.localScale!=Vector3.one)
                throw new InvalidOperationException("Invalid building footprint: "+path);
        }
        foreach(var builder in Object.FindObjectsByType<PlayerBuilding>())
        {
            var data=new SerializedObject(builder);var prefabs=data.FindProperty("buildingPrefabs");
            if(prefabs.arraySize!=4)throw new InvalidOperationException("Shop/farm/wall/barracks choices required");
            var kinds=new System.Collections.Generic.HashSet<BuildingKind>();
            for(int i=0;i<prefabs.arraySize;i++)
            {
                var choice=prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameBuilding;
                if(choice==null||choice.Kind==BuildingKind.Factory||!kinds.Add(choice.Kind))throw new InvalidOperationException("Invalid or duplicate building choice");
            }
        }
    }
    public static void Stage4()=>Stage5();
    public static void Stage3()=>Stage5();
    public static void Stage2()=>Stage5();
}
