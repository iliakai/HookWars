using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Presentation owns detached transforms only. Never parent a 3D model to a logical
// XY transform: its Z rotation/scale belongs to Physics2D and legacy graphics.
[DefaultExecutionOrder(1000)]
public class ArenaPresentation25D : MonoBehaviour
{
    public enum VisualKind { Player, Enemy, Sheep, Cow, Pig, Carcass, Factory, Shop, Farm, Wall, Hook }
    [System.Serializable]
    public struct ModelOverride
    {
        public VisualKind kind;
        public GameObject prefab;
    }
    [SerializeField] private RenderPipelineAsset pipeline;
    [SerializeField] private Material litMaterial;
    [SerializeField] private Material chainMaterial;
    [SerializeField] private ModelOverride[] models = new ModelOverride[0];
    private RenderPipelineAsset previousPipeline;
    private bool pipelineApplied;
    private readonly Dictionary<Color, Material> palette = new Dictionary<Color, Material>();
    private readonly MaterialPropertyBlock previewProperties = new MaterialPropertyBlock();
    private readonly Dictionary<Component, GameObject> views = new Dictionary<Component, GameObject>();
    private readonly Dictionary<HookAbility, LineRenderer> chains = new Dictionary<HookAbility, LineRenderer>();
    private readonly Dictionary<SpriteRenderer, bool> hidden = new Dictionary<SpriteRenderer, bool>();
    private readonly List<Component> removed = new List<Component>();
    private readonly List<SpriteRenderer> removedSprites = new List<SpriteRenderer>();
    private readonly List<Material> materials = new List<Material>();
    private readonly Dictionary<SpriteRenderer, GameObject> terrain = new Dictionary<SpriteRenderer, GameObject>();
    private GameObject generated;
    private GameObject preview;
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (generated != null) generated.SetActive(true);
        previousPipeline = QualitySettings.renderPipeline;
        pipelineApplied = true;
        if (pipeline != null) QualitySettings.renderPipeline = pipeline;
    }
    private void Start()
    {
        generated = new GameObject("Generated 25D visuals");
        generated.transform.SetParent(transform, false);
        // Bounds come from ArenaLayout's already resized logical surfaces.
        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>())
        {
            if (sprite.gameObject.scene != gameObject.scene) continue;
            string label = sprite.name.ToLowerInvariant();
            if (label != "left bank" && label != "right bank" && label != "river" &&
                label != "left mountains" && label != "right mountains") continue;
            Bounds bounds = sprite.bounds;
            bool mountain = label.Contains("mountains");
            Color color = mountain ? new Color(0.26f, 0.29f, 0.33f) : label == "river"
                ? new Color(0.12f, 0.4f, 0.65f) : new Color(0.25f, 0.43f, 0.23f);
            float height = mountain ? 1.5f : 0.15f;
            GameObject ground = Primitive(label, PrimitiveType.Cube, generated.transform,
                new Vector3(bounds.size.x, height, bounds.size.y), color);
            // Bank and water top are both exactly Y=0; adjacent edges do not overlap.
            ground.transform.position = ArenaSpace25D.ToWorld(bounds.center, mountain ? height / 2 : -height / 2);
            terrain.Add(sprite, ground);
        }
        preview = Primitive("Cell preview", PrimitiveType.Cube, generated.transform, new Vector3(0.98f, 0.04f, 0.98f), Color.green);
        preview.SetActive(false);
    }
    private GameObject Primitive(string label, PrimitiveType shape, Transform parent, Vector3 size, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = label; part.transform.SetParent(parent, false); part.transform.localScale = size;
        Collider collider = part.GetComponent<Collider>();
        // Remove immediately so no frame can contain duplicate 3D interaction shapes.
        if (collider != null) DestroyImmediate(collider);
        if (!palette.TryGetValue(color, out Material material))
        {
            material = new Material(litMaterial); material.color = color;
            materials.Add(material); palette.Add(color, material);
        }
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }
    private GameObject View(Component source, PrimitiveType shape, Vector3 size, Color color)
    {
        if (!views.TryGetValue(source, out GameObject view))
        {
            view = new GameObject(source.name + " 25D"); view.transform.SetParent(generated.transform, false);
            GameObject prefab = ModelFor(source);
            if (prefab != null && (prefab.GetComponentsInChildren<Collider>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Collider2D>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Rigidbody>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Rigidbody2D>(true).Length > 0))
            {
                Debug.LogError("25D model must contain no physics components: " + prefab.name, prefab);
                prefab = null;
            }
            GameObject model;
            if (prefab != null)
            {
                model = Instantiate(prefab, view.transform, false); model.name = "Model";
                model.transform.localPosition = Vector3.up * 0.03f;
                model.transform.localRotation = Quaternion.identity;
            }
            else
            {
                model = Primitive("Model", shape, view.transform, size, color);
                float halfHeight = shape == PrimitiveType.Capsule ? size.y : size.y / 2;
                model.transform.localPosition = Vector3.up * (halfHeight + 0.03f);
            }
            views.Add(source, view);
        }
        view.SetActive(source.gameObject.activeInHierarchy);
        view.transform.position = ArenaSpace25D.ToWorld(source.transform.position);
        return view;
    }
    private GameObject ModelFor(Component source)
    {
        VisualKind kind;
        if (source is Health) kind = source.GetComponent<PlayerMovement>() != null ? VisualKind.Player : VisualKind.Enemy;
        else if (source is Animal animal) kind = animal.MassKg >= 10 ? VisualKind.Cow : animal.MassKg >= 6 ? VisualKind.Pig : VisualKind.Sheep;
        else if (source is Carcass) kind = VisualKind.Carcass;
        else if (source is BoneWall) kind = VisualKind.Wall;
        else if (source is AnimalFarm) kind = VisualKind.Farm;
        else if (source is MeatShop) kind = VisualKind.Shop;
        else if (source is GameBuilding) kind = VisualKind.Factory;
        else kind = VisualKind.Hook;
        if (models != null) foreach (ModelOverride model in models) if (model.kind == kind) return model.prefab;
        return null;
    }
    private void LateUpdate()
    {
        if (generated == null) return;
        foreach (var pair in terrain)
        {
            Bounds bounds = pair.Key.bounds;
            bool mountain = pair.Key.name.ToLowerInvariant().Contains("mountains");
            float height = mountain ? 1.5f : 0.15f;
            pair.Value.transform.position = ArenaSpace25D.ToWorld(bounds.center, mountain ? height / 2 : -height / 2);
            pair.Value.transform.localScale = new Vector3(bounds.size.x, height, bounds.size.y);
        }
        // Discovery includes objects spawned by unchanged farms, death and building code.
        // Prototype scan deliberately prioritises compatibility; replace with a registry
        // before large populations, without modifying the gameplay coordinate contract.
        foreach (Health hero in FindObjectsByType<Health>(FindObjectsInactive.Include))
        {
            if (hero.gameObject.scene != gameObject.scene) continue;
            Color color = hero.GetComponent<PlayerMovement>() != null ? new Color(0.2f, 0.65f, 1) : new Color(0.95f, 0.24f, 0.18f);
            GameObject view = View(hero, PrimitiveType.Capsule, new Vector3(0.8f, 0.9f, 0.8f), color);
            view.SetActive(hero.gameObject.activeInHierarchy && !hero.IsDead);
            if (view.transform.Find("Facing") == null)
            {
                GameObject arrow = Primitive("Facing", PrimitiveType.Cube, view.transform, new Vector3(0.16f, 0.12f, 0.85f), Color.yellow);
                arrow.transform.localPosition = new Vector3(0, 0.12f, 0.8f);
                GameObject tip = Primitive("Tip", PrimitiveType.Cube, view.transform, new Vector3(0.35f, 0.12f, 0.35f), Color.yellow);
                tip.transform.localPosition = new Vector3(0, 0.12f, 1.15f);
                tip.transform.localRotation = Quaternion.Euler(0, 45, 0);
                GameObject cargo = Primitive("Cargo", PrimitiveType.Cube, view.transform, Vector3.one * 0.45f, new Color(1, 0.5f, 0.08f));
                cargo.transform.localPosition = new Vector3(0, 1.3f, -0.45f);
                GameObject hp = Primitive("Health", PrimitiveType.Cube, view.transform, new Vector3(0.9f, 0.09f, 0.12f), Color.green);
                hp.transform.localPosition = Vector3.up * 2.1f;
            }
            PlayerAim aim = hero.GetComponentInChildren<PlayerAim>();
            Vector2 direction = aim != null ? aim.AimDirection : (Vector2)(FindPlayerPosition() - hero.transform.position);
            view.transform.rotation = ArenaSpace25D.Facing(direction);
            CarcassCarrier carrier = hero.GetComponent<CarcassCarrier>();
            view.transform.Find("Cargo").gameObject.SetActive(carrier != null && carrier.Count > 0);
            view.transform.Find("Health").localScale = new Vector3(0.9f * hero.HealthFraction, 0.09f, 0.12f);
        }
        foreach (Animal animal in FindObjectsByType<Animal>(FindObjectsInactive.Include))
        {
            if (animal.gameObject.scene != gameObject.scene) continue;
            GameObject view = View(animal, PrimitiveType.Sphere, new Vector3(0.65f, 0.65f, 0.9f),
                animal.MassKg >= 10 ? new Color(0.6f, 0.48f, 0.3f) : animal.MassKg >= 6 ? new Color(0.95f, 0.55f, 0.6f) : Color.white);
            view.SetActive(animal.gameObject.activeInHierarchy && !animal.IsDead);
        }
        foreach (Carcass carcass in FindObjectsByType<Carcass>(FindObjectsInactive.Include))
            if (carcass.gameObject.scene == gameObject.scene)
                View(carcass, PrimitiveType.Cube, new Vector3(0.65f, 0.25f, 0.5f), new Color(0.65f, 0.22f, 0.2f));
        foreach (GameBuilding building in FindObjectsByType<GameBuilding>(FindObjectsInactive.Include))
        {
            if (building.gameObject.scene != gameObject.scene) continue;
            Color color = building is BoneWall ? new Color(0.85f, 0.8f, 0.6f) : building is AnimalFarm ? new Color(0.6f, 0.8f, 0.3f)
                : building is MeatShop ? new Color(0.9f, 0.4f, 0.25f) : new Color(0.45f, 0.5f, 0.65f);
            View(building, PrimitiveType.Cube, new Vector3(1, building is BoneWall ? 0.85f : 1.3f, 1), color);
        }
        foreach (HookAbility hook in FindObjectsByType<HookAbility>())
        {
            if (hook.gameObject.scene != gameObject.scene) continue;
            if (!chains.TryGetValue(hook, out LineRenderer chain))
            {
                GameObject line = new GameObject(hook.name + " chain 25D"); line.transform.SetParent(generated.transform, false);
                chain = line.AddComponent<LineRenderer>(); chain.sharedMaterial = chainMaterial;
                chain.startWidth = chain.endWidth = 0.08f; chain.useWorldSpace = true;
                chain.shadowCastingMode = ShadowCastingMode.Off; chains.Add(hook, chain);
            }
            chain.positionCount = hook.ChainPointCount;
            for (int i = 0; i < hook.ChainPointCount; i++) chain.SetPosition(i, ArenaSpace25D.ToWorld(hook.GetChainPoint(i), 0.65f));
            if (hook.ActiveHook != null)
            {
                GameObject view = View(hook.ActiveHook, PrimitiveType.Cube, new Vector3(0.3f, 0.25f, 0.65f), Color.yellow);
                view.transform.position = ArenaSpace25D.ToWorld(hook.ActiveHook.position, 0.5f);
                // Logical HookVisual uses a 270 degree art offset. Its local +Y is forward.
                Vector2 forward = hook.ActiveHook.up;
                view.transform.rotation = ArenaSpace25D.Facing(forward);
            }
        }
        PlayerBuilding builder = FindAnyObjectByType<PlayerBuilding>();
        SpriteRenderer marker = builder != null ? builder.Preview : null;
        preview.SetActive(marker != null);
        if (marker != null)
        {
            preview.transform.position = ArenaSpace25D.ToWorld(marker.transform.position, 0.035f);
            previewProperties.SetColor("_BaseColor", marker.color);
            preview.GetComponent<Renderer>().SetPropertyBlock(previewProperties);
        }
        // forceRenderingOff survives old scripts/Animator toggling enabled and color.
        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            if (sprite.gameObject.scene == gameObject.scene)
            {
                if (!hidden.ContainsKey(sprite)) hidden.Add(sprite, sprite.forceRenderingOff);
                sprite.forceRenderingOff = true;
            }
        removed.Clear();
        foreach (var pair in views) if (pair.Key == null) { Destroy(pair.Value); removed.Add(pair.Key); }
        foreach (Component key in removed) views.Remove(key);
        removedSprites.Clear();
        foreach (var pair in hidden) if (pair.Key == null) removedSprites.Add(pair.Key);
        foreach (SpriteRenderer sprite in removedSprites) hidden.Remove(sprite);
    }
    private Vector3 FindPlayerPosition()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        return player != null ? player.transform.position : Vector3.zero;
    }
    private void OnDisable()
    {
        if (!pipelineApplied) return;
        pipelineApplied = false;
        QualitySettings.renderPipeline = previousPipeline;
        if (generated != null) generated.SetActive(false);
        foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        hidden.Clear();
    }
    private void OnDestroy()
    {
        if (generated != null) Destroy(generated);
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
