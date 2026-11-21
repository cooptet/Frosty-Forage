using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum DistanceMetric { Radial, Square }

[System.Serializable]
//consider object that is already on map and next object needs to avoid it
public class PreplacedBlocker
{
    public Transform target;
    [Tooltip("0 := measure from colliders.")]
    public float radius = 0f;
    public float padding = 0.3f;
}

public class ForestGenerator : MonoBehaviour
{
    const string ContainerName = "Generated";

    //specifying where the generator can place objects.
    [Header("Map")]
    public bool sizeFromGround = true;
    [Tooltip("Determine the size of square where generating objects")]
    public float areaSize = 29f;
    public float edgeMargin = 0.5f;
    public DistanceMetric metric = DistanceMetric.Radial;

    [Header("Ground height: false based on object y position(make it 0)")]
    public bool snapToGround = false;
    public LayerMask groundMask = ~0;

    [Header("Generation")]
    public int seed = 12345;
    public bool generateOnStart = true;
    public bool randomSeedEachRun = false;
    [Tooltip("In general how close objects are going to be to eachother")]
    [Range(0.1f, 3f)] public float globalDensity = 1f;

    [Header("Objects already on the map")]
    public PreplacedBlocker[] preplaced;

    [Header("Layers - Main/General Categories")]
    public SpawnLayer[] layers;

    //For debuging purposes
    //This is used to verify whether objects are positioned correctly
    //it draws auxiliary areas occupied by the objects.
    //main game ->keep it false
    public bool drawFootprints = true;

    Vector2 areaCenter;
    float areaHalf;
    readonly Dictionary<GameObject, float> footprints = new Dictionary<GameObject, float>();
    SpatialGrid lastGrid;
    float lastY;

    void Start()
    {
        if (!generateOnStart) return;
        //keeping it as randomised or change it if we want some special sead(predetermined)
        if (randomSeedEachRun) seed = UnityEngine.Random.Range(0, int.MaxValue);
        Generate();
    }


