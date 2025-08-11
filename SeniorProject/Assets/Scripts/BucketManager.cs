using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BucketManager : MonoBehaviour, ISaveable
{
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
    private Transform _originalParent;
    private Vector3 _originalLocalScale;

    private bool _cursorOwned;
    private Texture2D _scaledGrabCursor;
    private Vector2 _scaledHotspot;
    private bool _cursorDirty;

    public bool IsFilled => isFilled;
    public bool IsCarried => isCarried;
    
    [Header("Save")]
    [Tooltip("Persistent ID for save/load. Set a unique value per bucket in the scene.")]
    public string saveId;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _originalLocalScale = transform.localScale;
    }

    private void Start()
    {
        if (player == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) player = playerGO.transform;
        }
        ApplyVisual();
    if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    if (fillPromptUI != null) fillPromptUI.SetActive(false);
    if (waterVisual != null && waterVisual.activeSelf != isFilled) waterVisual.SetActive(isFilled);
        _cursorDirty = true;
        
        // Cache camera reference to avoid FindObjectOfType calls
        _cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        
        // Automatically exclude UI layer from hover mask if specified
        if (uiLayerToExclude >= 0 && uiLayerToExclude < 32)
        {
            hoverMask &= ~(1 << uiLayerToExclude);
        }
    }

    private void ApplyVisual()
    {
        if (waterVisual != null)
        {
            waterVisual.SetActive(isFilled);
        }
    }

    private void OnValidate()
    {
        _cursorDirty = true;
    }

    private void Update()
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
                PickUp();
            }
        }

        // Cursor feedback while hovering (throttled)
        if (enableMouseInteraction && enableCursorFeedback)
        {
            UpdateCursorFeedbackThrottled();
        }

    // Prompts and keyboard (optional)
    bool nearBucket = IsPlayerNearBucket();
        if (pickupPromptUI != null)
        {
            bool showPickup = !isCarried && nearBucket && enableKeyboardInteraction;
            pickupPromptUI.SetActive(showPickup);
        }

    if (isCarried)
        {
            // Pressing fill always tries; acceptance is validated by WellManager
            if (Input.GetKeyDown(fillKey) && !isFilled)
            {
                TryFillViaWellManager();
            }
        }
        else
        {
        // Fill prompt is managed by WellManager now
        }

        if (enableKeyboardInteraction && !isCarried && nearBucket && Input.GetKeyDown(pickupKey))
        {
            PickUp();
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
        isFilled = true;
        ApplyVisual();
    // Refresh colliders in case waterVisual enabled additional colliders
    RefreshCollidersAndPhysicsState();
        onFilled?.Invoke();
    }

    // Empties the bucket after watering; returns true if water was consumed
    public bool TryConsumeAllWater()
    {
        if (!isFilled) return false;
        isFilled = false;
        ApplyVisual();
        RefreshCollidersAndPhysicsState();
        onEmptied?.Invoke();
        return true;
    }

    private void UpdateCursorFeedbackThrottled()
    {
        // Only check hover state every HOVER_CHECK_INTERVAL seconds to reduce flicker
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastHoverCheckTime < HOVER_CHECK_INTERVAL)
        {
            // Use cached hover state
            UpdateCursorState(_lastHoverState);
            return;
        }
        
        _lastHoverCheckTime = currentTime;
        bool hovering = IsHoveringThisBucketOptimized();
        _lastHoverState = hovering;
        UpdateCursorState(hovering);
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
        isCarried = true;
    CurrentCarried = this;
        onPickedUp?.Invoke();
    }

    public void Drop()
    {
        if (!isCarried) return;
        isCarried = false;
    if (CurrentCarried == this) CurrentCarried = null;

    // Find ground point near player
    Vector3 groundPoint;
    Vector3 groundNormal;
    if (!FindGroundPoint(out groundPoint, out groundNormal))
        {
            // Fallback: current position treated as ground
            groundPoint = transform.position;
            groundNormal = Vector3.up;
        }

        // Compute half-height from colliders to keep above ground
    float halfHeight = GetHalfHeight();
        Vector3 finalPos = groundPoint + Vector3.up * (Mathf.Max(0.01f, halfHeight) + dropClearance);
        if (forceFixedDropY)
        {
            finalPos.y = fixedDropY;
        }
        else if (clampMinYOnDrop)
        {
            finalPos.y = Mathf.Max(finalPos.y, minYOnDrop);
        }

        // Detach and place
        transform.SetParent(_originalParent);
        transform.position = finalPos;

        if (alignUprightOnDrop)
        {
            float y = player != null ? player.eulerAngles.y : transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        // Restore original scale (carried may have custom scale)
        transform.localScale = _originalLocalScale;

    // Enable physics with continuous collision to avoid tunneling
    SetCarriedPhysics(false);
    // Ensure collider list includes any newly-enabled colliders (e.g., water visuals)
    RefreshCollidersList();

        // Resolve any initial interpenetrations with the environment
        ResolvePenetrations(5);

    // Small upward nudge if overlapping after placement
    NudgeUpIfOverlapping();

        onDropped?.Invoke();

        if (stabilizeAfterDrop)
        {
            StopAllCoroutines();
            StartCoroutine(StabilizeAfterDropCoroutine());
        }
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
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
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
        data["carried"] = false; // We never restore as carried across scenes
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
        ApplyVisual();
        RefreshCollidersAndPhysicsState();
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
