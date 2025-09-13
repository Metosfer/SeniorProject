using System;
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
public class FarmingAreaManager : MonoBehaviour, IDropHandler, ISaveable
{
    [Header("Planting Spots")]
    [Tooltip("Planting points (empty GameObjects). The first free slot is chosen.")]
    public List<Transform> plotPoints = new List<Transform>();

    [Header("Soil Visuals")]
    [Tooltip("Her plot için SoilPlace kök objesi (Renderer/SpriteRenderer içerebilir)")]
    public List<GameObject> soilPlaceRoots = new List<GameObject>();
    [Range(0f,1f)] public float unpreparedAlpha = 0.5f;
    [Range(0f,1f)] public float preparedAlpha = 1.0f;
    [Tooltip("Alpha yerine renk tonu (tint) kullan (hazır değil: açık, hazır: koyu)")]
    public bool useTintForSoil = true;
    [Tooltip("Hazır DEĞİLKEN uygulanacak renk tonu (açık). Sprite/Mat rengi ile çarpılır.")]
    public Color unpreparedTint = Color.white;
    [Tooltip("Hazırken uygulanacak renk tonu (koyu). Örn: 0.6,0.6,0.6,1")]
    public Color preparedTint = new Color(0.6f, 0.6f, 0.6f, 1f);
    [Header("Soil Auto-Assign")]
    [Tooltip("Inspector listesi boş/eksikse SoilPlace köklerini otomatik doldur")] public bool autoAssignSoilPlace = true;
    [Tooltip("Soil araması için kök (boşsa bu GameObject altında aranır)")] public Transform soilSearchRoot;
    [Tooltip("İsim filtrelemesi (içeriyorsa önceliklendir)")] public string soilNameContains = "Soil";

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
    [Tooltip("Max toplam hızlandırma (s) bir seferde uygulanabilecek")] public float maxBoostPerRun = 10f;

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

    [Header("Preparation (Rake / Harrow)")]
    [Tooltip("Key to prepare (harrow) empty plots when in range with rake equipped.")]
    public KeyCode prepareKey = KeyCode.E;
    [Tooltip("Max distance from player to allow preparing (harrowing) plots.")]
    public float prepareRange = 2.5f;
    [Tooltip("Optional UI prompt to show when preparing is possible (e.g., 'Press E to prepare')")] 
    public GameObject preparePromptUI;

    [Header("Status Formatting (Detailed)")]
    [Tooltip("If true, use extended detailed status including waiting.")]
    public bool useWateringAwareStatus = true;
    [Tooltip("Detailed status format when watering is used: {0}=ready, {1}=growing, {2}=waiting, {3}=empty")]
    public string statusFormatWatering = "Ready: {0} | Growing: {1} | Waiting: {2} | Empty: {3}";

    [Header("Save")]
    [Tooltip("Persistent ID for save/load. Set a unique value if you have multiple farming areas.")]
    public string saveId;


    // Internal state tracking
    private readonly List<PlotState> _plots = new List<PlotState>();

    // Throttled prompt update like WellManager
    private const float UI_UPDATE_INTERVAL = 0.1f;
    private const float RANGE_HYSTERESIS = 0.2f;
    private float _lastUIUpdateTime;
    private bool _inRangeSticky;
    private bool _lastPromptState;
    // Anti-flicker and input-grace controls
    private const float PROMPT_MIN_ON_DURATION = 0.25f;
    private const float PROMPT_MIN_OFF_DURATION = 0.2f;
    private const float INPUT_GRACE_WINDOW = 0.2f;
    private float _lastPromptStateChangeTime;
    private float _pendingWaterPressUntil;
    // Preparation prompt state
    private float _lastPrepareUIUpdateTime;
    private bool _inPrepareRangeSticky;
    private bool _lastPreparePromptState;
    private float _lastPreparePromptStateChangeTime;
    private float _pendingPreparePressUntil;

    private void Awake()
    {
    SyncPlotStatesWithPoints();
    TryAutoAssignSoilPlaceRoots();
        UpdateStatusText();
        // Ensure prompts/VFX start disabled to avoid lingering after load
        if (wateringPromptUI != null && wateringPromptUI.activeSelf)
            wateringPromptUI.SetActive(false);
        if (wateringVFX != null && wateringVFX.activeSelf)
            wateringVFX.SetActive(false);

    }
    
    private void OnValidate()
    {
    SyncPlotStatesWithPoints();
    TryAutoAssignSoilPlaceRoots();
    }

    private void Update()
    {
        // Suppress all interactions if a modal (e.g., Market) is open
        if (MarketManager.IsAnyOpen)
        {
            if (wateringPromptUI != null && wateringPromptUI.activeSelf) wateringPromptUI.SetActive(false);
            if (preparePromptUI != null && preparePromptUI.activeSelf) preparePromptUI.SetActive(false);
            return;
        }

    // Watering prompt + input handling (integrates with BucketManager)
    UpdateWateringPromptAndHandleInput();
    // Preparation prompt + input handling (integrates with HarrowManager)
    UpdatePreparePromptAndHandleInput();
    }

