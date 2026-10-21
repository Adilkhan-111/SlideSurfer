using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shape of the slide's cross-section: how high the surface is at a given X.
/// Measured once with rays shot down onto the colliders tagged "Pipe" (all pipe pieces have the same profile),
/// so coins and the shark can follow the curved floor and walls.
/// </summary>
public static class SlideProfile
{
    private const string PipeTag = "Pipe";
    private const float HalfWidth = 6f;
    private const float Step = 0.2f;

    private static readonly List<Vector2> points = new List<Vector2>();   // (x, surface y), sorted by x

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => points.Clear();

    /// <summary>Measure at <paramref name="probeZ"/> (where a pipe is sure to be, e.g. under the player).</summary>
    public static void Measure(float probeZ)
    {
        points.Clear();
        for (float x = -HalfWidth; x <= HalfWidth + 0.001f; x += Step)
        {
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 100f, probeZ), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag(PipeTag) && hit.point.y > best) best = hit.point.y;
            }
            if (best > float.MinValue) points.Add(new Vector2(x, best));
        }
        if (points.Count == 0)
        {
            Debug.LogWarning("SlideProfile: no '" + PipeTag + "' colliders under z=" + probeZ + ", using a flat floor");
            points.Add(new Vector2(0f, 0.6f));
        }
    }

    public static bool IsMeasured => points.Count > 0;

    public static float SurfaceY(float x)
    {
        if (points.Count == 0) Measure(-4f);
        if (x <= points[0].x) return points[0].y;
        for (int i = 1; i < points.Count; i++)
        {
            if (x <= points[i].x)
                return Mathf.Lerp(points[i - 1].y, points[i].y, Mathf.InverseLerp(points[i - 1].x, points[i].x, x));
        }
        return points[points.Count - 1].y;
    }

    /// <summary>Surface normal at X (the slide's cross-section, no slope along Z).</summary>
    public static Vector3 SurfaceNormal(float x)
    {
        const float d = 0.25f;
        float slope = (SurfaceY(x + d) - SurfaceY(x - d)) / (2f * d);
        return new Vector3(-slope, 1f, 0f).normalized;
    }
}