    public void Generate()
    {
        Clear(); //delate before created objects
        if (layers == null) return;

        float size, groundY;
        //initial values from map values and object values
        ResolveArea(out areaCenter, out size, out groundY);
        areaHalf = size * 0.5f;
        lastY = groundY;

        //vector 2 bc we are not looking at putting anything in sky
        //preparing arena
        Vector2 domain = Vector2.one * Mathf.Max(0.1f, size - 2f * edgeMargin);
        Vector2 origin = areaCenter - domain * 0.5f;

        //With each new generation, the extents of the prefabs will be re-measured.
        footprints.Clear();
        var grid = new SpatialGrid(2f);
        lastGrid = grid;
        //we have palyer and tree that need to be already treated as obstacle
        RegisterPreplaced(grid);

        //Game object generate
        Transform root = new GameObject(ContainerName).transform;
        root.SetParent(transform, false);

        //Game objects for each layer/main categories + log to know how many objects on scene
        for (int i = 0; i < layers.Length; i++)
        {
            SpawnLayer L = layers[i];
            if (L == null || !L.enabled || L.groups == null || L.groups.Length == 0) continue;
            int n = GenerateLayer(i, L, grid, origin, domain, groundY, root);
            Debug.Log("[PCG] " + L.name + ": " + n);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(root.gameObject, "Generate Forest");
#endif
    }

    //removes the previously generated container
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject go = transform.GetChild(i).gameObject;
            if (go.name != ContainerName) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Undo.DestroyObjectImmediate(go); continue; }
#endif
            Destroy(go);
        }
    }


    int GenerateLayer(int li, SpawnLayer L, SpatialGrid grid, Vector2 origin, Vector2 domain, float groundY, Transform root)
    {
        //prepare randomness and points
        var rng = new System.Random(seed + 7919 * (li + 1));
        List<Vector2> pts = PoissonDisc.Sample(domain, L.minDistance, rng);
        //A mix of points to address the problem in random order
        Shuffle(pts, rng);

        //Object with name of layer all object from this layer will be children
        Transform parent = new GameObject(L.name).transform;
        parent.SetParent(root, false);

        //We generate two offsets for the Perlin noise.
        //This allows different layers to have distinct noise patterns.
        float noiseX = rng.Next(0, 1000), noiseY = rng.Next(0, 1000);
        //Each element will store the number of objects belonging to a given group of the point under consideration.
        var counts = new int[L.groups.Length];
        //weigths likehood of choosing each group
        var weights = new float[L.groups.Length];
        int spawned = 0;

        foreach (Vector2 p in pts)
        {
            //going from object genereted by poisson disk sampling, if we reach limit break
            if (L.maxCount > 0 && spawned >= L.maxCount) break;

            //started at 0 byt be give a proper place in map
            Vector2 pos = origin + p;
            //This value will be used by curves that define, for example, the density and size of objects.
            float t = NormDistance(pos);

            //fun part-> chance of generation based on desity. multiplayer and value of curve based on distance
            float chance = L.density * globalDensity * L.densityByDistance.Evaluate(t);

            //if crowding turn on change change based on number of blocking objects
            if (L.useCrowding)
            {
                float crowd = Mathf.Clamp01(grid.CountBlockers(pos, L.crowdingRadius) / L.crowdingSaturation);
                chance *= L.densityByCrowding.Evaluate(crowd);
            }
            //implementing second algoritm pelin noise
            //empty space and natural clusters -> grass/plants
            if (L.noiseInfluence > 0f)
            {
                float n = Mathf.PerlinNoise(pos.x * L.noiseScale + noiseX, pos.y * L.noiseScale + noiseY);
                float f = Mathf.Clamp01(Mathf.InverseLerp(L.noiseMin, L.noiseMax, n));
                chance *= Mathf.Lerp(1f, f, L.noiseInfluence);
            }

            // determining whether an object should appear
            if (Next01(rng) > chance) continue;

            //it checks which groups(subgroups) are located near a given point and, based on this, randomly selects one of them.
            int g = PickGroup(L, grid, pos, li, counts, weights, rng);
            if (g < 0) continue;
            VariantGroup group = L.groups[g];

            //pick index of subgroup(small, middle etc) + calc scale
            int rank = PickRank(L, group.prefabs.Length, t, rng);
            float s = Mathf.Lerp(L.scaleRange.x, L.scaleRange.y, Next01(rng)) * L.scaleByDistance.Evaluate(t);

            //The prefab selection and process checks whether the chosen prefab can be placed at a given location.
            GameObject prefab = null;
            float radius = 0f;
            for (int r = rank; r >= 0; r--)
            {
                GameObject cand = PrefabAtRank(L, group, r);
                if (cand == null) continue;
                float rad = Footprint(cand) * L.footprintMultiplier * s;
                if (!L.avoidBlockers || !grid.Overlaps(pos, rad, L.padding)) { prefab = cand; radius = rad; break; }
                if (!L.tryDownsize) break;
            }
            if (prefab == null) continue;

            Vector3 wp = new Vector3(pos.x, groundY, pos.y);
            RaycastHit hit;
            //sending ray and safe hit result 
            //placing a point in space on the ground surface
            if (snapToGround && Physics.Raycast(new Vector3(pos.x, groundY + 50f, pos.y), Vector3.down, out hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
                wp.y = hit.point.y;
            wp.y += L.yOffset;

            //roatating objects
            float yaw = L.randomYaw ? Next01(rng) * 360f : 0f;
            float tx = L.maxTilt > 0f ? (Next01(rng) * 2f - 1f) * L.maxTilt : 0f;
            float tz = L.maxTilt > 0f ? (Next01(rng) * 2f - 1f) * L.maxTilt : 0f;
            Quaternion rot = Quaternion.Euler(tx, yaw, tz) * prefab.transform.rotation;

            //Creating an object and adding it to the grid
            GameObject inst = Spawn(prefab, parent);
            inst.transform.SetPositionAndRotation(wp, rot);
            inst.transform.localScale = prefab.transform.localScale * s;

            grid.Add(new Placed { pos = pos, radius = radius, layer = li, group = g, blocks = L.blocksOthers });
            spawned++;
        }
        return spawned;
    }
    //This method selects a group from which a prefab will be randomly chosen.

    //It takes into account the group's weight as well as which objects are already nearby.

    int PickGroup(SpawnLayer L, SpatialGrid grid, Vector2 pos, int li, int[] counts, float[] weights, System.Random rng)
    {
        grid.CountGroups(pos, L.clusterRadius, li, counts);
        float total = 0f;
        for (int g = 0; g < L.groups.Length; g++)
        {
            VariantGroup grp = L.groups[g];
            weights[g] = HasPrefab(grp) ? grp.weight * (1f + L.groupStickiness * counts[g]) : 0f;
            total += weights[g];
        }
        if (total <= 0f) return -1;

        float pick = Next01(rng) * total;
        int last = -1;
        for (int g = 0; g < weights.Length; g++)
        {
            if (weights[g] <= 0f) continue;
            last = g;
            pick -= weights[g];
            if (pick <= 0f) return g;
        }
        return last;
    }
    //selects a variant index based on the size selection mode , a value from a curve ,and a random perturbation
    int PickRank(SpawnLayer L, int n, float t, System.Random rng)
    {
        if (n <= 1) return 0;
        if (L.sizeMode == SizeMode.Random) return rng.Next(n);
        float target = Mathf.Clamp01(L.sizeByDistance.Evaluate(t) + (Next01(rng) * 2f - 1f) * L.sizeJitter);
        return Mathf.RoundToInt(target * (n - 1));
    }

    // retrieves a prefab based on its rank, based of largest/smallest order
    static GameObject PrefabAtRank(SpawnLayer L, VariantGroup group, int rank)
    {
        int n = group.prefabs.Length;
        int idx = L.variantOrder == VariantOrder.SmallestFirst ? rank : n - 1 - rank;
        return group.prefabs[idx];
    }

    //if group has at least one prefab
    static bool HasPrefab(VariantGroup g)
    {
        if (g == null || g.prefabs == null) return false;
        for (int i = 0; i < g.prefabs.Length; i++) if (g.prefabs[i] != null) return true;
        return false;
    }

    //distance from middle of map

    float NormDistance(Vector2 p)
    {
        Vector2 d = p - areaCenter;
        float dist = metric == DistanceMetric.Radial ? d.magnitude : Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
        return Mathf.Clamp01(dist / Mathf.Max(0.01f, areaHalf));
    }

    //deciding of area for generation-> square for us
    void ResolveArea(out Vector2 center, out float size, out float y)
    {
        center = new Vector2(transform.position.x, transform.position.z);
        size = areaSize;
        y = transform.position.y;
    }

    //measure radius of object that will be used for checking collisions
    static float MeasureRadius(GameObject go, float fallback)
    {
        bool has = false;
        Bounds b = default(Bounds);
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
        }
        if (!has)
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
        return has ? Mathf.Max(b.extents.x, b.extents.z) : fallback;
    }

    // Footprint radius of a prefab at its own scale - debugging purposes 
    float Footprint(GameObject prefab)
    {
        float f;
        if (footprints.TryGetValue(prefab, out f)) return f;

        GameObject tmp = Instantiate(prefab, new Vector3(0f, -10000f, 0f), prefab.transform.rotation);
        Physics.SyncTransforms();
        f = MeasureRadius(tmp, 0.25f);
        if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);

        footprints[prefab] = f;
        return f;
    }

    //adds objects that are already on the map to the SpatialGrid.
    void RegisterPreplaced(SpatialGrid grid)
    {
        if (preplaced == null) return;
        Physics.SyncTransforms();
        foreach (PreplacedBlocker p in preplaced)
        {
            if (p == null || p.target == null) continue;
            float r = p.radius > 0f ? p.radius : MeasureRadius(p.target.gameObject, 0.5f);
            grid.Add(new Placed
            {
                pos = new Vector2(p.target.position.x, p.target.position.z),
                radius = r + p.padding,
                layer = -1,
                group = -1,
                blocks = true
            });
        }
    }


    static GameObject Spawn(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
        return Instantiate(prefab, parent);
    }
    //just random nr from 0 to 1
    static float Next01(System.Random rng) { return (float)rng.NextDouble(); }

    //mix order of points
    static void Shuffle(List<Vector2> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2 tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // helping on balancing for rest of team
    //visible + default combination-> no acorns and houses(implemented later)

    static VariantGroup G(string name, int count)
    {
        return new VariantGroup { name = name, prefabs = new GameObject[count] };
    }

    static AnimationCurve C(float center, float edge) { return AnimationCurve.Linear(0f, center, 1f, edge); }

    void Reset()
    {
        layers = new SpawnLayer[]
        {
            new SpawnLayer
            {
                name = "Trees", minDistance = 2.2f, density = 1f,
                densityByDistance = C(1f, 0.25f),
                sizeMode = SizeMode.ByDistance, sizeByDistance = C(1f, 0f), sizeJitter = 0.15f,
                groupStickiness = 1.5f, clusterRadius = 7f, padding = 0.3f,
                scaleByDistance = C(1f, 0.8f),
                groups = new VariantGroup[] { G("Conifers", 4), G("Colorful", 3), G("Deciduous", 3) }
            },
            new SpawnLayer
            {
                name = "Rocks", minDistance = 3f, density = 0.6f,
                densityByDistance = C(1f, 1f),
                useCrowding = true, crowdingRadius = 5f, crowdingSaturation = 5f, densityByCrowding = C(1f, 0.05f),
                sizeMode = SizeMode.Random, padding = 0.3f, scaleRange = new Vector2(0.8f, 1.3f),
                groups = new VariantGroup[] { G("Rocks", 3) }
            },
            new SpawnLayer
            {
                name = "Bushes", minDistance = 1.6f, density = 1f,
                densityByDistance = C(0.2f, 1f),
                useCrowding = true, crowdingRadius = 3.5f, crowdingSaturation = 4f, densityByCrowding = C(1f, 0.15f),
                sizeMode = SizeMode.Random, variantOrder = VariantOrder.LargestFirst, padding = 0.2f,
                groups = new VariantGroup[] { G("Bushes", 3) }
            },
            new SpawnLayer
            {
                name = "Grass", minDistance = 0.45f, density = 1f,
                densityByDistance = C(1f, 1f),
                noiseInfluence = 1f, noiseScale = 0.12f, noiseMin = 0.35f, noiseMax = 0.65f,
                blocksOthers = false, padding = 0.05f, tryDownsize = false,
                sizeMode = SizeMode.Random, groupStickiness = 0f,
                scaleRange = new Vector2(0.8f, 1.2f), maxTilt = 6f,
                groups = new VariantGroup[] { G("Grass", 2) }
            },
            new SpawnLayer
            {
                name = "Plants", minDistance = 1.1f, density = 1f,
                densityByDistance = C(1f, 1f),
                noiseInfluence = 1f, noiseScale = 0.2f, noiseMin = 0.5f, noiseMax = 0.75f,
                blocksOthers = false, padding = 0.1f, tryDownsize = false,
                sizeMode = SizeMode.Random, groupStickiness = 0f,
                scaleRange = new Vector2(0.8f, 1.2f), maxTilt = 4f,
                groups = new VariantGroup[] { G("Plants", 2) }
            }
        };
    }

    //just drawing this helping areas -> for debugging not usefull for game
    void OnDrawGizmosSelected()
    {
        Vector2 c; float s, y;
        ResolveArea(out c, out s, out y);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(c.x, y, c.y), new Vector3(s, 0.05f, s));
        Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
        float inner = Mathf.Max(0.1f, s - 2f * edgeMargin);
        Gizmos.DrawWireCube(new Vector3(c.x, y, c.y), new Vector3(inner, 0.05f, inner));

        if (!drawFootprints || lastGrid == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        foreach (Placed it in lastGrid.Items)
            if (it.blocks) Gizmos.DrawWireSphere(new Vector3(it.pos.x, lastY, it.pos.y), it.radius);
    }
}