using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class World25DDiagnostics
{
    public static void MeasureBody()
    {
        var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Butcher25DSetup.ModelPath));
        var mesh = new Mesh();
        try
        {
            var body = rig.GetComponentsInChildren<Transform>().Single(t => t.name == "BodyRoll");
            var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.StartsWith("02_Body_"));
            skin.BakeMesh(mesh, false);
            Matrix4x4 boneToWorld = body.localToWorldMatrix * skin.sharedMesh.bindposes[System.Array.IndexOf(skin.bones,body)];
            Vector3[] points = skin.sharedMesh.vertices.Select(v => rig.transform.InverseTransformPoint(boneToWorld.MultiplyPoint3x4(v))).ToArray();
            Bounds bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            Vector3 pivot = rig.transform.InverseTransformPoint(body.position);
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"bounds center={bounds.center:F6} extents={bounds.extents:F6} pivot={pivot:F6}");
            lines.AppendLine($"radius min={points.Min(p => Vector3.Distance(p,pivot)):F6} max={points.Max(p => Vector3.Distance(p,pivot)):F6}");
            lines.AppendLine($"renderer scale={skin.transform.lossyScale} raw bounds={skin.sharedMesh.bounds} baked={mesh.bounds}");
            foreach (var b in AnimationUtility.GetCurveBindings(Butcher25DSetup.Clip("Roll_N")))
                if (b.path.EndsWith("BodyRoll"))
                {
                    var c = AnimationUtility.GetEditorCurve(Butcher25DSetup.Clip("Roll_N"), b);
                    lines.AppendLine($"curve {b.propertyName}: {c.keys.Min(k=>k.value):F6}..{c.keys.Max(k=>k.value):F6}");
                }
            File.WriteAllText("Temp/HookWarsAutomation/body-measurement.txt", lines.ToString());
        }
        finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(rig); }
    }
}
