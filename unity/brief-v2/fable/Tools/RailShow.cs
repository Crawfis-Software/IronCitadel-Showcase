using UnityEditor;
using UnityEngine;
public static class RailShow
{
    public static string Run()
    {
        var root = new GameObject("RailShow");
        string[] paths = {
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Railing_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Railing_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Railing_03.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Railing_04.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Wood/SM_Env_Wood_Railing_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Wood/SM_Env_Wood_Railing_02.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Environments/SM_Env_Dwarf_Balustrade_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Environments/SM_Env_Dwarf_Balustrade_02.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Env_Railing_Curved_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Railing_Angle_01.prefab",
        };
        for (int i = 0; i < paths.Length; i++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p); go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(200 + i * 6, 0, 0);
        }
        var floor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonDungeon/Prefabs/Environments/Floors/SM_Env_Tiles_03.prefab");
        for (int i = 0; i < 14; i++) { var f = (GameObject)PrefabUtility.InstantiatePrefab(floor); f.transform.SetParent(root.transform); f.transform.position = new Vector3(195 + i * 5 + 5, -0.1f, -5); }
        var l = new GameObject("RailLight").AddComponent<Light>(); l.transform.SetParent(root.transform); l.type = LightType.Directional; l.intensity = 1.5f; l.transform.rotation = Quaternion.Euler(45, 30, 0);
        string r = IronCitadel.EditorTools.IronCitadelCaptures.Adhoc("rails_a,205,2.5,9,180,12;rails_b,235,2.5,9,180,12;rails_c,251,2.5,9,180,12");
        Object.DestroyImmediate(root);
        return r;
    }
}