    // Rhythm Game integration: apply a time reduction boost to seeds
    // amountSeconds: how many seconds to reduce. Applies to currently growing plots (reduces remaining time)
    // and to waiting plots (reduces planned growth time so they finish sooner once watered).
    public float ApplyGrowthBoost(float amountSeconds)
    {
        if (amountSeconds <= 0f) return 0f;
        float applied = 0f;
        float budget = maxBoostPerRun > 0f ? Mathf.Min(amountSeconds, maxBoostPerRun) : amountSeconds;
        for (int i = 0; i < _plots.Count && budget > 0f; i++)
        {
            var s = _plots[i]; if (s == null) continue;
            if (s.isOccupied && !s.isReady)
            {
                if (s.isGrowing)
                {
                    // Reduce remaining time directly, not below zero
                    float remain = Mathf.Max(0f, s.growthEndTime - Time.time);
                    float delta = Mathf.Min(remain, budget);
                    if (delta > 0f)
                    {
                        s.growthEndTime -= delta;
                        // If countdown exists, it will reflect new remaining automatically
                        applied += delta; budget -= delta;

                        // If time is up after boost, finish this plot immediately
                        float remainAfter = Mathf.Max(0f, s.growthEndTime - Time.time);
                        if (remainAfter <= 0.01f)
                        {
                            if (s.growthRoutine != null)
                            {
                                StopCoroutine(s.growthRoutine);
                                s.growthRoutine = null;
                            }
                            CompleteGrowthNow(i);
                        }
                        else
                        {
                            // Time reduced but not zero: restart growth to honor the new remaining time
                            if (s.growthRoutine != null)
                            {
                                StopCoroutine(s.growthRoutine);
                                s.growthRoutine = null;
                            }
                            s.growthRoutine = StartCoroutine(GrowAndSpawn(i, remainAfter));
                        }
                    }
                }
                else if (s.requiresWater)
                {
                    // Not growing yet: reduce planned growth time
                    float planned = Mathf.Max(0.01f, s.plannedGrowthTime);
                    float delta = Mathf.Min(planned * 0.9f, budget); // keep tiny floor
                    if (delta > 0f)
                    {
                        s.plannedGrowthTime = Mathf.Max(0.01f, planned - delta);
                        applied += delta; budget -= delta;
                        // Update text if showing "Waiting"
                        CreateOrUpdateCountdown(i, startTimer: false, customText: "Waiting");
                    }
                }
            }
        }
        if (applied > 0f)
        {
            UpdateStatusText();
        }
        return applied;
    }

    // Immediately complete growth for the given plot (spawn grown plant etc.)
    private void CompleteGrowthNow(int plotIndex)
    {
        if (plotIndex < 0 || plotIndex >= _plots.Count) return;
        var state = _plots[plotIndex];
        if (state == null || !state.isOccupied || state.isReady) return;
        var point = plotPoints[plotIndex]; if (point == null) return;

        // Clear countdown UI
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
            var instanceRoot = Instantiate(grownPrefab, point.position + spawnOffset, grownPrefab.transform.rotation);

            // Prefer existing Plant anywhere in the hierarchy; otherwise add one on root
            Plant[] plants = instanceRoot.GetComponentsInChildren<Plant>(true);
            Plant plant = null;
            if (plants != null && plants.Length > 0)
            {
                plant = plants[0];
                // Destroy any duplicate Plant components beyond the first to prevent multi-pickup
                for (int k = 1; k < plants.Length; k++)
                {
                    if (plants[k] != null) Destroy(plants[k]);
                }
            }
            else
            {
                plant = instanceRoot.AddComponent<Plant>();
            }

            // Ensure the collider lives with the Plant object; create if missing
            GameObject plantGO = plant.gameObject;
            var plantCol = plantGO.GetComponent<Collider>();
            if (plantCol == null)
            {
                plantCol = plantGO.AddComponent<BoxCollider>();
            }
            plantCol.isTrigger = true;

            // Optional rigidbody on the Plant holder for stable trigger behavior
            var rb = plantGO.GetComponent<Rigidbody>();
            if (rb == null) rb = plantGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Assign harvest item if not set
            if (plant.item == null)
            {
                var harvest = GetHarvestItem(state.currentSeed);
                if (harvest != null) plant.item = harvest;
            }

            // Track the actual Plant gameObject so when it's destroyed, we can reset the plot
            // Mark as ready for harvest
            state.isGrowing = false;
            state.isReady = true;
            state.grownInstance = plantGO;
        }
        else
        {
            Debug.LogWarning("FarmingArea: Grown prefab not found; spawn skipped.");
        }

