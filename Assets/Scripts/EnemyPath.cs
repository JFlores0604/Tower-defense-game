using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  EnemyPath.cs — the predetermined, curvy (non-straight) path enemies walk.
//  Control points are smoothed with a Catmull-Rom spline and then resampled
//  so we can ask "where is distance d along the path?" in O(log n).
// ============================================================================
public class EnemyPath
{
    public readonly List<Vector3> points = new List<Vector3>();
    readonly List<float> cumulative = new List<float>();
    public float TotalLength { get; private set; }
    public const float HalfWidth = 1.1f;

    // Edit these to reshape the path (x, z on the board).
    public static readonly Vector2[] ControlPoints =
    {
        new Vector2(-19f, 4f), new Vector2(-12f, 4f), new Vector2(-8.5f, 7f), new Vector2(-4f, 6.5f),
        new Vector2(-3f, 1.5f), new Vector2(-6.5f, -2.5f), new Vector2(-5f, -7f), new Vector2(0.5f, -7.5f),
        new Vector2(3f, -3f), new Vector2(1.5f, 2f), new Vector2(4.5f, 6.5f), new Vector2(10f, 6.5f),
        new Vector2(12.5f, 2f), new Vector2(9f, -2f), new Vector2(11f, -6.5f), new Vector2(17.5f, -6.5f)
    };

    public EnemyPath()
    {
        var raw = new List<Vector3>();
        var cp = ControlPoints;
        for (int i = 0; i < cp.Length - 1; i++)
        {
            Vector3 p0 = V(cp[Mathf.Max(i - 1, 0)]);
            Vector3 p1 = V(cp[i]);
            Vector3 p2 = V(cp[i + 1]);
            Vector3 p3 = V(cp[Mathf.Min(i + 2, cp.Length - 1)]);
            for (int s = 0; s < 16; s++) raw.Add(CatmullRom(p0, p1, p2, p3, s / 16f));
        }
        raw.Add(V(cp[cp.Length - 1]));

        // Resample at even spacing so speed along the path is constant.
        const float step = 0.25f;
        points.Add(raw[0]);
        float carry = 0f;
        for (int i = 1; i < raw.Count; i++)
        {
            Vector3 a = raw[i - 1], b = raw[i];
            float seg = Vector3.Distance(a, b);
            float t = step - carry;
            while (t <= seg)
            {
                points.Add(Vector3.Lerp(a, b, t / seg));
                t += step;
            }
            carry = seg - (t - step);
        }
        if (points[points.Count - 1] != raw[raw.Count - 1]) points.Add(raw[raw.Count - 1]);

        cumulative.Add(0f);
        for (int i = 1; i < points.Count; i++)
            cumulative.Add(cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]));
        TotalLength = cumulative[cumulative.Count - 1];
    }

    static Vector3 V(Vector2 v) => new Vector3(v.x, 0f, v.y);

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    int IndexAt(float dist)
    {
        int lo = 0, hi = cumulative.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (cumulative[mid] <= dist) lo = mid; else hi = mid;
        }
        return lo;
    }

    public Vector3 PositionAt(float dist)
    {
        if (dist <= 0f) return points[0];
        if (dist >= TotalLength) return points[points.Count - 1];
        int i = IndexAt(dist);
        float segLen = cumulative[i + 1] - cumulative[i];
        float t = segLen > 0f ? (dist - cumulative[i]) / segLen : 0f;
        return Vector3.Lerp(points[i], points[i + 1], t);
    }

    public Vector3 TangentAt(float dist)
    {
        int i = Mathf.Clamp(IndexAt(Mathf.Clamp(dist, 0f, TotalLength)), 0, points.Count - 2);
        return (points[i + 1] - points[i]).normalized;
    }

    /// Horizontal distance from a point to the nearest part of the path.
    public float DistanceTo(Vector3 p)
    {
        p.y = 0f;
        float best = float.MaxValue;
        for (int i = 0; i < points.Count - 1; i += 1)
        {
            Vector3 a = points[i], b = points[i + 1];
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
            float d = (a + ab * t - p).sqrMagnitude;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }
}
