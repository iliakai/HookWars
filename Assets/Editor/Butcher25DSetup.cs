using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Butcher25DSetup
{
    public const string Root = "Assets/HookWarsButcher";
    public const string ModelPath = Root + "/Models/Butcher_Animated.fbx";
    public const string Adapted = Root + "/Integration";
    public const string HeroPrefab = Adapted + "/Butcher25D.prefab";
    public const string HookPrefab = Adapted + "/Hook25D.prefab";
    public const string CarcassPrefab = Adapted + "/ButcherCarcass25D.prefab";
    public static readonly string[] ClipNames = { "Idle", "Roll_N", "Roll_NE", "Roll_E", "Roll_SE", "Roll_S", "Roll_SW", "Roll_W", "Roll_NW", "Axe_Attack", "Hook_Cast" };
    public static bool Matches(string actual, string suffix) => actual == suffix || actual.EndsWith("|" + suffix, StringComparison.Ordinal);
    public static AnimationClip Clip(string suffix)
    {
        var matches = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__") && Matches(c.name, suffix)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Ambiguous/missing clip " + suffix + ": " + string.Join(", ", matches.Select(c => c.name)));
        return matches[0];
    }
    [MenuItem("HookWars/25D/Butcher/Inspect imported FBX")]
    public static void Inspect()
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing butcher FBX at " + ModelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false; importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var settings = importer.defaultClipAnimations;
        foreach (string expected in ClipNames)
        {
            var found = settings.Where(s => Matches(s.name, expected)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Ambiguous/missing take: " + expected);
            found[0].loopTime = expected == "Idle" || expected.StartsWith("Roll_");
            found[0].loopPose = false; found[0].lockRootHeightY = true;
            found[0].lockRootPositionXZ = true; found[0].lockRootRotation = true;
            found[0].events = new AnimationEvent[0];
        }
        importer.clipAnimations = settings; importer.SaveAndReimport();
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        try
        {
            var transforms = instance.GetComponentsInChildren<Transform>(true);
            var lines = new System.Text.StringBuilder();
            foreach (Transform t in transforms)
                lines.AppendLine("TRANSFORM " + AnimationUtility.CalculateTransformPath(t, instance.transform) + " position=" + t.localPosition.ToString("F4") + " rotation=" + t.localEulerAngles.ToString("F2") + " scale=" + t.localScale.ToString("F4"));
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                lines.AppendLine("RENDERER " + r.name + " type=" + r.GetType().Name + " bounds=" + r.bounds + " materials=" + string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "NULL")));
                if (r is SkinnedMeshRenderer skin)
                {
                    lines.AppendLine(" SKIN_BONES " + string.Join(",", skin.bones.Select(b => b.name)));
                    lines.AppendLine(" WEIGHTED_BONES " + string.Join(",", skin.sharedMesh.boneWeights.Where(w => w.weight0 > 0).Select(w => skin.bones[w.boneIndex0].name).Distinct()));
                }
            }
            foreach (string name in ClipNames)
            {
                AnimationClip clip = Clip(name);
                lines.AppendLine("CLIP " + clip.name + " duration=" + clip.length + " fps=" + clip.frameRate + " loop=" + clip.isLooping);
                foreach (string path in AnimationUtility.GetCurveBindings(clip).Select(b => b.path).Distinct()) lines.AppendLine(" BINDING " + path);
                if (name.StartsWith("Roll_"))
                {
                    clip.SampleAnimation(instance, 0.1f); Quaternion from = Bone(instance, "BodyRoll").rotation;
                    clip.SampleAnimation(instance, 0.11f); Quaternion to = Bone(instance, "BodyRoll").rotation;
                    (to * Quaternion.Inverse(from)).ToAngleAxis(out float degrees, out Vector3 axis);
                    lines.AppendLine(" ROTATION_DELTA " + name + " degrees=" + degrees + " axis=" + axis.ToString("F4"));
                }
                if (name == "Axe_Attack" || name == "Hook_Cast")
                    foreach (float time in new[] { 0f, 0.2f, name == "Axe_Attack" ? 0.4f : 7f / 30f, 0.6f })
                    {
                        clip.SampleAnimation(instance, time); Transform bone = Bone(instance, name == "Axe_Attack" ? "AxePivot" : "ChainArm");
                        lines.AppendLine(" ACTION_POSE " + name + " time=" + time + " rotation=" + bone.localEulerAngles.ToString("F2") + " position=" + bone.localPosition.ToString("F4"));
                    }
            }
            Directory.CreateDirectory("Temp/HookWarsAutomation");
            File.WriteAllText("Temp/HookWarsAutomation/butcher-import.txt", lines.ToString());
            Debug.Log("HOOKWARS_BUTCHER_INSPECT_PASS: 11 unambiguous clips imported; report in Temp/HookWarsAutomation/butcher-import.txt");
        }
        finally { Object.DestroyImmediate(instance); }
    }
    [MenuItem("HookWars/25D/Butcher/Setup Arena25D butcher")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
        Inspect();
        if (!AssetDatabase.IsValidFolder(Adapted)) AssetDatabase.CreateFolder(Root, "Integration");
        var materials = new System.Collections.Generic.Dictionary<string, Material>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader missing");
        foreach (string texturePath in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            string name = Path.GetFileNameWithoutExtension(texturePath), path = Adapted + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", name.Contains("Steel") || name.Contains("Iron") ? 0.35f : 0);
            EditorUtility.SetDirty(material); materials.Add(name, material);
        }
        GameObject prefabRoot = new GameObject("Butcher25D");
        try
        {
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rig.name = "RigVisual"; rig.transform.SetParent(prefabRoot.transform, false); rig.transform.localScale = Vector3.one * 0.48f;
            foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && materials.TryGetValue(m.name, out Material mapped) ? mapped : throw new InvalidOperationException("Unmapped material " + m?.name)).ToArray();
            Transform body = Bone(rig, "BodyRoll");
            var skinOrientation = new GameObject("SkinOrientation").transform; skinOrientation.SetParent(body, false);
            foreach (SkinnedMeshRenderer skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.bones = skin.bones.Select(b => b == body ? skinOrientation : b).ToArray();
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
            }
            SkinnedMeshRenderer bodySkin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name.StartsWith("02_Body_"));
            if (!bodySkin.bones.Contains(skinOrientation)) throw new InvalidOperationException("Body skin is not attached to BodyRoll");
            float radius = CenteredBall(bodySkin, skinOrientation);
            // One bind-pose placement, independent of animated pose and weapons.
            rig.transform.position += Vector3.up * (radius + 0.015f - body.position.y);
            Animator animator = rig.GetComponent<Animator>() ?? rig.AddComponent<Animator>();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AnimatorController controller = Controller(rig);
            animator.runtimeAnimatorController = controller;
            ButcherVisual25D driver = prefabRoot.AddComponent<ButcherVisual25D>();
            SerializedObject data = new SerializedObject(driver);
            Set(data, "animator", animator); Set(data, "bodyRoll", body); Set(data, "skinOrientation", skinOrientation);
            Set(data, "hookSocket", Bone(rig, "HookSocket")); Set(data, "visualRig", rig.transform); Set(data, "bodyMesh", bodySkin);
            Set(data, "restingHook", rig.GetComponentsInChildren<Renderer>(true).Single(r => r.name.StartsWith("04_Hook_Metal")));
            data.FindProperty("effectiveRadius").floatValue = radius; data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, HeroPrefab);
            SaveCarcass(rig);
        }
        finally { Object.DestroyImmediate(prefabRoot); }
        SaveProjectile(materials["Cleaver_Steel"]);
        Arena25DSetup.Configure();
        foreach (Health hero in Object.FindObjectsByType<Health>())
            if (hero.GetComponent<HookVictim>() != null && hero.GetComponent<HeroActionTiming25D>() == null) hero.gameObject.AddComponent<HeroActionTiming25D>();
        var presentation = Object.FindAnyObjectByType<ArenaPresentation25D>();
        SerializedObject sceneData = new SerializedObject(presentation);
        var models = sceneData.FindProperty("models"); models.arraySize = 4;
        int[] kinds = { 0, 1, 5, 10 }; string[] paths = { HeroPrefab, HeroPrefab, CarcassPrefab, HookPrefab };
        for (int i = 0; i < kinds.Length; i++)
        {
            var entry = models.GetArrayElementAtIndex(i); entry.FindPropertyRelative("kind").intValue = kinds[i];
            entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
        }
        sceneData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(presentation.gameObject.scene); AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_BUTCHER_SETUP_PASS: model scale 0.48, radius 0.4992m; four layers; no demo/3D physics");
    }
    private static Transform Bone(GameObject rig, string name)
    {
        var matches = rig.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Missing/ambiguous bone " + name);
        return matches[0];
    }
    private static float CenteredBall(SkinnedMeshRenderer skin, Transform bone)
    {
        int index = Array.IndexOf(skin.bones, bone);
        Mesh source = skin.sharedMesh;
        if (source.boneWeights.Any(w => w.boneIndex0 != index || w.weight0 < 0.9999f))
            throw new InvalidOperationException("Ball must be rigidly weighted to BodyRoll");
        Matrix4x4 toBone = source.bindposes[index], fromBone = toBone.inverse;
        Vector3[] vertices = source.vertices;
        float radius = vertices.Max(v => toBone.MultiplyPoint3x4(v).magnitude);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = toBone.MultiplyPoint3x4(vertices[i]);
            if (p.sqrMagnitude < 1e-12f) throw new InvalidOperationException("Ball vertex at pivot");
            vertices[i] = fromBone.MultiplyPoint3x4(p.normalized * radius);
        }
        Mesh copy = Object.Instantiate(source); copy.name = "CenteredBody";
        copy.vertices = vertices; copy.RecalculateNormals(); copy.RecalculateTangents(); copy.RecalculateBounds();
        string path = Adapted + "/CenteredBody.asset";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { saved = copy; AssetDatabase.CreateAsset(saved, path); }
        else { EditorUtility.CopySerialized(copy, saved); Object.DestroyImmediate(copy); EditorUtility.SetDirty(saved); }
        skin.sharedMesh = saved;
        float worldRadius = bone.TransformVector(Vector3.up * radius).magnitude;
        Debug.Log($"HOOKWARS_BODY_GEOMETRY: radius={worldRadius:F6}m, pivot={bone.position:F6}; original mesh/FBX preserved");
        return worldRadius;
    }
    private static void Set(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
    private static AnimatorController Controller(GameObject rig)
    {
        string path = Adapted + "/Butcher25D.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.parameters = new[] { new AnimatorControllerParameter { name = "RollRate", type = AnimatorControllerParameterType.Float },
            new AnimatorControllerParameter { name = "AxeSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 } };
        string[] layerNames = { "Idle", "Body", "Axe", "Hook" };
        for (int i = controller.layers.Length; i < 4; i++) controller.AddLayer(layerNames[i]);
        var layers = controller.layers;
        for (int i = 0; i < 4; i++)
        {
            layers[i].name = layerNames[i]; layers[i].stateMachine.name = layerNames[i]; layers[i].defaultWeight = 1;
            if (i > 0) layers[i].avatarMask = Mask(rig, layerNames[i], i == 1 ? "BodyRoll" : i == 2 ? "AxePivot" : "ChainArm");
            var machine = layers[i].stateMachine;
            string[] names = i == 0 ? new[] { "Idle" } : i == 1 ? ClipNames.Where(n => n.StartsWith("Roll_")).ToArray() :
                new[] { "Rest", i == 2 ? "Axe_Attack" : "Hook_Cast" };
            foreach (string name in names)
            {
                AnimatorState state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == name) ?? machine.AddState(name);
                state.motion = name.StartsWith("Roll_") ? AdaptedRoll(name) : Clip(name == "Rest" ? "Idle" : name); state.writeDefaultValues = false;
                state.speed = 1; state.speedParameterActive = i == 1 || i == 2;
                state.speedParameter = i == 1 ? "RollRate" : i == 2 ? "AxeSpeed" : "";
            }
            machine.defaultState = machine.states.First(s => s.state.name == names[0]).state;
        }
        controller.layers = layers; EditorUtility.SetDirty(controller); return controller;
    }
    private static AnimationClip AdaptedRoll(string name)
    {
        // Verified imported FBX deltas: N=+X, E=+Z, but Unity +X travel
        // needs rotation about -Z. Correct the exported roll basis in asset data,
        // preserving each named authored roll and its phase. Other branches are
        // evaluated independently by the Idle and arm layers.
        // Conjugation by X(180) maps quaternion (x,y,z,w) to (x,-y,-z,w).
        AnimationClip copy = Object.Instantiate(Clip(name)); copy.name = name; copy.hideFlags = HideFlags.None;
        foreach (var binding in AnimationUtility.GetCurveBindings(copy))
        {
            // Parent/head/weapon curves are supplied by the Idle and arm layers.
            // Do not duplicate their masked-out samples in eight runtime assets.
            if (binding.path != "ButcherRig/Root/BodyRoll") { AnimationUtility.SetEditorCurve(copy, binding, null); continue; }
            bool rotation = binding.propertyName.StartsWith("m_LocalRotation.") || binding.propertyName.StartsWith("localEulerAngles");
            if (!rotation ||
                !(binding.propertyName.EndsWith(".y") || binding.propertyName.EndsWith(".z"))) continue;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(copy, binding);
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            { keys[i].value = -keys[i].value; keys[i].inTangent = -keys[i].inTangent; keys[i].outTangent = -keys[i].outTangent; }
            curve.keys = keys; AnimationUtility.SetEditorCurve(copy, binding, curve);
        }
        copy.EnsureQuaternionContinuity();
        string path = Adapted + "/" + name + ".anim";
        AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (saved == null) { saved = copy; AssetDatabase.CreateAsset(saved, path); }
        else { EditorUtility.CopySerialized(copy, saved); Object.DestroyImmediate(copy); EditorUtility.SetDirty(saved); }
        return saved;
    }
    private static AvatarMask Mask(GameObject rig, string name, string branch)
    {
        string path = Adapted + "/" + name + ".mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null) { mask = new AvatarMask { name = name }; AssetDatabase.CreateAsset(mask, path); }
        Transform[] bones = rig.GetComponentsInChildren<Transform>(true); mask.transformCount = bones.Length;
        string active = AnimationUtility.CalculateTransformPath(Bone(rig, branch), rig.transform);
        for (int i = 0; i < bones.Length; i++)
        {
            string bonePath = AnimationUtility.CalculateTransformPath(bones[i], rig.transform);
            mask.SetTransformPath(i, bonePath); mask.SetTransformActive(i, bonePath == active || bonePath.StartsWith(active + "/"));
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        EditorUtility.SetDirty(mask); return mask;
    }
    private static void SaveCarcass(GameObject rig)
    {
        Clip("Idle").SampleAnimation(rig, 0);
        var root = new GameObject("ButcherCarcass25D"); var pose = new GameObject("Static pose"); pose.transform.SetParent(root.transform, false);
        try
        {
            foreach (SkinnedMeshRenderer skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("01_Head") || s.name.StartsWith("02_Body")))
            {
                string path = Adapted + "/Corpse_" + skin.name + ".asset";
                var baked = new Mesh { name = "Corpse " + skin.name }; skin.BakeMesh(baked);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = baked; AssetDatabase.CreateAsset(mesh, path); }
                else { EditorUtility.CopySerialized(baked, mesh); Object.DestroyImmediate(baked); EditorUtility.SetDirty(mesh); }
                var part = new GameObject(skin.name); part.transform.SetParent(pose.transform, false);
                part.transform.position = skin.transform.position; part.transform.rotation = skin.transform.rotation; part.transform.localScale = skin.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            }
            pose.transform.localRotation = Quaternion.Euler(70, 0, 0);
            Bounds bounds = BoundsOf(root); pose.transform.localScale = Vector3.one * (0.65f / Mathf.Max(bounds.size.x, bounds.size.z));
            bounds = BoundsOf(root); pose.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y + 0.015f, -bounds.center.z);
            PrefabUtility.SaveAsPrefabAsset(root, CarcassPrefab);
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static void SaveProjectile(Material material)
    {
        var root = new GameObject("Hook25D");
        GameObject model = null;
        var meshes = new System.Collections.Generic.List<Mesh>();
        try
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/HookProjectile.fbx"));
            var parts = new System.Collections.Generic.List<CombineInstance>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh); meshes.Add(mesh); }
                else mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                // HookProjectile.fbx is already exported along Unity +Z.
                for (int i = 0; i < mesh.subMeshCount; i++) parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = renderer.localToWorldMatrix });
            }
            var combined = new Mesh { name = "HookProjectile FBX static geometry" }; combined.CombineMeshes(parts.ToArray(), true, true);
            Bounds bounds = combined.bounds; float scale = 0.45f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            combined.vertices = combined.vertices.Select(v => (v - bounds.center) * scale).ToArray(); combined.RecalculateBounds();
            string path = Adapted + "/HookProjectileMesh.asset";
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { saved = combined; AssetDatabase.CreateAsset(saved, path); }
            else { EditorUtility.CopySerialized(combined, saved); Object.DestroyImmediate(combined); EditorUtility.SetDirty(saved); }
            root.AddComponent<MeshFilter>().sharedMesh = saved; root.AddComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, HookPrefab);
        }
        finally { if (model != null) Object.DestroyImmediate(model); foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); }
    }
    private static Bounds BoundsOf(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Model contains no renderers");
        Bounds bounds = renderers[0].bounds; foreach (Renderer r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); return bounds;
    }
}

