using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Presentation owns detached transforms only. Never parent a 3D model to a logical
// XY transform: its Z rotation/scale belongs to Physics2D and legacy graphics.
[DefaultExecutionOrder(1000)]
public class ArenaPresentation25D : MonoBehaviour
{
    public enum VisualKind { Player, Enemy, Sheep, Cow, Pig, Carcass, Factory, Shop, Farm, Wall, Hook, SheepCarcass, CowCarcass, PigCarcass, Rock }
    [System.Serializable]
    public struct ModelOverride
    {
        public VisualKind kind;
        public GameObject prefab;
    }
    [SerializeField] private RenderPipelineAsset pipeline;
    [SerializeField] private Material litMaterial;
    [SerializeField] private Mesh physicalChainMesh;
    [SerializeField] private Material physicalChainMaterial;
    [SerializeField, Min(.1f)] private float chainLinkScale=1;
    [SerializeField, Min(.1f)] private float chainThickness=1;
    [SerializeField, Range(0,.2f)] private float chainSlackRatio=.025f;
    private Dictionary<HookAbility,HookChain25D> physicalChains=new Dictionary<HookAbility,HookChain25D>();
    [SerializeField] private ModelOverride[] models = new ModelOverride[0];
    private RenderPipelineAsset previousPipeline;
    private bool pipelineApplied;
    private Dictionary<Color, Material> palette = new Dictionary<Color, Material>();
    // This allocates native Unity state: never construct it in a field initializer.
    private MaterialPropertyBlock previewProperties;
    private Dictionary<Component, GameObject> views = new Dictionary<Component, GameObject>();
    private Dictionary<HookAbility, GameObject> projectiles = new Dictionary<HookAbility, GameObject>();
    private List<Component> removed = new List<Component>();
    private List<Material> materials = new List<Material>();
    private Dictionary<string, GameObject> terrain = new Dictionary<string, GameObject>();
    private GameObject generated;
    private GameObject preview;
    private GameObject previewModel;
    private GameBuilding previewPrefab;
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        bool lostCaches = terrain == null || views == null || palette == null || terrain.Count == 0;
        palette ??= new Dictionary<Color, Material>();
        previewProperties ??= new MaterialPropertyBlock();
        views ??= new Dictionary<Component, GameObject>();
        projectiles ??= new Dictionary<HookAbility, GameObject>();
        removed ??= new List<Component>();
        materials ??= new List<Material>(); terrain ??= new Dictionary<string, GameObject>();
        if (lostCaches && generated != null)
        {
            DestroyGenerated(); views.Clear(); physicalChains.Clear(); projectiles.Clear(); terrain.Clear(); palette.Clear();
            Start();
        }
        if (generated != null) generated.SetActive(true);
        previousPipeline = QualitySettings.renderPipeline;
        pipelineApplied = true;
        if (pipeline != null) QualitySettings.renderPipeline = pipeline;
    }
    private void Start()
    {
        if (generated != null) return;
        physicalChains ??= new Dictionary<HookAbility,HookChain25D>();
        generated = new GameObject("Generated 25D visuals");
        generated.transform.SetParent(transform, false);
        // Bounds come from ArenaLayout's already resized logical surfaces.
        foreach (string label in new[]{"left bank","right bank","river","left mountains","right mountains"})
        {
            Bounds bounds = TerrainBounds(label);
            bool mountain = label.Contains("mountains");
            Color color = mountain ? new Color(0.26f, 0.29f, 0.33f) : label == "river"
                ? new Color(0.12f, 0.4f, 0.65f) : new Color(0.25f, 0.43f, 0.23f);
            float height = mountain ? 1.5f : 0.15f;
            GameObject ground = Primitive(label, PrimitiveType.Cube, generated.transform,
                new Vector3(bounds.size.x, height, bounds.size.y), color);
            // Bank and water top are both exactly Y=0; adjacent edges do not overlap.
            ground.transform.position = ArenaSpace25D.ToWorld(bounds.center, mountain ? height / 2 : -height / 2);
            terrain.Add(label, ground);
        }
        preview = new GameObject("Cell preview");preview.transform.SetParent(generated.transform,false);
        var cell=Primitive("Cell highlight",PrimitiveType.Cube,preview.transform,new Vector3(.98f,.004f,.98f),Color.green);cell.transform.localPosition=Vector3.up*.003f;
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
            material = new Material(litMaterial) { name = "Arena25D Runtime " + litMaterial.name, hideFlags = HideFlags.DontSave }; material.color = color;
            materials.Add(material); palette.Add(color, material);
        }
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }
    private GameObject View(Component source)
    {
        if (!views.TryGetValue(source, out GameObject view))
        {
            GameObject prefab = ModelFor(source);
            if(prefab==null)throw new System.InvalidOperationException("Missing 3D model for "+source.name);
            if (prefab.GetComponentsInChildren<Collider>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Collider2D>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Rigidbody>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Rigidbody2D>(true).Length > 0 || prefab.GetComponentsInChildren<SpriteRenderer>(true).Length>0)
                throw new System.InvalidOperationException("3D model must contain no gameplay physics or sprites: "+prefab.name);
            foreach(var script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if(script==null||!(script is ButcherVisual25D||script is BuildingPresentation25D||script is AnimalPresentation25D||
                    script is HookWars.Animals.AnimalMotion||script is HookWars.Buildings.BuildingVisualController))
                    throw new System.InvalidOperationException("Unexpected script in visual prefab: "+prefab.name);
            view = new GameObject(source.name + " 25D"); view.transform.SetParent(generated.transform, false);
            GameObject model=Instantiate(prefab, view.transform, false);model.name="Model";
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            views.Add(source, view);
            if (source is Health hero)
            {
                ButcherVisual25D butcher = model.GetComponent<ButcherVisual25D>();
                if (butcher != null) butcher.Bind(hero);
            }
            if (source is GameBuilding building) model.GetComponent<BuildingPresentation25D>()?.Bind(building);
            if (source is Animal animal) model.GetComponent<AnimalPresentation25D>()?.Bind(animal);
        }
        view.SetActive(source.gameObject.activeInHierarchy);
        view.transform.position = ArenaSpace25D.ToWorld(source.transform.position);
        return view;
    }
    public GameObject ModelFor(Component source)
    {
        VisualKind kind;
        if (source is Health) kind = source.GetComponent<PlayerMovement>() != null ? VisualKind.Player : VisualKind.Enemy;
        else if (source is Animal animal) kind = animal.Kind == Animal.Species.Cow ? VisualKind.Cow : animal.Kind == Animal.Species.Pig ? VisualKind.Pig : VisualKind.Sheep;
        else if (source is Carcass carcass)
        {
            var species=carcass.AnimalSpecies ?? (carcass.MassKg>=10?Animal.Species.Cow:carcass.MassKg>=6?Animal.Species.Pig:Animal.Species.Sheep);
            kind=carcass.IsHeroCarcass?VisualKind.Carcass:species==Animal.Species.Cow?VisualKind.CowCarcass:species==Animal.Species.Pig?VisualKind.PigCarcass:VisualKind.SheepCarcass;
        }
        else if (source is PermanentRock) kind=VisualKind.Rock;
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
            Bounds bounds = TerrainBounds(pair.Key);
            bool mountain = pair.Key.Contains("mountains");
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
            GameObject view = View(hero);
            view.SetActive(hero.gameObject.activeInHierarchy && !hero.IsDead);
            if (view.transform.Find("Facing") == null)
            {
                GameObject arrow = Primitive("Facing", PrimitiveType.Cube, view.transform, new Vector3(0.16f, 0.12f, 0.85f), Color.yellow);
                arrow.transform.localPosition = new Vector3(0, 0.12f, 0.8f);
                GameObject tip = Primitive("Tip", PrimitiveType.Cube, view.transform, new Vector3(0.35f, 0.12f, 0.35f), Color.yellow);
                tip.transform.localPosition = new Vector3(0, 0.12f, 1.15f);
                tip.transform.localRotation = Quaternion.Euler(0, 45, 0);
                GameObject cargo = new GameObject("Cargo");cargo.transform.SetParent(view.transform,false);
                cargo.transform.localPosition = new Vector3(0, .25f, -.8f);
                cargo.AddComponent<CargoPresentation25D>().Bind(hero.GetComponent<CarcassCarrier>(),this);
                GameObject hp = Primitive("Health", PrimitiveType.Cube, view.transform, new Vector3(0.9f, 0.09f, 0.12f), Color.green);
                hp.transform.localPosition = Vector3.up * (view.GetComponentInChildren<ButcherVisual25D>() != null ? 1.65f : 2.1f);
                GameObject team = Primitive("Team", PrimitiveType.Cylinder, view.transform, new Vector3(1.1f, 0.005f, 1.1f), color);
                team.transform.localPosition = Vector3.up * 0.007f;
            }
            PlayerAim aim = hero.GetComponentInChildren<PlayerAim>();
            Vector2 direction = aim != null ? aim.AimDirection : (Vector2)(FindPlayerPosition() - hero.transform.position);
            HeroActionTiming25D timing = hero.GetComponent<HeroActionTiming25D>();
            if (timing != null) direction = timing.ResolveFacing(direction);
            Quaternion facing=ArenaSpace25D.Facing(direction);
            // Once the cast has finished, an instantaneous 180-degree mouse turn
            // teleports the hand socket through the live physical cable. Keep the
            // gameplay aim independent; slew only the armed visual while flying.
            bool cableActive=physicalChainMesh!=null&&hero.GetComponent<HookAbility>()?.ActiveHook!=null&&!(timing?.HookAnimating??false);
            view.transform.rotation=cableActive?Quaternion.RotateTowards(view.transform.rotation,facing,360*Time.deltaTime):facing;
            CarcassCarrier carrier = hero.GetComponent<CarcassCarrier>();
            view.transform.Find("Cargo").gameObject.SetActive(carrier != null && carrier.Count > 0);
            view.transform.Find("Health").localScale = new Vector3(0.9f * hero.HealthFraction, 0.09f, 0.12f);
            if(Camera.main!=null)view.transform.Find("Health").rotation=Camera.main.transform.rotation;
        }
        foreach (Animal animal in FindObjectsByType<Animal>(FindObjectsInactive.Include))
        {
            if (animal.gameObject.scene != gameObject.scene) continue;
            GameObject view = View(animal);
            view.SetActive(animal.gameObject.activeInHierarchy && !animal.IsDead);
        }
        foreach (Carcass carcass in FindObjectsByType<Carcass>(FindObjectsInactive.Include))
            if (carcass.gameObject.scene == gameObject.scene)
                View(carcass);
        foreach (GameBuilding building in FindObjectsByType<GameBuilding>(FindObjectsInactive.Include))
        {
            if (building.gameObject.scene != gameObject.scene) continue;
            var buildingView=View(building);
            buildingView.transform.rotation=Quaternion.Euler(0,-building.transform.eulerAngles.z,0);
            buildingView.transform.localScale=new Vector3(building.Footprint.x,Mathf.Max(building.Footprint.x,building.Footprint.y),building.Footprint.y);
        }
        foreach(var rock in FindObjectsByType<PermanentRock>())
        {
            if(rock.gameObject.scene!=gameObject.scene)continue;
            View(rock).transform.rotation=Quaternion.Euler(0,-rock.transform.eulerAngles.z,0);
        }
        foreach (HookAbility hook in FindObjectsByType<HookAbility>())
        {
            if (hook.gameObject.scene != gameObject.scene) continue;
            UpdateHookPresentation(hook, null);
        }
        PlayerBuilding builder = FindAnyObjectByType<PlayerBuilding>();
        Transform marker = builder != null ? builder.Preview : null;
        preview.SetActive(marker != null);
        if (marker != null)
        {
            preview.transform.position = ArenaSpace25D.ToWorld(marker.position);
            preview.transform.rotation=Quaternion.Euler(0,-marker.eulerAngles.z,0);
            var previewSize=builder.SelectedPrefab.Footprint;
            preview.transform.localScale=new Vector3(previewSize.x,Mathf.Max(previewSize.x,previewSize.y),previewSize.y);
            if(previewPrefab!=builder.SelectedPrefab)
            {
                if(previewModel!=null)Destroy(previewModel);previewPrefab=builder.SelectedPrefab;
                var prefab=ModelFor(previewPrefab);previewModel=prefab!=null?Instantiate(prefab,preview.transform,false):null;
                if(previewModel!=null)
                {
                    foreach(var script in previewModel.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
                    foreach(var animator in previewModel.GetComponentsInChildren<Animator>())animator.enabled=false;
                    foreach(var particles in previewModel.GetComponentsInChildren<ParticleSystem>())particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    foreach(var light in previewModel.GetComponentsInChildren<Light>())light.enabled=false;
                }
            }
            previewProperties.SetColor("_BaseColor", builder.PreviewColor);
            foreach(var renderer in preview.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(previewProperties);
        }
        removed.Clear();
        foreach (var pair in views) if (pair.Key == null) { Destroy(pair.Value); removed.Add(pair.Key); }
        foreach (Component key in removed) views.Remove(key);
    }
    public void UpdateHookPresentation(HookAbility hook, ButcherVisual25D butcher)
    {
        if (generated == null || hook == null) return;
        if (butcher == null && hook.GetComponent<Health>() is Health owner && views.TryGetValue(owner, out GameObject heroView))
            butcher = heroView.GetComponentInChildren<ButcherVisual25D>(true);
        Vector3 tip = hook.ActiveHook != null ? ArenaSpace25D.ToWorld(hook.ActiveHook.position, 0.65f) : Vector3.zero;
        if (hook.ActiveHook != null && butcher != null)
        {
            // Cosmetic endpoint transition only. Physics2D and logical chain length
            // still begin at the unchanged hero origin and use the unchanged tip.
            float proximity = 1f - Mathf.Clamp01(Vector2.Distance(hook.ActiveHook.position, hook.transform.position) / 1.2f);
            tip += (butcher.Socket.position - ArenaSpace25D.ToWorld(hook.transform.position, 0.65f)) * proximity;
        }
        if(physicalChainMesh!=null)
        {
            physicalChains ??= new Dictionary<HookAbility,HookChain25D>();
            if(!physicalChains.TryGetValue(hook,out var physical)||physical==null)
            {
                var go=new GameObject(hook.name+" physical chain");go.transform.SetParent(generated.transform,false);
                physical=go.AddComponent<HookChain25D>();physical.Initialize(hook,physicalChainMesh,physicalChainMaterial);physicalChains[hook]=physical;
            }
            physical.linkScale=chainLinkScale;physical.thickness=chainThickness;physical.slackRatio=chainSlackRatio;
            physical.Present(butcher,tip);
        }
        if (hook.ActiveHook != null)
        {
            GameObject view = View(hook.ActiveHook);
            if (projectiles.TryGetValue(hook, out GameObject previousView) && previousView != null && previousView != view) previousView.SetActive(false);
            projectiles[hook] = view;
            view.transform.position = butcher != null ? tip : ArenaSpace25D.ToWorld(hook.ActiveHook.position, 0.5f);
            view.transform.rotation = ArenaSpace25D.Facing(hook.VisualDirection);
            view.transform.localScale=Vector3.one*hook.VisualScale;
        }
        else if (projectiles.TryGetValue(hook, out GameObject retiredView) && retiredView != null) retiredView.SetActive(false);
    }
    public Transform VisualFor(Component source) => source != null && views.TryGetValue(source, out GameObject view) && view != null ? view.transform : null;
    private static Bounds TerrainBounds(string label)
    {
        bool mountain=label.Contains("mountains"),right=label.StartsWith("right");
        float width=label=="river"?ArenaLayout.RiverWidth:mountain?ArenaLayout.MountainWidth:ArenaLayout.Depth;
        float center=label=="river"?0:(mountain?ArenaLayout.OuterEdge+width*.5f:ArenaLayout.RiverHalfWidth+width*.5f)*(right?1:-1);
        return new Bounds(new Vector3(center,0,0),new Vector3(width,ArenaLayout.FieldHeight,0));
    }
    private Vector3 FindPlayerPosition()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        return player != null ? player.transform.position : Vector3.zero;
    }
    private void OnDisable()
    {
        if (pipelineApplied) QualitySettings.renderPipeline = previousPipeline;
        pipelineApplied = false;
        if (generated != null) generated.SetActive(false);
    }
    private void DestroyGenerated()
    {
        if (generated == null) return;
        generated.SetActive(false);
        generated.name = "Retired 25D visuals";
        foreach (Renderer renderer in generated.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material != litMaterial &&
                    material.name.StartsWith("Arena25D Runtime ") &&
                    (material.hideFlags & HideFlags.DontSave) != 0) Destroy(material);
        Destroy(generated); generated = null; preview = null;
    }
    private void OnDestroy()
    {
        DestroyGenerated();
        if (materials != null) foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
