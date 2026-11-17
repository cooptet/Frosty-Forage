using UnityEngine;

// idea spawning of prefab(ordered in sizes) depending on distance from middle
//if spawning is by distance(trees-> accesiblity to ligtht) or doesnt matter
public enum SizeMode { ByDistance, Random }

//if prefabs in list are from largest or smallest
public enum VariantOrder { SmallestFirst, LargestFirst }

[System.Serializable]

//Groups in group -> devided each group to subgroups to make ilussion of same category
//Weight is giving weight in draw-> if we want to change to make some subgroup appier more often then other
public class VariantGroup
{
    public string name = "Group";
    [Min(0f)] public float weight = 1f;
    [Tooltip("Prefabs should be orderded by size, depending on VariantOrder choosen').")]
    public GameObject[] prefabs;
}

// Category of object, like trees, bushes, rocks etc. General names
// We can add houses to create village etc.
[System.Serializable]
public class SpawnLayer
{
    public string name = "Layer";
    [Tooltip("For diffrent maps if we decide to not spawn some layer(general group).")]
    public bool enabled = true;

    [Header("Variants")]
    public VariantGroup[] groups;
    public VariantOrder variantOrder = VariantOrder.SmallestFirst;
    public SizeMode sizeMode = SizeMode.ByDistance;
    [Tooltip("for modeling how the transition from the largest to the smallest changes as a function of distance. X-> 0 := center, 1 := edges. Y-> 0 :=smallest, 1:= largest")]
    public AnimationCurve sizeByDistance = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    // we dont want perfect rings-> add aditional noice -> change if paterns look too regular
    [Range(0f, 0.5f)] public float sizeJitter = 0.15f;
    //concept: items from the same category are more likely to appear next to each other.
    [Tooltip("Changes how often the same categories spawn near each other")]
    [Range(0f, 3f)] public float groupStickiness = 1f;
    [Tooltip("Just to count when we consider that something is neighbour")]
    [Min(0.1f)] public float clusterRadius = 6f;

    [Header("Density")]
    [Tooltip("Poisson radius – density limit. A higher value means neighbors will be sparser, while a lower value means they will be more frequent denser.\r\n")]
    [Min(0.05f)] public float minDistance = 2f;
    [Tooltip("Multiplier on spawn chance.")]
    [Min(0f)] public float density = 1f;
    [Tooltip("X(0:=center, 1:=edges). Y: spawn chance multiplier based on distance from center")]
    public AnimationCurve densityByDistance = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
    //Crowding: the generator checks how many objects are located around a given point and, based on this, decreases (or increases) the spawn chance.
    [Tooltip("enabled or disabled to the check for how tight the space is around a point -> if disabled, the next 3 parameters are not applied")]
    public bool useCrowding = false;
    //radius to determine how many placed objects are already to determine crowdeness
    [Min(0.1f)] public float crowdingRadius = 4f;
    [Tooltip("setting a threshold for the number of objects that can be within a certain radius before it is considered crawded")]
    [Min(1f)] public float crowdingSaturation = 5f;
    [Tooltip("X(0:=empty, 1:=crowded). Y(again change multiplayer)")]
    public AnimationCurve densityByCrowding = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);
    [Tooltip("maximum number of objects in a layer(general group)")]
    //I was thinking if we add houses there should be some limit on how many of them(and for acorns)
    [Min(0)] public int maxCount = 0;

    //creating Perlin noise determines where patches form. It dictates the density of an object and defining where there are open spaces
    [Header("Perlin Noise")]
    //size of patches and how strong noise is working
    [Range(0f, 1f)] public float noiseInfluence = 0f;
    [Min(0.001f)] public float noiseScale = 0.15f;
    //larger range transition smoother
    [Range(0f, 1f)] public float noiseMin = 0.35f;
    [Range(0f, 1f)] public float noiseMax = 0.65f;

    [Header("Collision")]
    //how deal with collision 
    [Tooltip("Later another object will see this object as taken place")]
    public bool blocksOthers = true;
    [Tooltip("Avoid other places with objects")]
    public bool avoidBlockers = true;
    //additional space from other spawn objects
    public float padding = 0.1f;
    [Tooltip("Multiplies footprint measured from the prefab colliders.")]
    public float footprintMultiplier = 1f;
    [Tooltip("If the chosen colider prefab does not fit in area, try smaller ones.")]
    public bool tryDownsize = true;

    //for transforming object
    [Header("Transform")]
    public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    public AnimationCurve scaleByDistance = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public bool randomYaw = true;
    //random lateral tilting (used for grass and plants).
    [Range(0f, 30f)] public float maxTilt = 0f;
    public float yOffset = 0f;
}