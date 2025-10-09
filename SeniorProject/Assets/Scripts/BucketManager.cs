using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BucketManager : MonoBehaviour, ISaveable
{
    public static BucketManager Instance { get; private set; }
    // Single-owner cursor control to prevent flicker
    private static BucketManager s_cursorOwner;
    private static float s_lastCursorChangeTime;
    private const float CURSOR_DEBOUNCE = 0.1f; // Increased to reduce flicker
    private const float HOVER_CHECK_INTERVAL = 0.05f; // Check hover less frequently
    // Global reference to whichever bucket is currently carried (null if none)
    public static BucketManager CurrentCarried { get; private set; }
    
    private float _lastHoverCheckTime;
    private bool _lastHoverState;
    private Camera _cachedCamera;
    [Header("References")]
    [Tooltip("The water visual GameObject to toggle when the bucket is filled.")]
    public GameObject waterVisual;

    [Tooltip("Optional pickup prompt shown when near the bucket and not carrying it.")]
    public GameObject pickupPromptUI;
    [Tooltip("Optional fill prompt shown when near a well while carrying the bucket.")]
    public GameObject fillPromptUI;

    [Tooltip("Player transform used for distance checks. If null, uses tagged 'Player'.")]
    public Transform player;
    [Tooltip("Socket transform on the player hand where the bucket will attach.")]
    public Transform handSocket;

    [Header("UI - Tool Icon")]
    [Tooltip("Optional UI Image to indicate this tool; will be tinted when selected.")]
    public Image toolIcon;
    [Tooltip("Color when the tool is not selected (default state).")]
    public Color toolDefaultColor = Color.white;
    [Tooltip("Color when this tool is selected/equipped.")]
    public Color toolSelectedColor = new Color(0.2f, 0.85f, 0.2f, 1f);

    [Header("Hotkey Equip")]
    [Tooltip("Enable 1-key toggle equip/unequip; disables ground pickup/drop mechanics.")]
    public bool enableHotkeyEquip = true;
    [Tooltip("Key to toggle bucket in hand")] public KeyCode toggleKey = KeyCode.Alpha1;

    [Header("Interaction Modes")]
    [Tooltip("Enable mouse click to pick up/drop by clicking the bucket.")]
    public bool enableMouseInteraction = true;
    [Tooltip("Enable keyboard interaction for pickup (E) as an optional fallback.")]
    public bool enableKeyboardInteraction = false;

    [Header("Keys & Ranges")]
    [Tooltip("Key to pick up the bucket when close to it (used only if keyboard is enabled).")]
    public KeyCode pickupKey = KeyCode.E;
    [Tooltip("Key to fill the bucket at the well while carrying it.")]
    public KeyCode fillKey = KeyCode.Y;

    [Tooltip("Max distance from player to this bucket to allow pickup (keyboard prompt).")]
    public float pickupRange = 2.0f;
    [Tooltip("Max distance from player to a well to allow filling.")]
    public float fillRange = 2.0f;
    [Tooltip("Max distance from player to allow mouse click pickup (0 = use pickupRange).")]
    public float mousePickupRange = 3.0f;

    [Header("Well Detection")]
    [Tooltip("Preferred physics layer for well objects. If set (non-zero), used for range checks.")]
    public LayerMask wellLayer;
    [Tooltip("Fallback tag used to identify well objects when layer is not set.")]
    public string wellTag = "Well";

    [Header("State")]
    [SerializeField]
    private bool isFilled = false;
    [SerializeField]
    private bool isCarried = false;
    [Tooltip("How many crops can be watered per full bucket.")]
    public int maxWaterCropsPerFill = 2;
    [SerializeField]
    private int waterCharges = 0; // remaining crops this fill can water

    [Header("Carry Pose (Local)")]
    public Vector3 carriedLocalPosition = new Vector3(0f, 0f, 0f);
    public Vector3 carriedLocalEuler = new Vector3(0f, 0f, 0f);
    public Vector3 carriedLocalScale = Vector3.one;

    [Header("Drop Placement")]
    [Tooltip("Horizontal distance in front of the player when dropping.")]
    public float dropForwardDistance = 0.6f;
    [Tooltip("Up offset to start ground raycast from.")]
    public float dropRayUpOffset = 1.0f;
    [Tooltip("Max raycast distance downward to find ground.")]
    public float dropRayDownDistance = 3.0f;
    [Tooltip("Layer mask for ground when dropping. If zero, uses all layers.")]
    public LayerMask groundLayer;
    [Tooltip("Small clearance added above the ground when placing.")]
    public float dropClearance = 0.02f;
    [Tooltip("Keep bucket upright on drop (align only Y rotation).")]
    public bool alignUprightOnDrop = true;

    [Header("Events")]
    public UnityEvent onPickedUp;
    public UnityEvent onFilled;
    public UnityEvent onEmptied;
    public UnityEvent onDropped;

    [Header("Cursor Feedback")]
    [Tooltip("Show a custom grab cursor when hovering the bucket.")]
    public bool enableCursorFeedback = true;
    [Tooltip("Texture used as grab-hand cursor when hovering the bucket.")]
    public Texture2D grabCursor;
    [Tooltip("Hotspot/pivot of the grab cursor texture (in source texture pixels).")]
    public Vector2 grabCursorHotspot = new Vector2(8, 8);
    [Tooltip("CursorMode (Auto recommended).")]
    public CursorMode grabCursorMode = CursorMode.Auto;

    [Tooltip("Resize the grab cursor to a consistent on-screen size.")]
    public bool resizeGrabCursor = true;
    [Tooltip("Desired cursor size in pixels (max dimension). Set 0 to use source size.")]
    public int grabCursorSize = 32;
    [Tooltip("Try to adjust size using screen DPI (uses 96dpi as baseline). If Screen.dpi is 0, this is ignored.")]
    public bool autoAdjustForDPI = true;

    [Header("Hover Scale")]
    [Tooltip("Mouse üstüne geldiğinde kovayı biraz büyüt")] public bool enableHoverScale = true;
    [Tooltip("Hedef ölçek çarpanı (1 = orijinal)")] public float hoverScale = 1.08f;
    [Tooltip("Hover'a geçiş süresi (sn)")] public float hoverScaleInDuration = 0.12f;
    [Tooltip("Hover'dan çıkış süresi (sn)")] public float hoverScaleOutDuration = 0.12f;
    private Coroutine _hoverScaleCo;
    private bool _hoverScaleActive;
    private Vector3 _initialScale;

    [Header("Hover Raycast")]
    [Tooltip("Layer mask used for hover/click raycasts. Exclude UI/3D Text layers to prevent cursor flicker.")]
    public LayerMask hoverMask = ~0;
    [Tooltip("Exclude UI layer from raycast checks. Set this to your UI layer number.")]
    public int uiLayerToExclude = 5; // Default UI layer

    [Header("Drop Stabilization")]
    [Tooltip("Run a brief stabilization after drop to ensure the bucket rests on ground and doesn't sink.")]
    public bool stabilizeAfterDrop = true;
    [Tooltip("Duration of stabilization after drop (seconds).")]
    public float dropStabilizeDuration = 0.5f;
    [Tooltip("Freeze X/Z rotation during stabilization to keep the bucket upright.")]
    public bool freezeTiltWhileStabilizing = true;

    [Header("Safety Clamp")]
    [Tooltip("If enabled, clamps the bucket's world Y after drop so it never goes below minYOnDrop.")]
    public bool clampMinYOnDrop = true;
    [Tooltip("Minimum world Y for the bucket after drop.")]
    public float minYOnDrop = 0.5f;

    [Header("Forced Drop Y")]
    [Tooltip("If enabled, forces the bucket's world Y to a fixed value at drop and during stabilization.")]
    public bool forceFixedDropY = true;
    [Tooltip("Forced world Y for the bucket at/after drop.")]
    public float fixedDropY = 0.05f;

    private Rigidbody _rb;
    private Collider[] _colliders;
    [Header("Animation")]
    [Tooltip("Play a small TakeItem animation when picking up the bucket.")]
    public bool playPickupAnimation = false; // disabled by default per request

    private Transform _originalParent;
    private Vector3 _originalLocalScale;

    private bool _cursorOwned;
    private Texture2D _scaledGrabCursor;
    private Vector2 _scaledHotspot;
    private bool _cursorDirty;
    // Sticky/hysteresis for mouse pickup range to avoid hover flicker
    private bool _mouseRangeSticky;
    private float _lastMouseRangeCheckTime;
    private const float MOUSE_RANGE_HYSTERESIS = 0.2f;

    // Water visual base scale cache for level scaling
    private Vector3 _waterBaseScale = Vector3.one;
    private bool _hasWaterBaseScale = false;

    public bool IsFilled => isFilled;
    public bool IsCarried => isCarried;
    
    [Header("Animation")]
    [Tooltip("Optional Animator on the bucket for playing watering animation.")]
    public Animator animator;
    private bool _checkedWateringParamBucket = false;
    private bool _hasWateringParamBucket = false;
    private string _wateringBucketParamName = null; // e.g., "WateringBucket" | fallback names
    
    [Header("Save")]
    [Tooltip("Persistent ID for save/load. Set a unique value per bucket in the scene.")]
    public string saveId;

    private void Awake()
    {
        Instance = this;
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _originalLocalScale = transform.localScale;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
        if (waterVisual != null)
        {
            _waterBaseScale = waterVisual.transform.localScale;
            _hasWaterBaseScale = true;
        }
        CacheRenderers();
    }

    private void Start()
    {
        // Ensure stable saveId if inspector left empty
        if (string.IsNullOrEmpty(saveId)) saveId = $"bucket_{gameObject.name}";
        if (player == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) player = playerGO.transform;
        }
    ApplyVisual();
    if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    if (fillPromptUI != null) fillPromptUI.SetActive(false);
        _cursorDirty = true;
    // Cache initial scale as stable base for hover scaling (prevents compounding)
    _initialScale = _originalLocalScale;
        
        // Cache camera reference to avoid FindObjectOfType calls
        _cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        
        // Automatically exclude UI layer from hover mask if specified
        if (uiLayerToExclude >= 0 && uiLayerToExclude < 32)
        {
            hoverMask &= ~(1 << uiLayerToExclude);
        }

        // Start stowed by default in hotkey mode (not in hand but following player)
        if (enableHotkeyEquip)
        {
            StowNearPlayer(invisible: true);
        }
    // Initialize tool icon color
    UpdateToolUI();
    }

    private void ApplyVisual()
    {
        if (waterVisual == null) return;

        // Ensure base scale captured
        if (!_hasWaterBaseScale)
        {
            _waterBaseScale = waterVisual.transform.localScale;
            _hasWaterBaseScale = true;
        }

    int capacity = Mathf.Max(1, maxWaterCropsPerFill);
    int charges = Mathf.Clamp(waterCharges, 0, capacity);
    // Visual is driven purely by remaining charges: show when charges > 0, hide when 0
    float fraction = charges / (float)capacity;
    bool shouldShow = charges > 0;
        if (waterVisual.activeSelf != shouldShow)
            waterVisual.SetActive(shouldShow);

        // Scale the Y of the water visual proportionally to remaining charges
        // Keep X/Z unchanged from base; clamp to a tiny positive to avoid zero-scale issues
        float scaledY = Mathf.Max(0.001f, _waterBaseScale.y * Mathf.Clamp01(fraction));
        var t = waterVisual.transform;
        t.localScale = new Vector3(_waterBaseScale.x, scaledY, _waterBaseScale.z);
    }

    private void OnValidate()
    {
        _cursorDirty = true;
    }

    private void Update()
    {
        // Hotkey equip/unequip
    if (enableHotkeyEquip && InputHelper.GetKeyDown(toggleKey))
        {
            ToggleEquip();
        }

        // If using hotkey mode, skip ground pickup/hover cursor mechanics
        if (!enableHotkeyEquip)
        {
            // Mouse click toggle
            if (enableMouseInteraction && Input.GetMouseButtonDown(0))
            {
                // If any context menu (e.g., Well) is open, swallow left click to allow UI selection
                var wmType = System.Type.GetType("WellManager");
                if (wmType != null)
                {
                    var prop = wmType.GetProperty("AnyContextMenuOpen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null)
                    {
                        object val = prop.GetValue(null, null);
                        if (val is bool open && open)
                        {
                            return; // Do not process pickup/drop while context menu is open
                        }
                    }
                }
                // If currently carried, drop on any left click (don't require raycast on trigger collider)
                if (isCarried)
                {
                    Drop();
                }
                else if (IsThisBucketClicked())
                {
                    // Enforce player-to-bucket distance for mouse pickup
                    if (IsWithinMousePickupRange())
                        PickUp();
                    else
                        return;
                }
            }
        }

        // Cursor feedback while hovering (skip in hotkey mode)
        if (!enableHotkeyEquip && enableMouseInteraction && (enableCursorFeedback || enableHoverScale))
        {
            // Suppress hover cursor while carried or when any context menu is open to avoid flicker
            bool anyMenuOpen = false;
            var wmType2 = System.Type.GetType("WellManager");
            if (wmType2 != null)
            {
                var prop = wmType2.GetProperty("AnyContextMenuOpen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    object val2 = prop.GetValue(null, null);
                    anyMenuOpen = (val2 is bool b && b);
                }
            }
            if (!isCarried && !anyMenuOpen)
            {
                UpdateCursorFeedbackThrottled();
            }
            else
            {
                // Ensure cursor resets if we own it
                UpdateCursorState(false);
                // And stop hover scale
                UpdateHoverScaleVisual(false);
            }
        }

        // Prompts disabled in hotkey mode
        if (enableHotkeyEquip)
        {
            if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
        }
        else
        {
            bool nearBucket = IsPlayerNearBucket();
            if (pickupPromptUI != null)
            {
                bool showPickup = !isCarried && nearBucket && enableKeyboardInteraction;
                pickupPromptUI.SetActive(showPickup);
            }
        }

        if (isCarried)
        {
            // Pressing fill always tries; acceptance is validated by WellManager
            if (InputHelper.GetKeyDown(fillKey) && !isFilled)
            {
                TryFillViaWellManager();
            }
        }
        else
        {
            // Fill prompt is managed by WellManager now
        }

        if (!enableHotkeyEquip)
        {
            bool nearBucket = IsPlayerNearBucket();
            if (enableKeyboardInteraction && !isCarried && nearBucket && InputHelper.GetKeyDown(pickupKey))
            {
                PickUp();
            }
        }
    }

    private bool IsPlayerNearBucket()
    {
        if (player == null) return false;
        return Vector3.Distance(player.position, transform.position) <= pickupRange;
    }

    private bool IsNearWell()
    {
        Vector3 center = player != null ? player.position : transform.position;
        // Prefer WellManager registry if present (via reflection to avoid hard dependency)
        if (AnyWellInRangeOptional(center, fillRange)) return true;

        // Backwards-compatible fallback: layer/tag
        int mask = wellLayer.value != 0 ? wellLayer.value : ~0;
        if (wellLayer.value != 0)
        {
            var hits = Physics.OverlapSphere(center, fillRange, wellLayer, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0) return true;
        }
        var any = Physics.OverlapSphere(center, fillRange, mask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < any.Length; i++)
        {
            var c = any[i];
            if (c != null && c.CompareTag(wellTag)) return true;
        }
        return false;
    }

    private void TryFillViaWellManager()
    {
        Vector3 center = player != null ? player.position : transform.position;
        // Prefer direct WellManager API when available
        #if true
        if (WellManager.TryGetNearestInRange(center, fillRange, out var well))
        {
            if (well != null && well.FillBucket(this))
            {
                Fill();
                return;
            }
        }
        #else
        // Legacy reflection path
        if (TryFillNearestWellOptional(center, fillRange))
        {
            Fill();
            return;
        }
        #endif
        // No fill if no valid well in range
    }

    private bool AnyWellInRangeOptional(Vector3 center, float range)
    {
        var t = System.Type.GetType("WellManager");
        if (t == null) return false;
        var mi = t.GetMethod("AnyWellInRange", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (mi == null) return false;
        object res = mi.Invoke(null, new object[] { center, range });
        return res is bool b && b;
    }

    private bool TryFillNearestWellOptional(Vector3 center, float range)
    {
        var t = System.Type.GetType("WellManager");
        if (t == null) return false;
        var miTry = t.GetMethod("TryGetNearestInRange", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var miFill = t.GetMethod("FillBucket", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (miTry == null || miFill == null) return false;
        object[] args = new object[] { center, range, null };
        object ok = miTry.Invoke(null, args);
        if (!(ok is bool) || !(bool)ok) return false;
        object wellInstance = args[2];
        if (wellInstance == null) return false;
        object filled = miFill.Invoke(wellInstance, new object[] { this });
        return filled is bool b && b;
    }

    private void Fill()
    {
        // Set charges first, then flag filled. Visual depends on charges>0.
        waterCharges = Mathf.Max(1, maxWaterCropsPerFill);
        isFilled = true;
        ApplyVisual();
        // Refresh colliders in case waterVisual enabled additional colliders
        RefreshCollidersAndPhysicsState();
        onFilled?.Invoke();
    }

    // Empties the bucket after watering; returns true if water was consumed
    public bool TryConsumeAllWater()
    {
    if (!isFilled && waterCharges <= 0) return false;
    // Play watering bucket animation when water is consumed
    TriggerWateringBucketAnimation();
        isFilled = false;
        waterCharges = 0;
        ApplyVisual();
        RefreshCollidersAndPhysicsState();
        onEmptied?.Invoke();
        return true;
    }

    // Try to consume up to 'count' water charges; returns how many were consumed
    public int TryConsumeWaterCharges(int count)
    {
        if (count <= 0) return 0;
        if (waterCharges <= 0)
        {
            // Already empty; ensure visual state consistent
            if (isFilled)
            {
                isFilled = false;
        // Single refresh at end
                onEmptied?.Invoke();
            }
            return 0;
        }
        int used = Mathf.Min(count, waterCharges);
        waterCharges -= used;
        if (used > 0)
        {
            // Trigger watering animation when consuming charges
            TriggerWateringBucketAnimation();
        }
    // Defer visual update until after potential empty transition
        if (waterCharges <= 0)
        {
            waterCharges = 0;
            if (isFilled)
            {
                isFilled = false;
        // will apply visuals below
                onEmptied?.Invoke();
            }
        }
    // Single visual/physics refresh to minimize UI flicker
    ApplyVisual();
    RefreshCollidersAndPhysicsState();
        return used;
    }

    public int RemainingWaterCharges => Mathf.Max(0, waterCharges);

    private void UpdateCursorFeedbackThrottled()
    {
        // Only check hover state every HOVER_CHECK_INTERVAL seconds to reduce flicker
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastHoverCheckTime < HOVER_CHECK_INTERVAL)
        {
            // Use cached hover state
            UpdateCursorState(_lastHoverState);
            UpdateHoverScaleVisual(_lastHoverState);
            return;
        }
        
        _lastHoverCheckTime = currentTime;
        bool hovering = IsHoveringThisBucketOptimized();
        // Suppress hover feedback if player is too far to interact
        if (hovering && !IsWithinMousePickupRange())
        {
            hovering = false;
        }
        _lastHoverState = hovering;
        UpdateCursorState(hovering);
        UpdateHoverScaleVisual(hovering);
    }

    private void UpdateHoverScaleVisual(bool desiredHover)
    {
        if (!enableHoverScale)
        {
            // If disabled, ensure reset when necessary
            if (_hoverScaleActive)
            {
                EndHoverScale();
            }
            return;
        }
        if (desiredHover)
        {
            StartHoverScale();
        }
        else
        {
            EndHoverScale();
        }
    }

    private void StartHoverScale()
    {
        Vector3 target = _initialScale * Mathf.Max(0.01f, hoverScale);
        // If already approximately at target, do nothing
        if (_hoverScaleCo == null && NearlyEqual(transform.localScale, target))
        {
            _hoverScaleActive = true;
            return;
        }
        if (_hoverScaleCo != null) StopCoroutine(_hoverScaleCo);
        _hoverScaleCo = StartCoroutine(ScaleTo(target, Mathf.Max(0.01f, hoverScaleInDuration)));
        _hoverScaleActive = true;
    }

    private void EndHoverScale()
    {
        Vector3 target = _initialScale;
        if (_hoverScaleCo == null && NearlyEqual(transform.localScale, target))
        {
            _hoverScaleActive = false;
            return;
        }
        if (_hoverScaleCo != null) StopCoroutine(_hoverScaleCo);
        _hoverScaleCo = StartCoroutine(ScaleTo(target, Mathf.Max(0.01f, hoverScaleOutDuration)));
        _hoverScaleActive = false;
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        if (duration <= 0.0001f)
        {
            transform.localScale = target; _hoverScaleCo = null; yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.Lerp(start, target, u);
            yield return null;
        }
        transform.localScale = target;
        _hoverScaleCo = null;
    }

    private bool IsWithinMousePickupRange()
    {
        if (player == null) return false;
        float allowed = Mathf.Max(0.01f, mousePickupRange > 0f ? mousePickupRange : pickupRange);

        // Throttle checks to reduce oscillation
        float now = Time.unscaledTime;
        if (now - _lastMouseRangeCheckTime >= HOVER_CHECK_INTERVAL)
        {
            _lastMouseRangeCheckTime = now;
            // Planar (XZ) distance to avoid Y jitter
            Vector3 pp = player.position;
            Vector3 bp = transform.position;
            float dx = pp.x - bp.x;
            float dz = pp.z - bp.z;
            float sqr = dx * dx + dz * dz;
            float enter = allowed + MOUSE_RANGE_HYSTERESIS;
            float exit = allowed - MOUSE_RANGE_HYSTERESIS;
            float enterSqr = enter * enter;
            float exitSqr = Mathf.Max(0f, exit * exit);
            if (!_mouseRangeSticky && sqr <= enterSqr)
                _mouseRangeSticky = true;
            else if (_mouseRangeSticky && sqr > exitSqr)
                _mouseRangeSticky = false;
        }
        return _mouseRangeSticky;
    }

    private void UpdateCursorState(bool hovering)
    {
        if (hovering && grabCursor != null)
        {
            // Only allow cursor change if we're not already the owner or enough time has passed
            if (s_cursorOwner != this)
            {
                float timeSinceLastChange = Time.unscaledTime - s_lastCursorChangeTime;
                if (timeSinceLastChange < CURSOR_DEBOUNCE)
                    return;
                    
                // Take ownership
                if (s_cursorOwner != null && s_cursorOwner._cursorOwned)
                {
                    s_cursorOwner.ResetCursorIfOwned();
                }
                s_cursorOwner = this;
                s_lastCursorChangeTime = Time.unscaledTime;
                _cursorOwned = false;
            }

            EnsureScaledCursor();
            if (!_cursorOwned)
            {
                Cursor.SetCursor(_scaledGrabCursor != null ? _scaledGrabCursor : grabCursor,
                                 _scaledGrabCursor != null ? _scaledHotspot : grabCursorHotspot,
                                 grabCursorMode);
                _cursorOwned = true;
            }
        }
        else
        {
            // Only reset if we own the cursor and enough time has passed
            if (s_cursorOwner == this)
            {
                float timeSinceLastChange = Time.unscaledTime - s_lastCursorChangeTime;
                if (timeSinceLastChange >= CURSOR_DEBOUNCE)
                {
                    ResetCursorIfOwned();
                    s_cursorOwner = null;
                    s_lastCursorChangeTime = Time.unscaledTime;
                }
            }
        }
    }

    private bool IsHoveringThisBucketOptimized()
    {
        if (_cachedCamera == null) return false;
        
        // Use a more targeted approach - check if mouse is over any collider first
        Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
        
        // First, do a simple raycast with our specific layer mask
        if (!Physics.Raycast(ray, out RaycastHit firstHit, 100f, hoverMask, QueryTriggerInteraction.Ignore))
            return false;
            
        // Check if the first hit is us
        var firstBM = firstHit.collider.GetComponentInParent<BucketManager>();
        if (firstBM == this) return true;
        
        // If not, check a few more hits but limit the search
        var hits = Physics.RaycastAll(ray, 100f, hoverMask, QueryTriggerInteraction.Ignore);
        if (hits.Length <= 1) return false; // We already checked the first one
        
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        // Only check the first 3 hits to avoid performance issues
        int maxCheck = Mathf.Min(3, hits.Length);
        for (int i = 0; i < maxCheck; i++)
        {
            var bm = hits[i].collider.GetComponentInParent<BucketManager>();
            if (bm == this) return true;
        }
        
        return false;
    }

    private void EnsureScaledCursor()
    {
        if (!resizeGrabCursor || grabCursor == null)
        {
            // Use original
            if (_scaledGrabCursor != null)
            {
                Destroy(_scaledGrabCursor);
                _scaledGrabCursor = null;
            }
            return;
        }

        if (!_cursorDirty && _scaledGrabCursor != null) return;

        // Determine target size
        int target = Mathf.Clamp(grabCursorSize, 8, 256);
        float dpi = Screen.dpi;
        if (autoAdjustForDPI && dpi > 1f)
        {
            // Scale size for DPI relative to 96
            float scale = dpi / 96f;
            target = Mathf.Clamp(Mathf.RoundToInt(target * scale), 8, 256);
        }

        // Compute aspect and hotspot scaling
        int srcW = grabCursor.width;
        int srcH = grabCursor.height;
        int maxSrc = Mathf.Max(srcW, srcH);
        float scaleFactor = maxSrc > 0 ? (float)target / maxSrc : 1f;
        int dstW = Mathf.Max(8, Mathf.RoundToInt(srcW * scaleFactor));
        int dstH = Mathf.Max(8, Mathf.RoundToInt(srcH * scaleFactor));

        // Build scaled texture
        var newTex = ResizeTexture(grabCursor, dstW, dstH);
        if (_scaledGrabCursor != null) Destroy(_scaledGrabCursor);
        _scaledGrabCursor = newTex;
        _scaledHotspot = grabCursorHotspot * scaleFactor;
        _cursorDirty = false;
    }

    private Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        // GPU scale to avoid requiring Read/Write on source
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        tex.name = source.name + "_scaled";
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    private bool IsThisBucketClicked()
    {
        if (_cachedCamera == null) return false;
        Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, hoverMask, QueryTriggerInteraction.Ignore))
        {
            var bm = hit.collider.GetComponentInParent<BucketManager>();
            return bm == this;
        }
        return false;
    }

    private bool IsHoveringThisBucket()
    {
        // This method is kept for compatibility but shouldn't be used
        // Use IsHoveringThisBucketOptimized instead
        return IsHoveringThisBucketOptimized();
    }

    public void PickUp()
    {
        if (isCarried) return;
        if (player == null)
        {
            Debug.LogWarning("BucketManager: No player reference found to carry the bucket.");
            return;
        }
        // Attach to hand or player
        _originalParent = transform.parent;
        Transform parentTarget = handSocket != null ? handSocket : player;
        transform.SetParent(parentTarget);
        transform.localPosition = carriedLocalPosition;
        transform.localEulerAngles = carriedLocalEuler;
        transform.localScale = carriedLocalScale;

        SetCarriedPhysics(true);
        SetVisible(true);
        isCarried = true;
        CurrentCarried = this;
        onPickedUp?.Invoke();
        // Trigger carry animation on
        var anim = FindObjectOfType<PlayerAnimationController>();
        if (anim != null) {
            if (playPickupAnimation)
            {
                anim.TriggerTakeItem();
            }
            // Keep carry state regardless of pickup anim
            anim.SetCarryBucket(true);
        }
        // Unequip harrow when bucket is picked up (mutual exclusive in hand)
        var harrow = HarrowManagerGlobal();
        if (harrow != null) harrow.ForceUnequip();
    // Update UI to reflect selection
    UpdateToolUI();
    }

    public void Drop()
    {
        if (!isCarried) return;
        // Stow near player invisibly instead of dropping to ground
        isCarried = false;
        if (CurrentCarried == this) CurrentCarried = null;
        SetCarriedPhysics(false);
        SetVisible(false);
        StowNearPlayer(invisible: true);
        onDropped?.Invoke();
        var anim = FindObjectOfType<PlayerAnimationController>();
        if (anim != null) anim.SetCarryBucket(false);
    // Update UI to reflect deselection
    UpdateToolUI();
    }

    // --- Hotkey helpers ---
    public void ToggleEquip()
    {
        if (isCarried) Drop(); else PickUp();
    }

    public void ForceUnequip()
    {
    if (isCarried) Drop(); else { SetVisible(false); StowNearPlayer(invisible: true); UpdateToolUI(); }
    }

    private void StowNearPlayer(bool invisible)
    {
        if (player == null) return;
        transform.SetParent(player);
        transform.localPosition = new Vector3(0.2f, 0.0f, -0.3f); // small offset behind/right
        transform.localRotation = Quaternion.identity;
        transform.localScale = _originalLocalScale;
        SetVisible(!invisible);
        UpdateToolUI();
    }

    private void UpdateToolUI()
    {
        if (toolIcon == null) return;
        toolIcon.color = isCarried ? toolSelectedColor : toolDefaultColor;
    }

    private List<Renderer> _renderers;
    private void CacheRenderers()
    {
        _renderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
    }
    private void SetVisible(bool visible)
    {
        if (_renderers == null) CacheRenderers();
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i]; if (r != null) r.enabled = visible;
        }
    }

    private HarrowManager HarrowManagerGlobal()
    {
        return HarrowManager.CurrentCarried != null ? HarrowManager.CurrentCarried : FindObjectOfType<HarrowManager>();
    }

    private bool FindGroundPoint(out Vector3 point, out Vector3 normal)
    {
        Vector3 basePos = player != null ? player.position : transform.position;
        Vector3 forward = player != null ? player.forward : transform.forward;
        Vector3 origin = basePos + forward * dropForwardDistance + Vector3.up * dropRayUpOffset;

        int mask = groundLayer.value != 0 ? groundLayer.value : ~0;

        // Raycast down first
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dropRayDownDistance + dropRayUpOffset, mask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        // Spherecast with approximated radius from bounds to catch small gaps/edges
        float radius = Mathf.Max(0.05f, GetPlanarRadius());
        if (Physics.SphereCast(origin, radius, Vector3.down, out hit, dropRayDownDistance + dropRayUpOffset, mask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = Vector3.zero;
        normal = Vector3.up;
        return false;
    }

    private float GetPlanarRadius()
    {
        // Half of the max of X/Z extents from combined bounds
        Bounds b;
        if (!TryGetCombinedBounds(out b)) return 0.15f;
        return Mathf.Max(b.extents.x, b.extents.z);
    }

    private float GetHalfHeight()
    {
        Bounds b;
        if (!TryGetCombinedBounds(out b)) return 0.15f;
        return Mathf.Max(0.01f, b.extents.y);
    }

    private bool TryGetCombinedBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool has = false;
        if (_colliders == null || _colliders.Length == 0)
        {
            return false;
        }
        for (int i = 0; i < _colliders.Length; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (!has)
            {
                bounds = c.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }
        return has;
    }

    private void NudgeUpIfOverlapping()
    {
        Bounds b;
        if (!TryGetCombinedBounds(out b)) return;

        int mask = ~0;
        const int maxSteps = 6;
        const float step = 0.01f;
        for (int i = 0; i < maxSteps; i++)
        {
            Collider[] hits = Physics.OverlapBox(b.center, b.extents, transform.rotation, mask, QueryTriggerInteraction.Ignore);
            bool overlap = false;
            for (int h = 0; h < hits.Length; h++)
            {
                var other = hits[h];
                if (other == null) continue;
                if (other.transform == transform || other.transform.IsChildOf(transform)) continue;
                if (other.isTrigger) continue;
                overlap = true;
                break;
            }
            if (!overlap) break;
            transform.position += Vector3.up * step;
            // Update bounds position for next check
            b.center += Vector3.up * step;
        }
    }

    private IEnumerator StabilizeAfterDropCoroutine()
    {
        if (_rb == null) yield break;

        // Configure rigidbody for stability
        var prevMode = _rb.collisionDetectionMode;
        var prevInterp = _rb.interpolation;
        var prevConstraints = _rb.constraints;

        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (freezeTiltWhileStabilizing)
        {
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        float t = 0f;
        while (t < dropStabilizeDuration)
        {
            t += Time.fixedDeltaTime;

            if (!forceFixedDropY)
            {
                // Keep on ground if slightly sinking due to precision issues
                Vector3 point, normal;
                if (FindGroundPoint(out point, out normal))
                {
                    float targetY = point.y + GetHalfHeight() + dropClearance;
                    if (transform.position.y < targetY - 0.005f)
                    {
                        Vector3 pos = transform.position;
                        pos.y = targetY;
                        transform.position = pos;
                        // Cancel downward velocity
                        Vector3 vel = _rb.velocity;
                        if (vel.y < 0f) vel.y = 0f;
                        _rb.velocity = vel;
                    }
                }
            }

            // Safety clamp or force fixed Y
            if (forceFixedDropY)
            {
                if (transform.position.y != fixedDropY)
                {
                    Vector3 pos = transform.position;
                    pos.y = fixedDropY;
                    transform.position = pos;
                }
                Vector3 vel = _rb.velocity;
                if (vel.y != 0f) vel.y = 0f;
                _rb.velocity = vel;
            }
            else if (clampMinYOnDrop && transform.position.y < minYOnDrop)
            {
                Vector3 pos = transform.position;
                pos.y = minYOnDrop;
                transform.position = pos;
                Vector3 vel = _rb.velocity;
                if (vel.y < 0f) vel.y = 0f;
                _rb.velocity = vel;
            }

            yield return new WaitForFixedUpdate();
        }

        // Restore previous settings
        _rb.collisionDetectionMode = prevMode;
        _rb.interpolation = prevInterp;
        _rb.constraints = prevConstraints;
    }

    private void ResolvePenetrations(int maxIterations)
    {
        if (_colliders == null || _colliders.Length == 0) return;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            bool moved = false;
            for (int i = 0; i < _colliders.Length; i++)
            {
                var c = _colliders[i];
                if (c == null || !c.enabled) continue;
                // Overlap candidates around this collider bounds
                Collider[] hits = Physics.OverlapBox(c.bounds.center, c.bounds.extents + Vector3.one * 0.005f, c.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                {
                    var other = hits[h];
                    if (other == null) continue;
                    if (other.transform == transform || other.transform.IsChildOf(transform)) continue;
                    Vector3 dir;
                    float dist;
                    if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation,
                                                   other, other.transform.position, other.transform.rotation,
                                                   out dir, out dist))
                    {
                        // Move out slightly more than the penetration distance
                        Vector3 delta = dir * (dist + 0.002f);
                        transform.position += delta;
                        moved = true;
                    }
                }
            }
            if (!moved) break;
        }
    }

    private void SetCarriedPhysics(bool carried)
    {
        if (_rb != null)
        {
            _rb.isKinematic = carried;
            _rb.useGravity = !carried;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.collisionDetectionMode = carried ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = carried ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] == null) continue;
                // Make them triggers while carried to avoid physical collisions
                _colliders[i].isTrigger = carried;
                _colliders[i].enabled = true;
            }
        }
        // Safety: disable any nested rigidbodies besides the root to prevent physics conflicts
        DisableNestedRigidbodiesExceptRoot();
    }

    private void RefreshCollidersList()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    private void RefreshCollidersAndPhysicsState()
    {
        RefreshCollidersList();
        // Reapply trigger state on all colliders according to carry state
        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                var c = _colliders[i];
                if (c == null) continue;
                c.isTrigger = isCarried;
                c.enabled = true;
            }
        }
        DisableNestedRigidbodiesExceptRoot();
    }

    private void DisableNestedRigidbodiesExceptRoot()
    {
        var rbs = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            var rb = rbs[i];
            if (rb == null) continue;
            if (rb == _rb) continue; // keep root
            // Ensure child rigidbodies don't interfere
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    private void ResetCursorIfOwned()
    {
        if (_cursorOwned)
        {
            var cm = CursorManager.Instance;
            if (cm != null)
            {
                cm.UseDefaultNow();
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            _cursorOwned = false;
        }
    }

    private void OnDisable()
    {
        if (s_cursorOwner == this)
        {
            ResetCursorIfOwned();
            s_cursorOwner = null;
            s_lastCursorChangeTime = Time.unscaledTime;
        }
    if (CurrentCarried == this) CurrentCarried = null;
    if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    if (fillPromptUI != null) fillPromptUI.SetActive(false);
        // Reset hover scale to base
        if (_hoverScaleCo != null) StopCoroutine(_hoverScaleCo);
        if (_hoverScaleActive || !NearlyEqual(transform.localScale, _initialScale))
        {
            transform.localScale = _initialScale;
            _hoverScaleActive = false;
        }
    }

    private void OnDestroy()
    {
        if (s_cursorOwner == this)
        {
            ResetCursorIfOwned();
            s_cursorOwner = null;
            s_lastCursorChangeTime = Time.unscaledTime;
        }
    if (CurrentCarried == this) CurrentCarried = null;
    if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    if (fillPromptUI != null) fillPromptUI.SetActive(false);
        if (_scaledGrabCursor != null)
        {
            Destroy(_scaledGrabCursor);
            _scaledGrabCursor = null;
        }
        // Ensure scale restored
        if (_hoverScaleCo != null) StopCoroutine(_hoverScaleCo);
        if (_hoverScaleActive || !NearlyEqual(transform.localScale, _initialScale))
        {
            transform.localScale = _initialScale;
            _hoverScaleActive = false;
        }
    }

    // --- Animation helpers ---
    public void TriggerWateringBucketAnimation()
    {
        if (animator == null) return;
        if (!_checkedWateringParamBucket)
        {
            string[] candidates = { "WateringBucket", "Watering", "Water" };
            foreach (var name in candidates)
            {
                if (AnimatorHasParameter(animator, name, AnimatorControllerParameterType.Trigger))
                {
                    _wateringBucketParamName = name;
                    _hasWateringParamBucket = true;
                    break;
                }
            }
            _checkedWateringParamBucket = true;
            if (!_hasWateringParamBucket)
            {
                Debug.LogWarning("Bucket Animator watering trigger not found. Add a Trigger named 'WateringBucket' (or 'Watering'/'Water') to play watering anim.", this);
            }
        }
        if (_hasWateringParamBucket && !string.IsNullOrEmpty(_wateringBucketParamName))
        {
            animator.SetTrigger(_wateringBucketParamName);
        }
    }

    private static bool AnimatorHasParameter(Animator anim, string name, AnimatorControllerParameterType type)
    {
        if (anim == null) return false;
        var pars = anim.parameters;
        for (int i = 0; i < pars.Length; i++)
        {
            var p = pars[i];
            if (p.type == type && p.name == name) return true;
        }
        return false;
    }

    private static bool NearlyEqual(in Vector3 a, in Vector3 b, float eps = 0.0005f)
    {
        return (a - b).sqrMagnitude <= eps * eps;
    }

    // ===== ISaveable =====
    public Dictionary<string, object> GetSaveData()
    {
        var data = new Dictionary<string, object>();
        data["saveId"] = string.IsNullOrEmpty(saveId) ? gameObject.name : saveId;
        data["pos"] = transform.position;
        data["rot"] = transform.eulerAngles;
        data["scale"] = transform.localScale;
        data["filled"] = isFilled;
        data["waterCharges"] = waterCharges;
        data["carried"] = false; // We never restore as carried across scenes
        Debug.Log($"[Bucket Save] id={saveId}, pos={transform.position}, filled={isFilled}, charges={waterCharges}");
        return data;
    }

    public void LoadSaveData(Dictionary<string, object> data)
    {
        if (data == null) return;
        // Helpers to parse values serialized as strings by GameSaveManager
        Vector3 ParseVec(string key, Vector3 fallback)
        {
            if (!data.ContainsKey(key) || data[key] == null) return fallback;
            string s = data[key].ToString();
            var parts = s.Split(',');
            if (parts.Length != 3) return fallback;
            float x = 0, y = 0, z = 0;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z);
            return new Vector3(x, y, z);
        }

    // Position/rotation/scale (keys are un-prefixed by type here)
    Vector3 pos = ParseVec("pos", transform.position);
    Vector3 rot = ParseVec("rot", transform.eulerAngles);
    Vector3 scale = ParseVec("scale", transform.localScale);
        bool filled = false;
    if (data.TryGetValue("filled", out var f))
        {
            bool.TryParse(f?.ToString(), out filled);
        }
        int charges = 0;
        if (data.TryGetValue("waterCharges", out var wcObj))
        {
            int.TryParse(wcObj?.ToString(), out charges);
        }

        // Apply restored state
        // Disable carry and re-enable physics for world placement
        isCarried = false;
        if (CurrentCarried == this) CurrentCarried = null;
        transform.SetParent(null);
        transform.position = pos;
        transform.eulerAngles = rot;
        transform.localScale = scale;
        SetCarriedPhysics(false);
        isFilled = filled;
        // If charges not present in save, infer from filled state
        waterCharges = (charges > 0) ? charges : (isFilled ? Mathf.Max(1, maxWaterCropsPerFill) : 0);
        ApplyVisual();
        RefreshCollidersAndPhysicsState();
        Debug.Log($"[Bucket Load] id={saveId}, pos={pos}, filled={filled}, charges={waterCharges}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 0.2f, 0.35f);
        Vector3 pc = player != null ? player.position : transform.position;
        Gizmos.DrawWireSphere(pc, pickupRange);

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.35f);
        Gizmos.DrawWireSphere(pc, fillRange);
    }
#endif
}
