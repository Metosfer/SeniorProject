using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// FarmingAreaManager: Manages dropping seeds, growth timing, and spawning the mature plant.
// - 3D world drop is handled via DragAndDropHandler (raycast hits this object).
// - When spawning the mature plant, keep the prefab's original shape/rotation/scale; do not parent.
// - Shows a per-plot countdown and a status text (ready/growing/empty) while growing.
public class FarmingAreaManager : MonoBehaviour, IDropHandler
{
    [Header("Planting Spots")]
    [Tooltip("Planting points (empty GameObjects). The first free slot is chosen.")]
    public List<Transform> plotPoints = new List<Transform>();

    [Header("Visuals & Feedback")]
    [Tooltip("Optional small marker prefab shown when planting (seed mark).")]
    public GameObject seedMarkerPrefab;
    [Tooltip("VFX or Particle prefab played when growth completes.")]
    public GameObject growVFXPrefab;

    [Tooltip("Status text: assign a world-space TMP_Text or any text component.")]
    public TMP_Text statusTMP;   // optional
    public TMP_Text statusTextUI;    // optional

    [Tooltip("Simple status format: {0}=ready, {1}=total")]
    public string statusFormatSimple = "{0}/{1} ready";
    [Tooltip("Detailed status format: {0}=ready, {1}=growing, {2}=empty")]
    public string statusFormatDetailed = "Ready: {0} | Growing: {1} | Empty: {2}";
    public bool showDetailedStatus = true;

    [Header("Countdown Text (Optional)")]
    [Tooltip("World-space text prefab to show remaining time while growing. Must have TMP_Text or UI.Text.")]
    public GameObject countdownTextPrefab;
    [Tooltip("Offset from the plot center for the countdown text.")]
    public Vector3 countdownOffset = new Vector3(0f, 0.25f, 0f);

    [Header("Growth Settings")]
    [Tooltip("Default growth time (s) if SCItem has no growthTime.")]
    public float defaultGrowthTime = 10f;
    [Tooltip("Use SCItem.growthTime when available, otherwise fallback to default.")]
    public bool useItemGrowthTimeIfAvailable = true;

    [Header("Placement")]
    [Tooltip("Apply a small offset at spawn position (slightly above ground).")]
    public Vector3 spawnOffset = new Vector3(0f, 0.02f, 0f);

    [Header("Watering (Bucket Integration)")]
    [Tooltip("Key to water crops when in range with a filled bucket.")]
    public KeyCode waterKey = KeyCode.E;
    [Tooltip("Max distance from player/bucket to this farming area to allow watering.")]
    public float wateringRange = 2.5f;
    [Tooltip("Optional UI prompt to show when watering is possible (e.g., 'Press E to water').")]
    public GameObject wateringPromptUI;
    [Tooltip("Optional VFX when watering starts.")]
    public GameObject wateringVFX;

    [Header("Status Formatting (Detailed)")]
    [Tooltip("If true, use extended detailed status including waiting.")]
    public bool useWateringAwareStatus = true;
    [Tooltip("Detailed status format when watering is used: {0}=ready, {1}=growing, {2}=waiting, {3}=empty")]
    public string statusFormatWatering = "Ready: {0} | Growing: {1} | Waiting: {2} | Empty: {3}";

    // Internal state tracking
    private readonly List<PlotState> _plots = new List<PlotState>();

    // Throttled prompt update like WellManager
    private const float UI_UPDATE_INTERVAL = 0.1f;
    private const float RANGE_HYSTERESIS = 0.2f;
    private float _lastUIUpdateTime;
    private bool _inRangeSticky;
    private bool _lastPromptState;

    private void Awake()
    {
        SyncPlotStatesWithPoints();
        UpdateStatusText();
    }

    private void OnValidate()
    {
        SyncPlotStatesWithPoints();
    }

    private void Update()
    {
        // Watering prompt + input handling (integrates with BucketManager)
        UpdateWateringPromptAndHandleInput();
    }

    private void SyncPlotStatesWithPoints()
    {
        if (_plots.Count != plotPoints.Count)
        {
            var newList = new List<PlotState>(plotPoints.Count);
            for (int i = 0; i < plotPoints.Count; i++)
            {
                if (i < _plots.Count && _plots[i] != null)
                    newList.Add(_plots[i]);
                else
                    newList.Add(new PlotState());
            }
            _plots.Clear();
            _plots.AddRange(newList);
        }
    }

    // DragAndDropHandler calls this directly; also works via IDropHandler if this is a UI target
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;