        // Start monitoring harvest and refresh UI
        StartCoroutine(WatchForHarvest(plotIndex));
        UpdateStatusText();
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
        // SoilPlace görsellerini senkronize et
        UpdateAllSoilPlaceVisuals();
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
        // Require prepared plot before planting
        if (!state.isPrepared)
        {
            Debug.Log("FarmingArea: Plot is not prepared. Use rake to prepare before planting.");
            return false;
        }

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

        // Remove marker (growth completed)
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
            var instanceRoot = Instantiate(grownPrefab, point.position + spawnOffset, grownPrefab.transform.rotation);

            // Prefer existing Plant anywhere in the hierarchy; otherwise add one on root
            Plant[] plants = instanceRoot.GetComponentsInChildren<Plant>(true);
            Plant plant = null;
            if (plants != null && plants.Length > 0)
            {
                plant = plants[0];
                // Destroy any duplicate Plant components beyond the first to prevent multi-pickup
                for (int k = 1; k < plants.Length; k++)
                {
                    if (plants[k] != null) Destroy(plants[k]);
                }
            }
            else
            {
                plant = instanceRoot.AddComponent<Plant>();
            }

            // Ensure the collider lives with the Plant object; create if missing
            GameObject plantGO = plant.gameObject;
            var plantCol = plantGO.GetComponent<Collider>();
            if (plantCol == null)
            {
                plantCol = plantGO.AddComponent<BoxCollider>();
            }
            plantCol.isTrigger = true;

            // Optional rigidbody on the Plant holder for stable trigger behavior
            var rb = plantGO.GetComponent<Rigidbody>();
            if (rb == null) rb = plantGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Assign harvest item if not set
            if (plant.item == null)
            {
                var harvest = GetHarvestItem(state.currentSeed);
                if (harvest != null) plant.item = harvest;
            }

