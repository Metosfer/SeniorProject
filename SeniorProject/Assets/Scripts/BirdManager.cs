using System.Collections.Generic;
using UnityEngine;

// Spawns and controls a flock of birds that patrol a 3D sky area with random wandering and altitude changes.
public class BirdManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject birdPrefab;
    [Min(0)] public int initialBirdCount = 8;
    public bool parentBirdsUnderManager = true;
    public int randomSeed = 0; // 0 = random each play

    [Header("Area Settings")] 
    [Tooltip("Center of the flight area. If 'Use Transform As Center' is true, this object's position is used.")]
    public Vector3 areaCenter = Vector3.zero;
    [Tooltip("Use this object's transform.position as area center at runtime.")]
    public bool useTransformAsCenter = true;
    [Tooltip("Size of the allowed flight area (X/Z = width/depth, Y = height).")]
    public Vector3 areaSize = new Vector3(40f, 20f, 40f);
    [Tooltip("Clamp birds inside the area bounds.")]
    public bool clampInsideArea = true;

    [Header("Flight Settings")] 
    public Vector2 speedRange = new Vector2(4f, 8f);
    [Tooltip("How fast birds turn towards targets (deg/sec).")]
    public float turnSpeedDeg = 180f;
    [Tooltip("How often a new wander target is picked [min,max] seconds.")]
    public Vector2 newTargetInterval = new Vector2(2f, 5f);
    [Tooltip("Chance per retarget to choose a new altitude.")]
    [Range(0f,1f)] public float altitudeChangeChance = 0.6f;
    [Tooltip("Extra random altitude drift per second (meters).")]
    public float altitudeDrift = 0.5f;

    [Header("Separation (Optional)")]
    public bool useSeparation = true;
    public float separationRadius = 2.5f;
    public float separationStrength = 2.0f;

    [Header("Gizmos")] 
    public bool drawGizmos = true;
    public Color areaGizmoColor = new Color(0.2f, 0.7f, 1f, 0.35f);
    public Color areaWireColor = new Color(0.1f, 0.6f, 1f, 0.9f);

    [Header("Lake Visits (Optional)")]
    [Tooltip("Kuşlar ara sıra 'Lake' tag'li objelere uğrasın.")]
    public bool enableLakeVisits = true;
    [Range(0f,1f)] public float lakeVisitChance = 0.15f;
    [Tooltip("Aynı kuş için iki ziyaret arası bekleme [min,max] sn.")]
    public Vector2 lakeVisitCooldown = new Vector2(10f, 20f);
    [Tooltip("Göl üstünde bekleme süresi [min,max] sn.")]
    public Vector2 lakeHoverTime = new Vector2(1.5f, 3.5f);
    [Tooltip("Göl yaklaşım yüksekliği (göl pozisyonuna eklenecek).")]
    public float lakeApproachAltitudeOffset = 2f;
    [Tooltip("En yakın gölü seç.")]
    public bool useNearestLake = true;
    [Tooltip("Sadece uçuş alanı içindeki gölleri seç.")]
    public bool restrictLakeToArea = true;
    [Tooltip("Göl varış yarıçapı (metre).")]
    public float lakeArrivalRadius = 1.2f;

    private readonly List<BirdAgent> _agents = new List<BirdAgent>();
    private Bounds AreaBounds
    {
        get
        {
            Vector3 center = useTransformAsCenter ? transform.position : areaCenter;
            return new Bounds(center, areaSize);
        }
    }

    private System.Random _rand;
    private readonly List<Transform> _lakePoints = new List<Transform>();

    private void Awake()
    {
        // Init random
        _rand = (randomSeed == 0) ? new System.Random() : new System.Random(randomSeed);
        ValidateParams();
    }

    private void Start()
    {
    // Cache lake points
    RefreshLakePoints();
        // Spawn initial birds
        for (int i = 0; i < initialBirdCount; i++)
        {
            SpawnBird();
        }
    }

    private void Update()
    {
        var bounds = AreaBounds;
        // Simple O(n^2) neighborhood for small flocks
        for (int i = 0; i < _agents.Count; i++)
        {
            var a = _agents[i];
            if (a == null || a.t == null) continue;

            // Retarget if needed
            if (Time.time >= a.nextRetarget || (a.t.position - a.target).sqrMagnitude < 1f)
            {
                // If visiting lake and hovering time elapsed, exit visit
                if (a.visitingLake)
                {
                    if (a.lakeTarget == null)
                    {
                        a.visitingLake = false;
                    }
                    else
                    {
                        float dist = Vector3.Distance(a.t.position, a.target);
                        bool arrived = dist <= lakeArrivalRadius;
                        if (arrived && a.lakeLeaveTime <= 0f)
                        {
                            a.lakeLeaveTime = Time.time + Mathf.Lerp(lakeHoverTime.x, lakeHoverTime.y, (float)_rand.NextDouble());
                            // keep target near lake while hovering (small circle)
                            a.target = a.lakeTarget.position + Vector3.up * lakeApproachAltitudeOffset;
                        }
                        if (a.lakeLeaveTime > 0f && Time.time >= a.lakeLeaveTime)
                        {
                            a.visitingLake = false;
                            a.lakeTarget = null;
                            a.lakeLeaveTime = 0f;
                            a.nextLakeAllowedTime = Time.time + Mathf.Lerp(lakeVisitCooldown.x, lakeVisitCooldown.y, (float)_rand.NextDouble());
                        }
                    }
                }

                if (!a.visitingLake && enableLakeVisits)
                {
                    if (MaybeSetLakeVisit(a, bounds))
                    {
                        // lake target set
                    }
                    else
                    {
                        PickNewTarget(a, bounds);
                    }
                }
                else if (!a.visitingLake)
                {
                    PickNewTarget(a, bounds);
                }
            }

            // Desired direction
            Vector3 desiredDir = (a.target - a.t.position);
            desiredDir.y += Mathf.Sin(Time.time * 0.9f + a.phase) * altitudeDrift * Time.deltaTime;
            if (desiredDir.sqrMagnitude > 0.0001f)
            {
                desiredDir.Normalize();
            }

            // Separation
            if (useSeparation)
            {
                Vector3 sep = Vector3.zero;
                int n = 0;
                for (int j = 0; j < _agents.Count; j++)
                {
                    if (j == i) continue;
                    var b = _agents[j];
                    if (b == null || b.t == null) continue;
                    Vector3 diff = a.t.position - b.t.position;
                    float d = diff.magnitude;
                    if (d > 0.0001f && d < separationRadius)
                    {
                        sep += diff / d; // direction away
                        n++;
                    }
                }
                if (n > 0)
                {
                    sep /= n;
                    desiredDir = (desiredDir + sep * separationStrength).normalized;
                }
            }

            // Turn towards desired
            if (desiredDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
                a.t.rotation = Quaternion.RotateTowards(a.t.rotation, targetRot, turnSpeedDeg * Time.deltaTime);
            }

            // Move
            a.t.position += a.t.forward * a.speed * Time.deltaTime;

            // Keep within bounds
            if (clampInsideArea)
            {
                Vector3 p = a.t.position;
                Vector3 min = bounds.min; Vector3 max = bounds.max;
                bool outside = false;
                if (p.x < min.x) { p.x = min.x; outside = true; }
                else if (p.x > max.x) { p.x = max.x; outside = true; }
                if (p.y < min.y) { p.y = min.y; outside = true; }
                else if (p.y > max.y) { p.y = max.y; outside = true; }
                if (p.z < min.z) { p.z = min.z; outside = true; }
                else if (p.z > max.z) { p.z = max.z; outside = true; }
                if (outside)
                {
                    a.t.position = p;
                    // bounce back by picking a new target inside
                    PickNewTarget(a, bounds);
                }
            }
        }
    }

    private void ValidateParams()
    {
        speedRange.x = Mathf.Max(0.1f, Mathf.Min(speedRange.x, speedRange.y));
        speedRange.y = Mathf.Max(speedRange.x, speedRange.y);
        newTargetInterval.x = Mathf.Max(0.2f, Mathf.Min(newTargetInterval.x, newTargetInterval.y));
        newTargetInterval.y = Mathf.Max(newTargetInterval.x, newTargetInterval.y);
        areaSize = new Vector3(Mathf.Max(1f, areaSize.x), Mathf.Max(1f, areaSize.y), Mathf.Max(1f, areaSize.z));
        separationRadius = Mathf.Max(0.1f, separationRadius);
        separationStrength = Mathf.Max(0f, separationStrength);
        turnSpeedDeg = Mathf.Clamp(turnSpeedDeg, 10f, 1080f);
    }

    private void SpawnBird()
    {
        Vector3 pos = RandomPointInBounds(AreaBounds);
        Quaternion rot = Quaternion.Euler(0f, (float)_rand.NextDouble() * 360f, 0f);
        GameObject go;
        if (birdPrefab != null)
        {
            go = Instantiate(birdPrefab, pos, rot);
        }
        else
        {
            // Fallback visual if no prefab assigned
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * 0.3f;
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
        }
        if (parentBirdsUnderManager)
            go.transform.SetParent(transform);

        var agent = new BirdAgent(go.transform)
        {
            speed = Mathf.Lerp(speedRange.x, speedRange.y, (float)_rand.NextDouble()),
            phase = (float)_rand.NextDouble() * Mathf.PI * 2f,
        };
        PickNewTarget(agent, AreaBounds);
        _agents.Add(agent);
    }

    private bool MaybeSetLakeVisit(BirdAgent a, Bounds bounds)
    {
        if (_lakePoints.Count == 0) return false;
        if (Time.time < a.nextLakeAllowedTime) return false;
        if ((float)_rand.NextDouble() > lakeVisitChance) return false;

        // Filter lakes by area if requested
        List<Transform> candidates = _lakePoints;
        if (restrictLakeToArea)
        {
            candidates = new List<Transform>();
            for (int i = 0; i < _lakePoints.Count; i++)
            {
                var lt = _lakePoints[i]; if (lt == null) continue;
                if (bounds.Contains(lt.position)) candidates.Add(lt);
            }
            if (candidates.Count == 0) return false;
        }

        Transform chosen = null;
        if (useNearestLake)
        {
            float best = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var lt = candidates[i]; if (lt == null) continue;
                float d = (lt.position - a.t.position).sqrMagnitude;
                if (d < best) { best = d; chosen = lt; }
            }
        }
        else
        {
            // random pick
            int tries = 0;
            while (tries < 10 && (chosen == null))
            {
                var lt = candidates[(int)Mathf.Clamp(Mathf.Floor((float)_rand.NextDouble() * candidates.Count), 0, candidates.Count - 1)];
                if (lt != null) chosen = lt;
                tries++;
            }
        }

        if (chosen == null) return false;

        Vector3 target = chosen.position + Vector3.up * lakeApproachAltitudeOffset;
        if (clampInsideArea)
        {
            target = new Vector3(
                Mathf.Clamp(target.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(target.y, bounds.min.y, bounds.max.y),
                Mathf.Clamp(target.z, bounds.min.z, bounds.max.z)
            );
        }
        a.visitingLake = true;
        a.lakeTarget = chosen;
        a.lakeLeaveTime = 0f;
        a.target = target;
        a.nextRetarget = Time.time + Mathf.Lerp(1.5f, 3.0f, (float)_rand.NextDouble());
        return true;
    }

    private void PickNewTarget(BirdAgent a, Bounds bounds)
    {
        Vector3 tgt = RandomPointInBounds(bounds);
        // Occasionally keep horizontal move but change altitude
        if ((float)_rand.NextDouble() < altitudeChangeChance)
        {
            tgt.x = a.t.position.x + Mathf.Lerp(-bounds.extents.x, bounds.extents.x, (float)_rand.NextDouble());
            tgt.z = a.t.position.z + Mathf.Lerp(-bounds.extents.z, bounds.extents.z, (float)_rand.NextDouble());
            tgt.x = Mathf.Clamp(tgt.x, bounds.min.x, bounds.max.x);
            tgt.z = Mathf.Clamp(tgt.z, bounds.min.z, bounds.max.z);
        }
        a.target = tgt;
        a.nextRetarget = Time.time + Mathf.Lerp(newTargetInterval.x, newTargetInterval.y, (float)_rand.NextDouble());
    }

    private Vector3 RandomPointInBounds(Bounds b)
    {
        return new Vector3(
            Mathf.Lerp(b.min.x, b.max.x, (float)_rand.NextDouble()),
            Mathf.Lerp(b.min.y, b.max.y, (float)_rand.NextDouble()),
            Mathf.Lerp(b.min.z, b.max.z, (float)_rand.NextDouble())
        );
    }

    private void OnValidate()
    {
        ValidateParams();
    }

    private void RefreshLakePoints()
    {
        _lakePoints.Clear();
        var lakes = GameObject.FindGameObjectsWithTag("Lake");
        for (int i = 0; i < lakes.Length; i++)
        {
            if (lakes[i] != null) _lakePoints.Add(lakes[i].transform);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var bounds = AreaBounds;
        // Filled box
        Gizmos.color = areaGizmoColor;
        Gizmos.DrawCube(bounds.center, bounds.size);
        // Wireframe
        Gizmos.color = areaWireColor;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private class BirdAgent
    {
        public Transform t;
        public float speed;
        public Vector3 target;
        public float nextRetarget;
        public float phase;
    public bool visitingLake;
    public Transform lakeTarget;
    public float nextLakeAllowedTime;
    public float lakeLeaveTime;

        public BirdAgent(Transform t)
        {
            this.t = t;
        }
    }
}
