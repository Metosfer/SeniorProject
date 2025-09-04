using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Click-to-pickup/drop Fishing Rod with hover scale and cursor feedback (similar to Bucket/Harrow)
public class FishingRodManager : MonoBehaviour, ISaveable
{
    // Global reference to whichever rod is currently carried (null if none)
    public static FishingRodManager CurrentCarried { get; private set; }

    [Header("References")]
    [Tooltip("Player transform used for distance checks. If null, uses tagged 'Player'.")]
    public Transform player;
    [Tooltip("Socket transform on the player hand where the rod will attach.")]
    public Transform handSocket;

    [Header("Interaction")]
    [Tooltip("Enable mouse click to pick up/drop by clicking the rod.")]
    public bool enableMouseInteraction = true;
    [Tooltip("Max distance from player to allow mouse click pickup (0 = use pickupRange).")]
    public float mousePickupRange = 3.0f;
    [Tooltip("Generic pickup range fallback.")]
    public float pickupRange = 2.0f;

    [Header("Carry Pose (Local)")]
    [Tooltip("Use custom local offsets when carried (else use socket exact pose)")]
    public bool useCustomCarriedPose = true;
    public Vector3 carriedLocalPosition = new Vector3(0f, 0f, 0f);
    public Vector3 carriedLocalEuler = new Vector3(0f, 0f, 0f);
    public bool preserveWorldScaleOnCarry = true;
    public Transform gripPoint; // optional alignment point on the rod

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
    [Tooltip("Keep rod upright on drop (align only Y rotation).")]
    public bool alignUprightOnDrop = true;

    [Header("Hover Raycast")]
    [Tooltip("Layer mask used for hover/click raycasts. Exclude UI layer to prevent flicker.")]
    public LayerMask hoverMask = ~0;
    [Tooltip("Exclude UI layer from raycast checks (default 5).")]
    public int uiLayerToExclude = 5;

    [Header("Cursor Feedback")]
    [Tooltip("Show a custom grab cursor when hovering the rod.")]
    public bool enableCursorFeedback = true;
    public Texture2D grabCursor;
    public Vector2 grabCursorHotspot = new Vector2(8, 8);
    public CursorMode grabCursorMode = CursorMode.Auto;
    [Tooltip("Resize the grab cursor to a consistent on-screen size.")]
    public bool resizeGrabCursor = true;
    [Tooltip("Desired cursor size in pixels (max dimension). Set 0 to use source size.")]
    public int grabCursorSize = 32;
    [Tooltip("Try to adjust size using screen DPI (uses 96dpi as baseline). If Screen.dpi is 0, ignored.")]
    public bool autoAdjustForDPI = true;

    [Header("Hover Scale")]
    [Tooltip("Slightly scale up when hovered.")]
    public bool enableHoverScale = true;
    public float hoverScale = 1.08f;
    public float hoverScaleInDuration = 0.12f;
    public float hoverScaleOutDuration = 0.12f;

    [Header("Drop Stabilization")]
    [Tooltip("Run a brief stabilization after drop to ensure the rod rests on ground and doesn't sink.")]
    public bool stabilizeAfterDrop = true;
    public float dropStabilizeDuration = 0.4f;
    public bool freezeTiltWhileStabilizing = true;
    [Tooltip("Clamp minimum world Y after drop.")]
    public bool clampMinYOnDrop = true;
    public float minYOnDrop = 0.0f;
    [Tooltip("Force world Y to a fixed value at/after drop.")]
    public bool forceFixedDropY = false;
    public float fixedDropY = 0.05f;

    [Header("Save")]
    [Tooltip("Persistent ID for save/load. Set a unique value per rod in the scene.")]
    public string saveId = "fishingrod_1";

    // Internal
    private Camera _cachedCamera;
    private Rigidbody _rb;
    private Collider[] _colliders;
    private Transform _originalParent;
    private Vector3 _originalLocalScale;
    private Vector3 _initialScale;
    private bool _isCarried;

    // Cursor ownership (single-owner to avoid flicker with other interactables)
    private static FishingRodManager s_cursorOwner;
    private bool _cursorOwned;
    private Texture2D _scaledGrabCursor;
    private Vector2 _scaledHotspot;
    private bool _cursorDirty;

