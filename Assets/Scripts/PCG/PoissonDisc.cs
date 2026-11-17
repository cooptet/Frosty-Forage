using System.Collections.Generic;
using UnityEngine;

public static class PoissonDisc
{
    //Poisson disc sampling
    //Goal: Conditionality of random points->points that do not form a regular grid, yet no point lies closer than a given radius to any other.
    //Returns points in [0,size.x] x [0,size.y].

    //size of square map, radius - the point has an invisible "shield" around it, rng - sead, attempts - trying to fin
    public static List<Vector2> Sample(Vector2 size, float radius, System.Random rng, int attempts = 30)
    {
        var points = new List<Vector2>();
        if (size.x <= 0f || size.y <= 0f || radius <= 0f) return points;

        float cell = radius / Mathf.Sqrt(2f); //pitagors - square r^2 = a^2 + a^2 
        int gw = Mathf.Max(1, Mathf.CeilToInt(size.x / cell)); //number of cells horizontally and vertically
        int gh = Mathf.Max(1, Mathf.CeilToInt(size.y / cell));
        var grid = new int[gw * gh]; //Auxiliary grid for collision checking, -1 cell is empty
        for (int i = 0; i < grid.Length; i++) grid[i] = -1;

        //points where an object can still be added, removed from the list after 30 attempts
        var active = new List<int>();
        Vector2 first = new Vector2((float)rng.NextDouble() * size.x, (float)rng.NextDouble() * size.y);
        AddPoint(first, points, active, grid, cell, gw);

        float sqr = radius * radius;
        //selects random active point and trys to generate new points around it, checking their positions and distances from other points,
        //if it fails  removes that point from the list of active points.
        while (active.Count > 0)
        {
            int ai = rng.Next(active.Count);
            Vector2 c = points[active[ai]];
            bool found = false;

            for (int a = 0; a < attempts; a++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = radius * (1f + (float)rng.NextDouble());
                Vector2 cand = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                if (cand.x < 0f || cand.y < 0f || cand.x >= size.x || cand.y >= size.y) continue;
                if (!IsFarEnough(cand, points, grid, cell, gw, gh, sqr)) continue;
                AddPoint(cand, points, active, grid, cell, gw);
                found = true;
                break;
            }

            if (!found)
            {
                active[ai] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
            }
        }
        return points;
    }

    static void AddPoint(Vector2 p, List<Vector2> points, List<int> active, int[] grid, float cell, int gw)
    {
        points.Add(p);
        active.Add(points.Count - 1);
        grid[(int)(p.y / cell) * gw + (int)(p.x / cell)] = points.Count - 1;
    }

    //we are compareing sqrmagnitute with sgr radius
    //it checks nearby grid cells to checks that the new point is not too close to other points.
    static bool IsFarEnough(Vector2 p, List<Vector2> points, int[] grid, float cell, int gw, int gh, float sqr)
    {
        int cx = (int)(p.x / cell), cy = (int)(p.y / cell);
        for (int x = Mathf.Max(0, cx - 2); x <= Mathf.Min(gw - 1, cx + 2); x++)
            for (int y = Mathf.Max(0, cy - 2); y <= Mathf.Min(gh - 1, cy + 2); y++)
            {
                int idx = grid[y * gw + x];
                if (idx >= 0 && (points[idx] - p).sqrMagnitude < sqr) return false;
            }
        return true;
    }
}