            // Track the actual Plant gameObject so when it's destroyed, we can reset the plot
            // Mark as ready for harvest
            state.isGrowing = false;
            state.isReady = true;
            state.grownInstance = plantGO;
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
            // Frame-accurate harvest detection for timely animation
            yield return null;
        }
        // Harvested: ensure seed marker is removed if still present
        if (state.seedMarkerInstance != null)
        {
            Destroy(state.seedMarkerInstance);
            state.seedMarkerInstance = null;
        }
        state.Reset();
    // After harvesting, require re-preparation
    state.isPrepared = false;
        UpdateSoilPlaceVisual(plotIndex);
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
    public bool isPrepared; // Must be true to allow planting
        public bool isOccupied;
        public bool requiresWater;
        public bool isGrowing;
        public bool isReady;           // Persistent flag: growth is complete and plant should exist
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
            // Do not clear isPrepared here; harvest will explicitly clear it
            isOccupied = false;
            requiresWater = false;
            isGrowing = false;
            isReady = false;
            plannedGrowthTime = 0f;
            currentSeed = null;
            if (seedMarkerInstance != null)
            {
                Destroy(seedMarkerInstance);
                seedMarkerInstance = null;
            }
            if (grownInstance != null)
            {
                Destroy(grownInstance);
                grownInstance = null;
            }
            if (countdownInstance != null)
            {
                Destroy(countdownInstance);
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
            if (_plots[i] != null && _plots[i].isReady) ready++;
        }
        return ready;
    }

    private int CountGrowing()
    {
        int growing = 0;
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            var s = _plots[i];
            if (s != null && s.isOccupied && s.isGrowing && !s.isReady) growing++;
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
            if (s != null && s.isOccupied && !s.isGrowing && !s.isReady) waiting++;
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

    // Public helper to check if there is at least one planted seed (waiting or growing)
    public bool HasAnyPlantedSeed()
    {
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            var s = _plots[i];
            if (s != null && s.isOccupied && !s.isReady)
            {
                return true; // includes waiting (requiresWater) and growing
            }
        }
        return false;
    }

    // Public helper to check if there is at least one seed currently growing (watered, timer running)
    public bool HasAnyGrowingSeed()
    {
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            var s = _plots[i];
            if (s != null && s.isOccupied && s.isGrowing && !s.isReady)
            {
                return true;
            }
        }
        return false;
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
                ready = _plots[i].isReady;
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
            if (s == null || s.isReady || !s.isOccupied || !s.isGrowing) break;
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
        if (!s.isGrowing && s.isOccupied && !s.isReady)
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

    // ===== ISaveable =====
    public Dictionary<string, object> GetSaveData()
    {
        var data = new Dictionary<string, object>();
        data["plotCount"] = plotPoints != null ? plotPoints.Count : 0;
        for (int i = 0; i < _plots.Count; i++)
        {
            var s = _plots[i];
            if (s == null) continue;
            string prefix = $"Plot{i}.";
            data[prefix + "prepared"] = s.isPrepared;
            data[prefix + "occupied"] = s.isOccupied;
            data[prefix + "waiting"] = s.isOccupied && !s.isGrowing && !s.isReady;
            data[prefix + "growing"] = s.isGrowing;
            data[prefix + "ready"] = s.isReady;
            data[prefix + "seedName"] = s.currentSeed != null ? s.currentSeed.itemName : "";
            data[prefix + "planned"] = s.plannedGrowthTime;
            // Save remaining time if growing
            float remaining = s.isGrowing ? Mathf.Max(0f, s.growthEndTime - Time.time) : 0f;
            data[prefix + "remaining"] = remaining;
        }
        return data;
    }

    public void LoadSaveData(Dictionary<string, object> data)
    {
        if (data == null) return;
        // Stop any watering visuals on load
        if (wateringVFX != null && wateringVFX.activeSelf)
            wateringVFX.SetActive(false);
        if (wateringPromptUI != null && wateringPromptUI.activeSelf)
            wateringPromptUI.SetActive(false);
        int count = plotPoints != null ? plotPoints.Count : 0;
        // Clear any running growth
        for (int i = 0; i < _plots.Count; i++)
        {
            var s = _plots[i];
            if (s == null) continue;
            if (s.growthRoutine != null) StopCoroutine(s.growthRoutine);
            s.Reset();
        }

        for (int i = 0; i < count && i < _plots.Count; i++)
        {
            var s = _plots[i];
            string prefix = $"Plot{i}.";

            // Restore prepared state and visuals regardless of occupancy
            s.isPrepared = GetBool(data, prefix + "prepared");
            UpdateSoilPlaceVisual(i);

            bool occupied = GetBool(data, prefix + "occupied");
            if (!occupied) continue;

            bool waiting = GetBool(data, prefix + "waiting");
            bool growing = GetBool(data, prefix + "growing");
            bool ready = GetBool(data, prefix + "ready");
            string seedName = GetString(data, prefix + "seedName");
            float planned = GetFloat(data, prefix + "planned", defaultGrowthTime);
            float remaining = GetFloat(data, prefix + "remaining", 0f);

            s.isOccupied = true;
            s.isReady = ready;
            s.plannedGrowthTime = Mathf.Max(0.01f, planned);
            s.currentSeed = !string.IsNullOrEmpty(seedName) ? FindItemByName(seedName) : null;

            var point = plotPoints[i];

            if (ready)
            {
                // Ready plants must be on prepared/occupied plots
                s.isPrepared = true;
                s.isOccupied = true;
                UpdateSoilPlaceVisual(i);
                
                // Always spawn the grown plant for ready plots to ensure persistence
                var grownPrefab = GetGrownPrefab(s.currentSeed);
                if (grownPrefab != null)
                {
                    var instanceRoot = Instantiate(grownPrefab, point.position + spawnOffset, grownPrefab.transform.rotation);
                    // Choose Plant component in children if present; else add to root
                    Plant[] plants = instanceRoot.GetComponentsInChildren<Plant>(true);
                    Plant plant = null;
                    if (plants != null && plants.Length > 0)
                    {
                        plant = plants[0];
                        for (int k = 1; k < plants.Length; k++)
                        {
                            if (plants[k] != null) Destroy(plants[k]);
                        }
                    }
                    else
                    {
                        plant = instanceRoot.AddComponent<Plant>();
                    }
                    var plantGO = plant.gameObject;
                    var plantCol = plantGO.GetComponent<Collider>();
                    if (plantCol == null) plantCol = plantGO.AddComponent<BoxCollider>();
                    plantCol.isTrigger = true;
                    var rb2 = plantGO.GetComponent<Rigidbody>();
                    if (rb2 == null) rb2 = plantGO.AddComponent<Rigidbody>();
                    rb2.isKinematic = true;
                    rb2.useGravity = false;
                    if (plant.item == null)
                    {
                        var harvest = GetHarvestItem(s.currentSeed);
                        if (harvest != null) plant.item = harvest;
                    }
                    s.grownInstance = plantGO;
                }
                else
                {
                    Debug.LogWarning($"Ready plot {i} couldn't resolve grown prefab for seed '{seedName}'. Plot may appear empty.");
                }
                DestroyCountdown(i);
                // Ensure harvest monitoring for loaded ready plants
                StartCoroutine(WatchForHarvest(i));
            }
            else if (growing)
            {
                s.isGrowing = true;
                s.requiresWater = false;
                s.growthEndTime = Time.time + Mathf.Max(0.01f, remaining);
                CreateOrUpdateCountdown(i, startTimer: true);
                s.growthRoutine = StartCoroutine(GrowAndSpawn(i, remaining > 0f ? remaining : s.plannedGrowthTime));
                // Show marker while growing
                if (seedMarkerPrefab != null)
                    s.seedMarkerInstance = Instantiate(seedMarkerPrefab, point.position + spawnOffset, seedMarkerPrefab.transform.rotation);
            }
            else if (waiting)
            {
                s.isGrowing = false;
                s.requiresWater = true;
                s.growthEndTime = 0f;
                CreateOrUpdateCountdown(i, startTimer: false, customText: "Waiting");
                // Show marker while waiting for water
                if (seedMarkerPrefab != null)
                    s.seedMarkerInstance = Instantiate(seedMarkerPrefab, point.position + spawnOffset, seedMarkerPrefab.transform.rotation);
            }
        }

        UpdateStatusText();
    }

    private static bool GetBool(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var v) || v == null) return false;
        bool.TryParse(v.ToString(), out bool b); return b;
    }
    private static float GetFloat(Dictionary<string, object> data, string key, float def)
    {
        if (!data.TryGetValue(key, out var v) || v == null) return def;
        float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f); return f;
    }
    private static string GetString(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var v) || v == null) return string.Empty;
        return v.ToString();
    }
    private static SCItem FindItemByName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        SCItem[] all = Resources.LoadAll<SCItem>("");
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].itemName == itemName) return all[i];
        }
        return null;
    }

    private static Plant FindNearestPlant(Vector3 position, float maxDistance)
    {
        Plant[] plants = GameObject.FindObjectsOfType<Plant>();
        Plant best = null;
        float bestSqr = maxDistance * maxDistance;
        for (int i = 0; i < plants.Length; i++)
        {
            var p = plants[i];
            float sqr = (p.transform.position - position).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = p;
            }
        }
        return best;
    }

    // Remove duplicate Plant components in a hierarchy leaving only one
    private static void CleanupDuplicatePlantComponents(GameObject root)
    {
        if (root == null) return;
        var plants = root.GetComponentsInChildren<Plant>(true);
        if (plants == null || plants.Length <= 1) return;
        for (int i = 1; i < plants.Length; i++)
        {
            if (plants[i] != null)
            {
                Destroy(plants[i]);
            }
        }
    }

    // ------- SoilPlace visuals -------
    private void UpdateAllSoilPlaceVisuals()
    {
        int n = _plots != null ? _plots.Count : 0;
        for (int i = 0; i < n; i++) UpdateSoilPlaceVisual(i);
    }

    private void UpdateSoilPlaceVisual(int index)
    {
        if (soilPlaceRoots == null) return;
        if (index < 0 || index >= soilPlaceRoots.Count) return;
        var root = soilPlaceRoots[index];
        if (root == null) return;
        bool prepared = index < _plots.Count && _plots[index] != null && _plots[index].isPrepared;
        if (useTintForSoil)
        {
            var tint = prepared ? preparedTint : unpreparedTint;
            ApplyTintToRoot(root, tint);
        }
        else
        {
            float a = prepared ? preparedAlpha : unpreparedAlpha;
            ApplyAlphaToRoot(root, a);
        }
    }

    private static void ApplyAlphaToRoot(GameObject root, float a)
    {
        // SpriteRenderer: component-level color değişimi (materyali değiştirmez)
        var sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var c = sr.color; c.a = a; sr.color = c;
        }

        // Mesh/Skinned renderers: MaterialPropertyBlock ile sadece renk alfa değeri
        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i]; if (r == null) continue;
            var sharedMats = r.sharedMaterials; if (sharedMats == null || sharedMats.Length == 0) continue;

            // Baz renk ve property ismi tespiti (yalnızca sharedMaterial'dan okuma; materyali instantiate etmeyiz)
            string colorProp = null;
            Color baseCol = Color.white;
            var refMat = sharedMats[0];
            if (refMat != null)
            {
                if (refMat.HasProperty("_BaseColor")) { colorProp = "_BaseColor"; baseCol = refMat.GetColor("_BaseColor"); }
                else if (refMat.HasProperty("_Color")) { colorProp = "_Color"; baseCol = refMat.color; }
            }
            if (string.IsNullOrEmpty(colorProp)) continue;

            var mpb = new MaterialPropertyBlock();
            // Not: aynı MPB'yi tüm indexler için kullanıp set etmek yeterli
            var newCol = new Color(baseCol.r, baseCol.g, baseCol.b, a);
            mpb.SetColor(colorProp, newCol);

            // Tüm materyal indexleri için uygula
            for (int mi = 0; mi < sharedMats.Length; mi++)
            {
                r.SetPropertyBlock(mpb, mi);
            }
        }
    }

    private static void ApplyTintToRoot(GameObject root, Color tint)
    {
        // SpriteRenderer: sr.color çarpan gibi çalışır; RGB'yi tint ile çarp, alpha'yı koru
        var sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var baseC = sr.color;
            var target = new Color(baseC.r * tint.r, baseC.g * tint.g, baseC.b * tint.b, baseC.a);
            sr.color = target;
        }

        // Mesh/Skinned renderers: _BaseColor/_Color'ı tint ile çarpıp PropertyBlock üzerinden uygula
        var rends = root.GetComponentsInChildren<Renderer>(true);
        var mpb = new MaterialPropertyBlock();
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i]; if (r == null) continue;
            var sharedMats = r.sharedMaterials; if (sharedMats == null || sharedMats.Length == 0) continue;
            var refMat = sharedMats[0]; if (refMat == null) continue;
            string colorProp = null;
            if (refMat.HasProperty("_BaseColor")) colorProp = "_BaseColor";
            else if (refMat.HasProperty("_Color")) colorProp = "_Color";
            if (string.IsNullOrEmpty(colorProp)) continue;

            Color baseCol = refMat.GetColor(colorProp);
            Color target = new Color(baseCol.r * tint.r, baseCol.g * tint.g, baseCol.b * tint.b, baseCol.a);
            r.GetPropertyBlock(mpb);
            mpb.SetColor(colorProp, target);
            r.SetPropertyBlock(mpb);
        }
    }

    // ------- Soil auto-assign helpers -------
    private void TryAutoAssignSoilPlaceRoots()
    {
        if (!autoAssignSoilPlace) return;
        int plots = plotPoints != null ? plotPoints.Count : 0;
        if (plots == 0) return;
        if (soilPlaceRoots != null && soilPlaceRoots.Count == plots && soilPlaceRoots.TrueForAll(go => go != null)) return;

        var root = soilSearchRoot != null ? soilSearchRoot : transform;
        var candidates = new List<GameObject>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == root) continue;
            if (plotPoints != null && plotPoints.Contains(t)) continue; // skip plot points themselves
            bool hasRenderer = t.GetComponentInChildren<Renderer>(true) != null || t.GetComponent<SpriteRenderer>() != null;
            if (!hasRenderer) continue;
            candidates.Add(t.gameObject);
        }
        if (!string.IsNullOrEmpty(soilNameContains))
        {
            candidates.Sort((a,b) =>
            {
                bool am = a != null && a.name.IndexOf(soilNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
                bool bm = b != null && b.name.IndexOf(soilNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
                if (am == bm) return 0; return am ? -1 : 1;
            });
        }

        var assigned = new List<GameObject>(new GameObject[plots]);
        var used = new HashSet<GameObject>();
        for (int i = 0; i < plots; i++)
        {
            var p = plotPoints[i]; if (p == null) continue;
            float best = float.MaxValue; GameObject bestGo = null;
            for (int c = 0; c < candidates.Count; c++)
            {
                var go = candidates[c]; if (go == null || used.Contains(go)) continue;
                float d = (go.transform.position - p.position).sqrMagnitude;
                if (d < best)
                {
                    best = d; bestGo = go;
                }
            }
            assigned[i] = bestGo;
            if (bestGo != null) used.Add(bestGo);
        }
        soilPlaceRoots = assigned;
        UpdateAllSoilPlaceVisuals();
    }

    // ------- Watering logic -------
    private bool HasAnyWaiting()
    {
        for (int i = 0; i < _plots.Count; i++)
        {
            var s = _plots[i];
            if (s != null && s.isOccupied && !s.isGrowing && !s.isReady) return true;
        }
        return false;
    }

    private void UpdateWateringPromptAndHandleInput()
    {
        // If a modal is open, handled in Update() already, but guard again
        if (MarketManager.IsAnyOpen)
        {
            if (wateringPromptUI != null && wateringPromptUI.activeSelf) wateringPromptUI.SetActive(false);
            return;
        }
        // Get carried bucket (if any)
        var bucket = BucketManager.CurrentCarried;
        bool baseCanWater = bucket != null && bucket.IsCarried && bucket.IsFilled && HasAnyWaiting();

        // Buffer input so a quick flicker won't drop the press
        if (Input.GetKeyDown(waterKey))
        {
            _pendingWaterPressUntil = Time.unscaledTime + INPUT_GRACE_WINDOW;
        }

        bool desiredShow = false;
        if (baseCanWater)
        {
            // Use planar distance (ignore Y) to reduce jitter
            Vector3 pA = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 src = bucket.player != null ? bucket.player.position : bucket.transform.position;
            Vector3 pB = new Vector3(src.x, 0f, src.z);
            float baseRange = Mathf.Max(0.1f, wateringRange);

            // Throttle prompt updates
            float currentTime = Time.unscaledTime;
            bool timeToUpdate = currentTime - _lastUIUpdateTime >= UI_UPDATE_INTERVAL;
            float enter = baseRange + RANGE_HYSTERESIS;
            float exit = baseRange - RANGE_HYSTERESIS;

            float sqr = (pA - pB).sqrMagnitude;
            if (!_inRangeSticky && sqr <= enter * enter) _inRangeSticky = true;
            else if (_inRangeSticky && sqr > exit * exit) _inRangeSticky = false;

            desiredShow = _inRangeSticky;

            // Anti-flicker: enforce minimum on/off durations for the prompt
            if (timeToUpdate)
            {
                _lastUIUpdateTime = currentTime;
                if (wateringPromptUI != null)
                {
                    bool target = desiredShow;
                    float since = currentTime - _lastPromptStateChangeTime;
                    if (_lastPromptState && !target && since < PROMPT_MIN_ON_DURATION)
                    {
                        target = true; // keep on until min on duration
                    }
                    else if (!_lastPromptState && target && since < PROMPT_MIN_OFF_DURATION)
                    {
                        target = false; // keep off until min off duration
                    }

                    if (wateringPromptUI.activeSelf != target)
                    {
                        wateringPromptUI.SetActive(target);
                        _lastPromptState = target;
                        _lastPromptStateChangeTime = currentTime;
                    }
                }
            }
            else
            {
                if (wateringPromptUI != null && wateringPromptUI.activeSelf != _lastPromptState)
                {
                    wateringPromptUI.SetActive(_lastPromptState);
                }
            }

            // Handle input with grace window
            if (_lastPromptState || desiredShow)
            {
                if (Time.unscaledTime <= _pendingWaterPressUntil)
                {
                    _pendingWaterPressUntil = 0f;
                    // Watering logic also deducts charges internally (or falls back to empty when no API),
                    // so we must not consume again here to avoid double-deduction.
                    // Trigger watering animation on player if available
                    var anim = FindObjectOfType<PlayerAnimationController>();
                    if (anim != null) anim.TriggerWatering();
                    int started = WaterAllWaiting(bucket);
                    // No extra consumption here; WaterAllWaiting handles it.
                }
            }
        }
        else
        {
            _inRangeSticky = false;
            if (wateringPromptUI != null)
            {
                if (wateringPromptUI.activeSelf)
                {
                    wateringPromptUI.SetActive(false);
                    _lastPromptState = false;
                    _lastPromptStateChangeTime = Time.unscaledTime;
                }
            }
        }
    }

    // ------- Preparation logic (Rake/Harrow) -------
    private bool HasAnyUnpreparedEmpty()
    {
        for (int i = 0; i < _plots.Count; i++)
        {
            var s = _plots[i];
            if (s == null) continue;
            if (!s.isOccupied && !s.isPrepared) return true;
        }
        return false;
    }

    private int FindNearestUnpreparedEmptyInRange(Transform player, float range)
    {
        if (player == null) return -1;
        float best = range * range;
        int bestIdx = -1;
        for (int i = 0; i < plotPoints.Count; i++)
        {
            var p = plotPoints[i];
            var s = (i < _plots.Count) ? _plots[i] : null;
            if (p == null || s == null) continue;
            if (s.isOccupied || s.isPrepared) continue;
            Vector3 a = new Vector3(player.position.x, 0f, player.position.z);
            Vector3 b = new Vector3(p.position.x, 0f, p.position.z);
            float sqr = (a - b).sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    private void UpdatePreparePromptAndHandleInput()
    {
        // If watering is possible and bucket is filled, prioritize watering prompt over prepare
        var bucket = BucketManager.CurrentCarried;
        bool canWater = bucket != null && bucket.IsCarried && bucket.IsFilled && HasAnyWaiting();
        if (canWater)
        {
            if (preparePromptUI != null && preparePromptUI.activeSelf) preparePromptUI.SetActive(false);
            return;
        }

        // Require rake equipped
        if (!IsRakeEquipped() || !HasAnyUnpreparedEmpty())
        {
            _inPrepareRangeSticky = false;
            if (preparePromptUI != null && preparePromptUI.activeSelf) preparePromptUI.SetActive(false);
            return;
        }

        // Buffer input so a quick flicker won't drop the press
        if (Input.GetKeyDown(prepareKey))
        {
            _pendingPreparePressUntil = Time.unscaledTime + INPUT_GRACE_WINDOW;
        }

        // Use player position; try to get from bucket.player else by tag
        Transform player = bucket != null && bucket.player != null ? bucket.player : (GameObject.FindGameObjectWithTag("Player")?.transform);
        float baseRange = Mathf.Max(0.1f, prepareRange);

        // Throttle prompt updates
        float currentTime = Time.unscaledTime;
        bool timeToUpdate = currentTime - _lastPrepareUIUpdateTime >= UI_UPDATE_INTERVAL;
        float enter = baseRange + RANGE_HYSTERESIS;
        float exit = baseRange - RANGE_HYSTERESIS;

        // Choose nearest candidate and decide if in range
        int nearestIdx = FindNearestUnpreparedEmptyInRange(player, enter);
        bool desiredShow = nearestIdx >= 0;

        // Sticky in-range by measuring distance at center -> nearest plot point
        if (!_inPrepareRangeSticky)
        {
            _inPrepareRangeSticky = desiredShow;
        }
        else
        {
            // If we were in range, only drop if nothing within the tighter exit range
            int idxExit = FindNearestUnpreparedEmptyInRange(player, exit);
            _inPrepareRangeSticky = idxExit >= 0;
        }

        bool finalShow = _inPrepareRangeSticky;

        if (timeToUpdate)
        {
            _lastPrepareUIUpdateTime = currentTime;
            if (preparePromptUI != null)
            {
                bool target = finalShow;
                float since = currentTime - _lastPreparePromptStateChangeTime;
                if (_lastPreparePromptState && !target && since < PROMPT_MIN_ON_DURATION)
                {
                    target = true;
                }
                else if (!_lastPreparePromptState && target && since < PROMPT_MIN_OFF_DURATION)
                {
                    target = false;
                }
                if (preparePromptUI.activeSelf != target)
                {
                    preparePromptUI.SetActive(target);
                    _lastPreparePromptState = target;
                    _lastPreparePromptStateChangeTime = currentTime;
                }
            }
        }
        else
        {
            if (preparePromptUI != null && preparePromptUI.activeSelf != _lastPreparePromptState)
            {
                preparePromptUI.SetActive(_lastPreparePromptState);
            }
        }

        // Handle input with grace window
        if (_lastPreparePromptState || finalShow)
        {
            if (Time.unscaledTime <= _pendingPreparePressUntil)
            {
                _pendingPreparePressUntil = 0f;
                if (nearestIdx >= 0)
                {
                    PreparePlot(nearestIdx);
                }
            }
        }
    }

    public bool PreparePlot(int plotIndex)
    {
        if (plotIndex < 0 || plotIndex >= _plots.Count) return false;
        var s = _plots[plotIndex];
        if (s == null) return false;
        if (s.isOccupied || s.isPrepared) return false;
        s.isPrepared = true;
        // Optional small feedback: reuse marker if desired; keeping it simple for now
    UpdateSoilPlaceVisual(plotIndex);
    UpdateStatusText();
        return true;
    }

    private int WaterAllWaiting(BucketManager bucket)
    {
        int count = 0;
    if (wateringVFX != null)
        {
            wateringVFX.SetActive(false);
            wateringVFX.SetActive(true);
        }
    // Also trigger watering animation as we start watering action (safety)
    var anim = FindObjectOfType<PlayerAnimationController>();
    if (anim != null) anim.TriggerWatering();
        // Determine how many plots we can water based on bucket charges
        int maxToWater = int.MaxValue;
        var chargesProp = typeof(BucketManager).GetProperty("RemainingWaterCharges", BindingFlags.Public | BindingFlags.Instance);
        if (chargesProp != null)
        {
            try
            {
                maxToWater = Mathf.Max(0, (int)chargesProp.GetValue(bucket));
                if (maxToWater == 0) return 0; // no water available
            }
            catch { maxToWater = int.MaxValue; }
        }

        int startedThisPass = 0;
        for (int i = 0; i < _plots.Count; i++)
        {
            var s = _plots[i];
            if (s == null) continue;
            if (!s.isOccupied || s.isGrowing || s.isReady) continue;
            if (startedThisPass >= maxToWater) break;

            // Start growth for this plot
            s.isGrowing = true;
            s.requiresWater = false;
            s.growthEndTime = Time.time + Mathf.Max(0.01f, s.plannedGrowthTime);
            // Update countdown UI and timer
            CreateOrUpdateCountdown(i, startTimer: true);
            s.growthRoutine = StartCoroutine(GrowAndSpawn(i, s.plannedGrowthTime));
            count++;
            startedThisPass++;
        }
        if (count > 0)
        {
            // Deduct charges if API available; else consume whole water once
            var miConsume = typeof(BucketManager).GetMethod("TryConsumeWaterCharges", BindingFlags.Public | BindingFlags.Instance);
            if (miConsume != null)
            {
                try { miConsume.Invoke(bucket, new object[] { startedThisPass }); }
                catch { ConsumeBucketWater(bucket); }
            }
            else
            {
                ConsumeBucketWater(bucket);
            }
            UpdateStatusText();
        }
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

    private static bool IsRakeEquipped()
    {
        var t = System.Type.GetType("HarrowManager");
        if (t == null) return false;
        var prop = t.GetProperty("IsEquipped", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (prop == null) return false;
        try { return (bool)prop.GetValue(null); } catch { return false; }
    }
}
