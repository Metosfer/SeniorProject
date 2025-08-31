using System.Collections.Generic;
using UnityEngine;

// Optional: converts detected beats into a queued pattern for deterministic playthroughs.
// Not strictly required for basic gameplay but useful if you want to precompute.
public class BeatmapGenerator : MonoBehaviour
{
    [Range(60, 200)] public float assumedBPM = 120f;
    public float offsetSeconds = 0f;

    public struct BeatEvent { public float time; public int lane; }

    public static List<BeatEvent> GenerateUniform(float lengthSec, float bpm, float offset, System.Random rng)
    {
        var events = new List<BeatEvent>();
        float step = 60f / Mathf.Max(1f, bpm);
        for (float t = offset; t <= lengthSec; t += step)
        {
            events.Add(new BeatEvent { time = t, lane = rng.Next(0, 4) });
        }
        return events;
    }
}