    // Hover scale coroutine state
    private Coroutine _hoverScaleCo;
    private bool _hoverScaleActive;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _originalLocalScale = transform.localScale;
        _initialScale = _originalLocalScale;
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(saveId)) saveId = $"fishingrod_{gameObject.name}";
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
        _cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (uiLayerToExclude >= 0 && uiLayerToExclude < 32)
        {
            hoverMask &= ~(1 << uiLayerToExclude);
        }
        _cursorDirty = true;
    }

    private void Update()
    {
        if (!enableMouseInteraction) return;

        // Left click: pickup or drop
        if (Input.GetMouseButtonDown(0))
        {
            if (_isCarried)
            {
                Drop();
                return;
            }
            else if (IsThisRodClicked())
            {
                if (IsWithinMousePickupRange())
                    PickUp();
                return;
            }
        }

        // Hover detection
        bool hovering = !_isCarried && IsHoveringThisRod();

        // Cursor feedback
        if (enableCursorFeedback)
        {
            if (hovering)
            {
                if (s_cursorOwner != this)
                {
                    if (s_cursorOwner != null) s_cursorOwner.ResetCursorIfOwned();
                    s_cursorOwner = this;
                    _cursorOwned = false;
                }
                if (!_cursorOwned && grabCursor != null)
                {
                    EnsureScaledCursor();
                    var tex = _scaledGrabCursor != null ? _scaledGrabCursor : grabCursor;
                    var hs = _scaledGrabCursor != null ? _scaledHotspot : grabCursorHotspot;
                    Cursor.SetCursor(tex, hs, grabCursorMode);
                    _cursorOwned = true;
                }
            }
            else if (s_cursorOwner == this)
            {
                ResetCursorIfOwned();
                s_cursorOwner = null;
            }
        }

        // Hover scale visual
        UpdateHoverScaleVisual(hovering);
    }

    // ==== Pickup/Drop ====
    public void PickUp()
    {
        if (_isCarried) return;
        if (player == null)
        {
            Debug.LogWarning("FishingRodManager: Player transform not found.");
            return;
        }

        _originalParent = transform.parent;
        Transform parentTarget = handSocket != null ? handSocket : player;

        // Preserve world scale if desired
        Vector3 oldWorldScale = transform.lossyScale;
        transform.SetParent(parentTarget);

        if (gripPoint != null)
        {
            transform.localRotation = Quaternion.Inverse(gripPoint.localRotation);
            transform.localPosition = -(transform.localRotation * gripPoint.localPosition);
            if (useCustomCarriedPose)
            {
                transform.localPosition += carriedLocalPosition;
                transform.localRotation = transform.localRotation * Quaternion.Euler(carriedLocalEuler);
            }
        }
        else
        {
            if (useCustomCarriedPose)
            {
                transform.localPosition = carriedLocalPosition;
                transform.localEulerAngles = carriedLocalEuler;
            }
            else
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        if (preserveWorldScaleOnCarry && parentTarget != null)
        {
            Vector3 pScale = parentTarget.lossyScale;
            float sx = pScale.x != 0 ? oldWorldScale.x / pScale.x : 1f;
            float sy = pScale.y != 0 ? oldWorldScale.y / pScale.y : 1f;
            float sz = pScale.z != 0 ? oldWorldScale.z / pScale.z : 1f;
            transform.localScale = new Vector3(sx, sy, sz);
        }

        SetCarriedPhysics(true);
        _isCarried = true;
        CurrentCarried = this;
    }

    public void Drop()
    {
        if (!_isCarried) return;
        _isCarried = false;
        if (CurrentCarried == this) CurrentCarried = null;

        // Find ground point in front of player
        Vector3 groundPoint, groundNormal;
        if (!FindGroundPoint(out groundPoint, out groundNormal))
        {
            groundPoint = transform.position;
            groundNormal = Vector3.up;
        }

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

        transform.SetParent(_originalParent);
        transform.position = finalPos;

        if (alignUprightOnDrop)
        {
            float y = player != null ? player.eulerAngles.y : transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        // Restore original authored local scale
        transform.localScale = _originalLocalScale;

        SetCarriedPhysics(false);
        RefreshCollidersList();
        NudgeUpIfOverlapping();

        if (stabilizeAfterDrop)
        {
            StopAllCoroutines();
            StartCoroutine(StabilizeAfterDropCoroutine());
        }
    }

    // ==== Hover helpers ====
    private bool IsThisRodClicked()
    {
        if (_cachedCamera == null) return false;
        Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, hoverMask, QueryTriggerInteraction.Ignore))
        {
            var rm = hit.collider.GetComponentInParent<FishingRodManager>();
            return rm == this;
        }
        return false;
    }

    private bool IsHoveringThisRod()
    {
        if (_cachedCamera == null) return false;
        Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, hoverMask, QueryTriggerInteraction.Ignore))
            return false;
        var rm = hit.collider.GetComponentInParent<FishingRodManager>();
        return rm == this && IsWithinMousePickupRange();
    }

    private bool IsWithinMousePickupRange()
    {
        if (player == null) return false;
        float allowed = Mathf.Max(0.01f, mousePickupRange > 0f ? mousePickupRange : pickupRange);
        Vector3 pp = player.position;
        Vector3 bp = transform.position;
        float dx = pp.x - bp.x;
        float dz = pp.z - bp.z;
        float sqr = dx * dx + dz * dz; // planar distance
        return sqr <= allowed * allowed;
    }

    // ==== Physics/colliders ====
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
                var c = _colliders[i];
                if (c == null) continue;
                // Disable colliders when carried to avoid physics jitter with player
                c.enabled = !carried;
            }
        }
        DisableNestedRigidbodiesExceptRoot();
    }

    private void RefreshCollidersList()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    private void DisableNestedRigidbodiesExceptRoot()
    {
        var rbs = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            var rb = rbs[i];
            if (rb == null) continue;
            if (rb == _rb) continue;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    // ==== Grounding helpers ====
    private bool FindGroundPoint(out Vector3 point, out Vector3 normal)
    {
        Vector3 basePos = player != null ? player.position : transform.position;
        Vector3 forward = player != null ? player.forward : transform.forward;
        Vector3 origin = basePos + forward * dropForwardDistance + Vector3.up * dropRayUpOffset;

        int mask = groundLayer.value != 0 ? groundLayer.value : ~0;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dropRayDownDistance + dropRayUpOffset, mask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

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
        if (!TryGetCombinedBounds(out Bounds b)) return 0.15f;
        return Mathf.Max(b.extents.x, b.extents.z);
    }

    private float GetHalfHeight()
    {
        if (!TryGetCombinedBounds(out Bounds b)) return 0.1f;
        return Mathf.Max(0.01f, b.extents.y);
    }

    private bool TryGetCombinedBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool has = false;
        if (_colliders == null || _colliders.Length == 0)
        {
            RefreshCollidersList();
            if (_colliders == null || _colliders.Length == 0) return false;
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
        if (!TryGetCombinedBounds(out Bounds b)) return;
        int mask = ~0;
        const int maxSteps = 6;
        const float step = 0.01f;
        for (int i = 0; i < maxSteps; i++)
        {
            var center = b.center + Vector3.up * (i * step);
            var halfExtents = b.extents + Vector3.one * 0.001f;
            var hits = Physics.OverlapBox(center, halfExtents, transform.rotation, mask, QueryTriggerInteraction.Ignore);
            bool overlap = false;
            for (int j = 0; j < hits.Length; j++)
            {
                var col = hits[j];
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue; // ignore self
                overlap = true; break;
            }
            if (!overlap)
            {
                transform.position = center;
                break;
            }
        }
    }

    private IEnumerator StabilizeAfterDropCoroutine()
    {
        if (_rb == null) yield break;

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
            if (forceFixedDropY)
            {
                Vector3 pos = transform.position;
                if (pos.y != fixedDropY)
                {
                    pos.y = fixedDropY;
                    transform.position = pos;
                }
                Vector3 vel = _rb.velocity; if (vel.y != 0f) { vel.y = 0f; _rb.velocity = vel; }
            }
            else if (clampMinYOnDrop && transform.position.y < minYOnDrop)
            {
                Vector3 pos = transform.position; pos.y = minYOnDrop; transform.position = pos;
                Vector3 vel = _rb.velocity; if (vel.y < 0f) { vel.y = 0f; _rb.velocity = vel; }
            }

            yield return new WaitForFixedUpdate();
        }

        _rb.collisionDetectionMode = prevMode;
        _rb.interpolation = prevInterp;
        _rb.constraints = prevConstraints;
    }

    // ==== Cursor helpers ====
    private void ResetCursorIfOwned()
    {
        if (_cursorOwned)
        {
            var cmType = System.Type.GetType("CursorManager");
            if (cmType != null)
            {
                var instProp = cmType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var useDefault = cmType.GetMethod("UseDefaultNow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (instProp != null && useDefault != null)
                {
                    var inst = instProp.GetValue(null, null);
                    if (inst != null)
                    {
                        useDefault.Invoke(inst, null);
                    }
                    else
                    {
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    }
                }
                else
                {
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                }
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            _cursorOwned = false;
        }
    }

    private void EnsureScaledCursor()
    {
        if (!resizeGrabCursor || grabCursor == null)
        {
            if (_scaledGrabCursor != null)
            {
                Destroy(_scaledGrabCursor);
                _scaledGrabCursor = null;
            }
            return;
        }
        if (!_cursorDirty && _scaledGrabCursor != null) return;

        int target = Mathf.Clamp(grabCursorSize, 8, 256);
        float dpi = Screen.dpi;
        if (autoAdjustForDPI && dpi > 1f)
        {
            float scale = dpi / 96f;
            target = Mathf.Clamp(Mathf.RoundToInt(target * scale), 8, 256);
        }
        int srcW = grabCursor.width;
        int srcH = grabCursor.height;
        int maxSrc = Mathf.Max(srcW, srcH);
        float scaleF = maxSrc > 0 ? (float)target / maxSrc : 1f;
        int dstW = Mathf.Max(8, Mathf.RoundToInt(srcW * scaleF));
        int dstH = Mathf.Max(8, Mathf.RoundToInt(srcH * scaleF));

        var rt = RenderTexture.GetTemporary(dstW, dstH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(grabCursor, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, dstW, dstH), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        tex.filterMode = FilterMode.Bilinear;

        if (_scaledGrabCursor != null) Destroy(_scaledGrabCursor);
        _scaledGrabCursor = tex;
        _scaledHotspot = grabCursorHotspot * scaleF;
        _cursorDirty = false;
    }

    // ==== Hover scale ====
    private void UpdateHoverScaleVisual(bool desiredHover)
    {
        if (!enableHoverScale)
        {
            if (_hoverScaleActive)
            {
                EndHoverScale();
            }
            return;
        }
        if (desiredHover) StartHoverScale(); else EndHoverScale();
    }

    private void StartHoverScale()
    {
        Vector3 target = _initialScale * Mathf.Max(0.01f, hoverScale);
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

    private static bool NearlyEqual(in Vector3 a, in Vector3 b, float eps = 0.0005f)
    {
        return (a - b).sqrMagnitude <= eps * eps;
    }

    private void OnDisable()
    {
        if (s_cursorOwner == this)
        {
            ResetCursorIfOwned();
            s_cursorOwner = null;
        }
        if (CurrentCarried == this) CurrentCarried = null;
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
        }
        if (CurrentCarried == this) CurrentCarried = null;
        if (_scaledGrabCursor != null)
        {
            Destroy(_scaledGrabCursor);
            _scaledGrabCursor = null;
        }
        if (_hoverScaleCo != null) StopCoroutine(_hoverScaleCo);
        if (_hoverScaleActive || !NearlyEqual(transform.localScale, _initialScale))
        {
            transform.localScale = _initialScale;
            _hoverScaleActive = false;
        }
    }

    // ===== ISaveable =====
    public Dictionary<string, object> GetSaveData()
    {
        var data = new Dictionary<string, object>();
        data["saveId"] = string.IsNullOrEmpty(saveId) ? gameObject.name : saveId;
        data["isCarried"] = _isCarried;
        data["posX"] = transform.position.x;
        data["posY"] = transform.position.y;
        data["posZ"] = transform.position.z;
        data["rotX"] = transform.eulerAngles.x;
        data["rotY"] = transform.eulerAngles.y;
        data["rotZ"] = transform.eulerAngles.z;
        data["sX"] = transform.localScale.x;
        data["sY"] = transform.localScale.y;
        data["sZ"] = transform.localScale.z;
        return data;
    }

    public void LoadSaveData(Dictionary<string, object> data)
    {
        if (data == null) return;

        // Always drop and reset to world before applying transform
        if (_isCarried)
        {
            transform.SetParent(_originalParent);
            SetCarriedPhysics(false);
            _isCarried = false;
            if (CurrentCarried == this) CurrentCarried = null;
        }

        float px = GetFloat(data, "posX", transform.position.x);
        float py = GetFloat(data, "posY", transform.position.y);
        float pz = GetFloat(data, "posZ", transform.position.z);
        float rx = GetFloat(data, "rotX", transform.eulerAngles.x);
        float ry = GetFloat(data, "rotY", transform.eulerAngles.y);
        float rz = GetFloat(data, "rotZ", transform.eulerAngles.z);
        float sx = GetFloat(data, "sX", transform.localScale.x);
        float sy = GetFloat(data, "sY", transform.localScale.y);
        float sz = GetFloat(data, "sZ", transform.localScale.z);
        transform.position = new Vector3(px, py, pz);
        transform.eulerAngles = new Vector3(rx, ry, rz);
        transform.localScale = new Vector3(sx, sy, sz);
    }

    private static float GetFloat(Dictionary<string, object> data, string key, float def)
    {
        if (!data.TryGetValue(key, out var v) || v == null) return def;
        float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f); return f;
    }
}