        var drag = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<DragAndDropHandler>() : null;
        if (drag == null || drag.inventory == null) return;

        var inventory = drag.inventory;
        int slotIndex = drag.slotIndex;
        if (slotIndex < 0 || slotIndex >= inventory.inventorySlots.Count) return;

        var slot = inventory.inventorySlots[slotIndex];
        var item = slot.item;
        if (item == null) return;
        if (!HasSeedFlag(item)) return;

        Vector3? worldHint = GetWorldPointFromScreen(eventData.position);
        int freeIndex = GetBestFreePlotIndex(worldHint);
        if (freeIndex < 0)
        {
            Debug.Log("FarmingArea: No free plot available.");
            return;
        }

        bool planted = TryPlantAtPlot(freeIndex, item, inventory, slotIndex);
        if (!planted)
        {
            Debug.LogWarning("FarmingArea: Planting failed.");
        }
    }

    private bool TryPlantAtPlot(int plotIndex, SCItem seedItem, SCInventory inventory, int inventorySlotIndex)
    {
        if (plotIndex < 0 || plotIndex >= plotPoints.Count) return false;
        var state = _plots[plotIndex];
        if (state.isOccupied) return false;

        Transform point = plotPoints[plotIndex];
        if (point == null) return false;

        // Consume 1 seed from inventory
        if (!RemoveSingleItemFromInventory(inventory, inventorySlotIndex))
        {
            Debug.LogWarning("FarmingArea: Could not consume seed from inventory.");
            return false;
        }

        // Optional marker
        if (seedMarkerPrefab != null)
        {
            state.seedMarkerInstance = Instantiate(seedMarkerPrefab, point.position + spawnOffset, seedMarkerPrefab.transform.rotation);
        }

    // Cache growth time but do NOT start yet (require water)
    float growthTime = GetGrowthTime(seedItem);

    // Set state and wait for water
        state.isOccupied = true;
        state.currentSeed = seedItem;
    state.requiresWater = true;
    state.isGrowing = false;
    state.plannedGrowthTime = Mathf.Max(0.01f, growthTime);
    state.growthEndTime = 0f;

    // Show per-plot "Waiting" text; do not start countdown until watered
    CreateOrUpdateCountdown(plotIndex, startTimer: false, customText: "Waiting");
        UpdateStatusText();
        return true;
    }

    private IEnumerator GrowAndSpawn(int plotIndex, float growthTime)
    {
        var state = _plots[plotIndex];
        Transform point = plotPoints[plotIndex];

        yield return new WaitForSeconds(growthTime);

    // Clear countdown (will be destroyed just before spawning to avoid residuals)
    DestroyCountdown(plotIndex);

        // Remove marker
        if (state.seedMarkerInstance != null)
        {
            Destroy(state.seedMarkerInstance);
            state.seedMarkerInstance = null;
        }

        // VFX
        if (growVFXPrefab != null)
        {
            var vfx = Instantiate(growVFXPrefab, point.position + spawnOffset, point.rotation);
            Destroy(vfx, 3f);
        }

        // Spawn grown (preserve prefab transform)
        GameObject grownPrefab = GetGrownPrefab(state.currentSeed);
        if (grownPrefab != null)
        {
            state.grownInstance = Instantiate(grownPrefab, point.position + spawnOffset, grownPrefab.transform.rotation);

            // Ensure Plant + interaction safety
            var plant = state.grownInstance.GetComponent<Plant>();
            if (plant == null) plant = state.grownInstance.AddComponent<Plant>();
            var col = state.grownInstance.GetComponent<Collider>();
            if (col == null) col = state.grownInstance.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var rb = state.grownInstance.GetComponent<Rigidbody>();
            if (rb == null) rb = state.grownInstance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (plant.item == null)
            {
                var harvest = GetHarvestItem(state.currentSeed);
                if (harvest != null) plant.item = harvest;
            }
        }
        else
        {
            Debug.LogWarning("FarmingArea: Grown prefab not found; spawn skipped.");
        }

        // Watch for harvest
        StartCoroutine(WatchForHarvest(plotIndex));
    UpdateStatusText();
    }

    private IEnumerator WatchForHarvest(int plotIndex)
    {
        var state = _plots[plotIndex];
        while (state.grownInstance != null)
        {
            yield return new WaitForSeconds(0.5f);
        }
        state.Reset();
        UpdateStatusText();
    }

    private int GetBestFreePlotIndex(Vector3? worldHint)
    {
        int fallback = -1;
        float bestDist = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < plotPoints.Count; i++)
        {
            if (_plots[i].isOccupied) continue;
            if (plotPoints[i] == null) continue;
            if (fallback == -1) fallback = i;

            if (worldHint.HasValue)
            {
                float d = Vector3.SqrMagnitude(plotPoints[i].position - worldHint.Value);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                }
            }
        }
        return bestIndex >= 0 ? bestIndex : fallback;
    }

    private Vector3? GetWorldPointFromScreen(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 200f)) return hit.point;
        return null;
    }

    private static bool RemoveSingleItemFromInventory(SCInventory inventory, int slotIndex)
    {
        if (inventory == null) return false;
        if (slotIndex < 0 || slotIndex >= inventory.inventorySlots.Count) return false;
        Slot slot = inventory.inventorySlots[slotIndex];
        if (slot == null || slot.item == null || slot.itemCount <= 0) return false;

        if (slot.itemCount > 1)
        {
            slot.itemCount--;
            if (slot.isFull) slot.isFull = false;
        }
        else
        {
            slot.item = null;
            slot.itemCount = 0;
            slot.isFull = false;
        }
        inventory.TriggerInventoryChanged();
        return true;
    }

    private float GetGrowthTime(SCItem item)
    {
        if (item == null) return defaultGrowthTime;
        // Use SCItem.growthTime directly when available, otherwise default
        return item.growthTime > 0f ? item.growthTime : defaultGrowthTime;
    }

    private GameObject GetGrownPrefab(SCItem item)
    {
        if (item == null) return null;
        return item.grownPrefab != null ? item.grownPrefab : item.itemPrefab;
    }

    private SCItem GetHarvestItem(SCItem seed)
    {
        if (seed == null) return null;
        return seed.harvestItem; // fallback if Plant.item isn't set on the prefab
    }

    private bool HasSeedFlag(SCItem item)
    {
        if (item == null) return false;
        return item.isSeed;
    }

    [System.Serializable]
    private class PlotState
    {
        public bool isOccupied;
        public bool requiresWater;
        public bool isGrowing;
        public float plannedGrowthTime;
        public SCItem currentSeed;
        public GameObject seedMarkerInstance;
        public GameObject grownInstance;
        public Coroutine growthRoutine;
        public float growthEndTime;
        public GameObject countdownInstance;
        public TMP_Text countdownTMP;
        public Text countdownText;

        public void Reset()
        {
            isOccupied = false;
            requiresWater = false;
            isGrowing = false;
            plannedGrowthTime = 0f;
            currentSeed = null;
            if (seedMarkerInstance != null)
            {
                Object.Destroy(seedMarkerInstance);
                seedMarkerInstance = null;
            }
            if (grownInstance != null)
            {
                Object.Destroy(grownInstance);
                grownInstance = null;
            }
            if (countdownInstance != null)
            {
                Object.Destroy(countdownInstance);
                countdownInstance = null;
            }
            countdownTMP = null;
            countdownText = null;
            growthRoutine = null;
            growthEndTime = 0f;
        }
    }

    // ------- UI Status helpers -------
    private int CountReady()
    {
        int ready = 0;
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            if (_plots[i] != null && _plots[i].grownInstance != null) ready++;
        }
        return ready;
    }

    private int CountGrowing()
    {
        int growing = 0;
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            var s = _plots[i];
            if (s != null && s.isOccupied && s.isGrowing && s.grownInstance == null) growing++;
        }
        return growing;
    }

    private int CountEmpty()
    {
        int empty = 0;
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            var s = _plots[i];
            if (s != null && !s.isOccupied) empty++;
        }
        return empty;
    }

    private int CountWaiting()
    {
        int waiting = 0;
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            var s = _plots[i];
            if (s != null && s.isOccupied && !s.isGrowing && s.grownInstance == null) waiting++;
        }
        return waiting;
    }

    private void UpdateStatusText()
    {
        int total = plotPoints != null ? plotPoints.Count : 0;
        int ready = CountReady();
        if (useWateringAwareStatus && showDetailedStatus)
        {
            int growing = CountGrowing();
            int waiting = CountWaiting();
            int empty = total - ready - growing - waiting;
            string msg = string.Format(statusFormatWatering, ready, growing, waiting, empty);
            if (statusTMP != null) statusTMP.text = msg;
            if (statusTextUI != null) statusTextUI.text = msg;
        }
        else if (showDetailedStatus)
        {
            int growing = CountGrowing();
            int empty = total - ready - growing;
            string msg = string.Format(statusFormatDetailed, ready, growing, empty);
            if (statusTMP != null) statusTMP.text = msg;
            if (statusTextUI != null) statusTextUI.text = msg;
        }
        else
        {
            string msg = string.Format(statusFormatSimple, ready, total);
            if (statusTMP != null) statusTMP.text = msg;
            if (statusTextUI != null) statusTextUI.text = msg;
        }
    }

    // ------- Editor Gizmos -------
    private void OnDrawGizmos()
    {
        if (plotPoints == null) return;
        for (int i = 0; i < plotPoints.Count; i++)
        {
            var p = plotPoints[i];
            if (p == null) continue;

            Color c = Color.gray;
            bool occupied = false;
            bool ready = false;
            if (Application.isPlaying && i < _plots.Count && _plots[i] != null)
            {
                occupied = _plots[i].isOccupied;
                ready = _plots[i].grownInstance != null;
            }
            if (ready) c = Color.green;
            else if (occupied) c = Color.yellow;
            else c = Color.gray;

            Gizmos.color = c;
            Gizmos.DrawSphere(p.position + Vector3.up * 0.02f, 0.08f);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
            Gizmos.DrawWireSphere(p.position + Vector3.up * 0.02f, 0.12f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = c;
            UnityEditor.Handles.Label(p.position + Vector3.up * 0.2f, $"{i}");
#endif
            Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
            Gizmos.DrawLine(transform.position, p.position);
        }
    }

    // ------- Countdown helpers -------
    private void CreateOrUpdateCountdown(int plotIndex, bool startTimer = true, string customText = null)
    {
        if (countdownTextPrefab == null || plotIndex < 0 || plotIndex >= plotPoints.Count) return;
        var state = _plots[plotIndex];
        var point = plotPoints[plotIndex];
        if (state == null || point == null) return;

        if (state.countdownInstance == null)
        {
            state.countdownInstance = Instantiate(countdownTextPrefab, point.position + spawnOffset + countdownOffset, countdownTextPrefab.transform.rotation);
            state.countdownTMP = state.countdownInstance.GetComponentInChildren<TMP_Text>();
            state.countdownText = state.countdownInstance.GetComponentInChildren<Text>();
        }

        if (!string.IsNullOrEmpty(customText))
        {
            if (state.countdownTMP != null) state.countdownTMP.text = customText;
            if (state.countdownText != null) state.countdownText.text = customText;
        }
        else
        {
            UpdateCountdownText(plotIndex);
        }

        if (startTimer)
        {
            StartCoroutine(CountdownRoutine(plotIndex));
        }
    }

    private IEnumerator CountdownRoutine(int plotIndex)
    {
        while (plotIndex >= 0 && plotIndex < _plots.Count)
        {
            var s = _plots[plotIndex];
            if (s == null || s.grownInstance != null || !s.isOccupied || !s.isGrowing) break;
            UpdateCountdownText(plotIndex);
            if (Time.time >= s.growthEndTime) break;
            yield return new WaitForSeconds(0.25f);
        }
        UpdateCountdownText(plotIndex);
    }

    private void UpdateCountdownText(int plotIndex)
    {
        if (plotIndex < 0 || plotIndex >= _plots.Count) return;
        var s = _plots[plotIndex];
        if (s == null) return;
        string msg;
        if (!s.isGrowing && s.isOccupied && s.grownInstance == null)
        {
            msg = "Waiting";
        }
        else
        {
            float remaining = Mathf.Max(0f, s.growthEndTime - Time.time);
            msg = FormatTime(remaining);
        }
        if (s.countdownTMP != null) s.countdownTMP.text = msg;
        if (s.countdownText != null) s.countdownText.text = msg;
    }

    private void DestroyCountdown(int plotIndex)
    {
        if (plotIndex < 0 || plotIndex >= _plots.Count) return;
        var s = _plots[plotIndex];
        if (s == null) return;
        s.countdownTMP = null;
        s.countdownText = null;
        if (s.countdownInstance != null)
        {
            Destroy(s.countdownInstance);
            s.countdownInstance = null;
        }
    }

    private string FormatTime(float t)
    {
        int seconds = Mathf.CeilToInt(t);
        int m = seconds / 60;
        int s = seconds % 60;
        if (m > 0) return string.Format("{0}:{1:00}", m, s);
        return s.ToString();
    }

    // ------- Watering logic -------
    private bool HasAnyWaiting()
    {
        for (int i = 0; i < _plots.Count; i++)
        {
            var s = _plots[i];
            if (s != null && s.isOccupied && !s.isGrowing && s.grownInstance == null) return true;
        }
        return false;
    }

    private void UpdateWateringPromptAndHandleInput()
    {
        // Get carried bucket (if any)
        var bucket = BucketManager.CurrentCarried;
        bool canWater = bucket != null && bucket.IsCarried && bucket.IsFilled && HasAnyWaiting();

        bool shouldShow = false;
        if (canWater)
        {
            Vector3 center = bucket.player != null ? bucket.player.position : bucket.transform.position;
            float baseRange = Mathf.Max(0.1f, wateringRange);

            // Throttle prompt updates
            float currentTime = Time.unscaledTime;
            bool timeToUpdate = currentTime - _lastUIUpdateTime >= UI_UPDATE_INTERVAL;
            float enter = baseRange + RANGE_HYSTERESIS;
            float exit = baseRange - RANGE_HYSTERESIS;

            float sqr = (transform.position - center).sqrMagnitude;
            if (!_inRangeSticky && sqr <= enter * enter) _inRangeSticky = true;
            else if (_inRangeSticky && sqr > exit * exit) _inRangeSticky = false;

            shouldShow = _inRangeSticky;

            if (timeToUpdate)
            {
                _lastUIUpdateTime = currentTime;
                if (wateringPromptUI != null && wateringPromptUI.activeSelf != shouldShow)
                    wateringPromptUI.SetActive(shouldShow);
                _lastPromptState = shouldShow;
            }
            else
            {
                if (wateringPromptUI != null && wateringPromptUI.activeSelf != _lastPromptState)
                    wateringPromptUI.SetActive(_lastPromptState);
            }

            // Handle input
            if (shouldShow && Input.GetKeyDown(waterKey))
            {
                int started = WaterAllWaiting(bucket);
                if (started > 0)
                {
                    // Consume the bucket's water (compatible even if method is absent)
                    ConsumeBucketWater(bucket);
                }
            }
        }
        else
        {
            _inRangeSticky = false;
            if (wateringPromptUI != null && wateringPromptUI.activeSelf)
                wateringPromptUI.SetActive(false);
        }
    }

    private int WaterAllWaiting(BucketManager bucket)
    {
        int count = 0;
        if (wateringVFX != null)
        {
            wateringVFX.SetActive(false);
            wateringVFX.SetActive(true);
        }
        for (int i = 0; i < _plots.Count; i++)
        {
            var s = _plots[i];
            if (s == null) continue;
            if (!s.isOccupied || s.isGrowing || s.grownInstance != null) continue;

            // Start growth for this plot
            s.isGrowing = true;
            s.requiresWater = false;
            s.growthEndTime = Time.time + Mathf.Max(0.01f, s.plannedGrowthTime);
            // Update countdown UI and timer
            CreateOrUpdateCountdown(i, startTimer: true);
            s.growthRoutine = StartCoroutine(GrowAndSpawn(i, s.plannedGrowthTime));
            count++;
        }
        if (count > 0) UpdateStatusText();
        return count;
    }

    private void ConsumeBucketWater(BucketManager bucket)
    {
        if (bucket == null) return;
        // Prefer a public API if available
        var mi = typeof(BucketManager).GetMethod("TryConsumeAllWater", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { mi.Invoke(bucket, null); return; } catch { /* fall through */ }
        }

        // Fallback: set private isFilled=false and call private ApplyVisual + RefreshCollidersAndPhysicsState
        try
        {
            var fi = typeof(BucketManager).GetField("isFilled", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null) fi.SetValue(bucket, false);

            var applyVisual = typeof(BucketManager).GetMethod("ApplyVisual", BindingFlags.NonPublic | BindingFlags.Instance);
            if (applyVisual != null) applyVisual.Invoke(bucket, null);

            var refresh = typeof(BucketManager).GetMethod("RefreshCollidersAndPhysicsState", BindingFlags.NonPublic | BindingFlags.Instance);
            if (refresh != null) refresh.Invoke(bucket, null);

            // Invoke public emptied event if present
            try
            {
                // Access the public field onEmptied and invoke if not null
                var evtField = typeof(BucketManager).GetField("onEmptied", BindingFlags.Public | BindingFlags.Instance);
                var evt = evtField != null ? evtField.GetValue(bucket) as UnityEngine.Events.UnityEvent : null;
                evt?.Invoke();
            }
            catch { }
        }
        catch { }
    }
}
