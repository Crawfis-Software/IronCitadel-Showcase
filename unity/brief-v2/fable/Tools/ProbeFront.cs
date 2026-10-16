using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProbeFront
{
    public static string Run()
    {
        var sb = new StringBuilder();
        string[] paths = {
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Bookcase_01.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Bookcase_Grand_01.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_KnightStand_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Torch_Ornate_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_WeaponRack_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Stone_Throne_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Railing_Angle_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Alter_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Statue_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Statue_04.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Walls/SM_Env_Wall_Fireplace_Entrance_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Walls/SM_Env_Wall_Alcove_Small_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Wall_Banner_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Props/SM_Prop_Banner_Damaged_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Props/SM_Prop_Banner_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Candle_Chandelier_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Chain_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Chain_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Chain_03.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Chain_04.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Chain_05.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/FX/FX_CandleFlame_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/FX/FX_Fire_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/FX/FX_Dust_Large_01_Soft.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Environments/SM_Env_SpiderWeb_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Props/SM_Prop_Dwarf_Shelf_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Skeleton_Slave_Shackles_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Rug_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Rug_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Rug_03.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Bench_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Table_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Props/SM_Prop_Hanging_Weapon_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Weapons/SM_Wep_Shield_Heater_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Environments/Floors/SM_Env_Stairs_02.prefab",
        };
        foreach (var p in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) { sb.AppendLine("MISSING " + p); continue; }
            int px = 0, nx = 0, pz = 0, nz = 0, py = 0; double sumXY = 0, sumZY = 0; int nv = 0;
            Bounds b = new Bounds(); bool has = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (m == null) continue;
                var verts = m.vertices; var norms = m.normals;
                for (int i = 0; i < verts.Length; i++)
                {
                    var n = mf.transform.TransformDirection(norms[i]); var v = mf.transform.TransformPoint(verts[i]);
                    if (n.x > 0.6f) px++; else if (n.x < -0.6f) nx++; if (n.z > 0.6f) pz++; else if (n.z < -0.6f) nz++; if (n.y > 0.6f) py++;
                    sumXY += v.x * v.y; sumZY += v.z * v.y; nv++;
                }
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            var ps = go.GetComponentsInChildren<ParticleSystem>();
            sb.AppendFormat("{0}: +x {1} -x {2} +z {3} -z {4} +y {5} | min=({6:F2},{7:F2},{8:F2}) max=({9:F2},{10:F2},{11:F2}) | xy {12:F1} zy {13:F1} | ps {14}\n",
                System.IO.Path.GetFileNameWithoutExtension(p), px, nx, pz, nz, py, b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z, sumXY, sumZY, ps.Length);
        }
        System.IO.File.WriteAllText("Tools/probe_front.txt", sb.ToString());
        return "ok";
    }
}
