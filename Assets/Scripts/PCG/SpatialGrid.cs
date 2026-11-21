using System.Collections.Generic;
using UnityEngine;

//stores information about a single object placed on the map
public struct Placed
{
    public Vector2 pos;     // world XZ
    public float radius;
    public int layer;       // -1 = pre-placed -> general group
    public int group;
    public bool blocks;  //if it blocks other objects
}
//We divide the space into cells of size cellSize by cellSize. When an object is added, it is stored in the appropriate cell,
//the system then checks the relevant neighboring cells and calculates the precise distances between objects,
//saving computional power from not performing these checks across the entire map.
public class SpatialGrid
{
    readonly float cellSize;
    readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
    public readonly List<Placed> Items = new List<Placed>(); //a list of all objects placed on the map.
    float maxBlockerRadius;

    //set size cell
    public SpatialGrid(float cellSize) { this.cellSize = Mathf.Max(0.1f, cellSize); }

    //change position of object to cell coordinates
    Vector2Int Cell(Vector2 p)
    {
        return new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize));
    }

    //Adding an object to the grid:
    //the object is stored in Items, while cells stores only its index.
    public void Add(Placed p)
    {
        int idx = Items.Count;
        Items.Add(p);
        //checks if in cell is already objects if not create new list
        Vector2Int c = Cell(p.pos);
        List<int> list;
        if (!cells.TryGetValue(c, out list)) { list = new List<int>(); cells[c] = list; }
        list.Add(idx);
        if (p.blocks) maxBlockerRadius = Mathf.Max(maxBlockerRadius, p.radius);
    }

    //checking if this place is already taken
    public bool Overlaps(Vector2 pos, float radius, float padding)
    {
        //we consider the biggest object radius to take the worst possibility
        float q = radius + maxBlockerRadius + padding;
        //creating square on place where we want to put object
        Vector2Int min = Cell(pos - Vector2.one * q), max = Cell(pos + Vector2.one * q);
        //going throug cells and checking if cells have objects
        //discard objects that do not block place
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            {
                List<int> list;
                if (!cells.TryGetValue(new Vector2Int(x, y), out list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    Placed it = Items[list[i]];
                    if (!it.blocks) continue;
                    //collision test
                    float d = radius + it.radius + padding;
                    if ((it.pos - pos).sqrMagnitude < d * d) return true;
                }
            }
        return false;
    }

    //Counts how many objects that block space for others are within a specified radius of the position of pos
    public int CountBlockers(Vector2 pos, float radius)
    {
        int count = 0;
        float sq = radius * radius;
        Vector2Int min = Cell(pos - Vector2.one * radius), max = Cell(pos + Vector2.one * radius);
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            {
                List<int> list;
                if (!cells.TryGetValue(new Vector2Int(x, y), out list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    Placed it = Items[list[i]];
                    if (it.blocks && (it.pos - pos).sqrMagnitude <= sq) count++;
                }
            }
        return count;
    }

    // counts how many objects from general groups are located within
    // a given radius(takes into account only the selected layer)
    public void CountGroups(Vector2 pos, float radius, int layer, int[] counts)
    {
        for (int i = 0; i < counts.Length; i++) counts[i] = 0;
        float sq = radius * radius;
        Vector2Int min = Cell(pos - Vector2.one * radius), max = Cell(pos + Vector2.one * radius);
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            {
                List<int> list;
                if (!cells.TryGetValue(new Vector2Int(x, y), out list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    Placed it = Items[list[i]];
                    if (it.layer != layer || it.group < 0 || it.group >= counts.Length) continue;
                    if ((it.pos - pos).sqrMagnitude <= sq) counts[it.group]++;
                }
            }
    }
}