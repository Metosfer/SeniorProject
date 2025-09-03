using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Harrow (rake) manager with mouse click pickup/drop like BucketManager.
// - Sol tık ile yerden al
// - Elde iken sol tık ile geri bırak
// - Bırakınca oyuncunun önünde, zemine oturacak şekilde sabitlenir
public class HarrowManager : MonoBehaviour, ISaveable
{
    // Global equip state used by farming logic
    public static bool IsEquipped { get; private set; }

    [Header("Save")]
    [Tooltip("Kalıcı kayıt için benzersiz ID")] public string saveId = "harrow_1";

    // Referanslar
    [Header("Referanslar")]
    [Tooltip("Oyuncu Transform referansı. Boş ise 'Player' tag'li obje bulunur.")]
    public Transform player;
    [Tooltip("Elde taşınırken bağlanacağı el soketi (oyuncu eli/kemik)")]
    public Transform handSocket;

    [Header("Etkileşim")]
    [Tooltip("Mouse ile tıklayıp alma/bırakma")]
    public bool enableMouseInteraction = true;
    [Tooltip("Mouse ile alma için maksimum mesafe (0 => pickupRange kullan)")]
    public float mousePickupRange = 3.0f;
    [Tooltip("Klavye ile alma için mesafe (şimdilik sadece görsel amaçlı)")]
    public float pickupRange = 2.0f;

    [Header("Taşıma Pozu (Local)")]
    public Vector3 carriedLocalPosition = new Vector3(0f, 0f, 0f);
    public Vector3 carriedLocalEuler = new Vector3(0f, 0f, 0f);
    public Vector3 carriedLocalScale = Vector3.one;

    [Header("Bırakma Konumlandırma")]
    [Tooltip("Bırakırken oyuncunun önüne atılacak yatay mesafe")]
    public float dropForwardDistance = 0.6f;
    [Tooltip("Aşağı doğru ray atışı için başlangıç yukarı offset")]
    public float dropRayUpOffset = 1.0f;
    [Tooltip("Aşağı doğru ray maksimum mesafe")]
    public float dropRayDownDistance = 3.0f;
    [Tooltip("Zemin layer mask. 0 ise tüm katmanlarda arar")]
    public LayerMask groundLayer;
    [Tooltip("Zeminin biraz üstüne koymak için küçük pay")]
    public float dropClearance = 0.02f;
    [Tooltip("Bırakırken dik duruşu koru (sadece Y rotasyonu)")]
    public bool alignUprightOnDrop = true;

    [Header("Hover Raycast")]
    [Tooltip("Hover/tıklama raycast'i için maske. UI katmanını hariç tutun.")]
    public LayerMask hoverMask = ~0;
    [Tooltip("UI katman numarası (varsayılan 5)")]
    public int uiLayerToExclude = 5;

    [Header("İmleç Geri Bildirimi")]
    public bool enableCursorFeedback = true;
    public Texture2D grabCursor;
    public Vector2 grabCursorHotspot = new Vector2(8, 8);
    public CursorMode grabCursorMode = CursorMode.Auto;
    [Tooltip("İmleç boyutunu yeniden ölçekle")] public bool resizeGrabCursor = true;
    [Tooltip("Hedef imleç boyutu (px)")] public int grabCursorSize = 32;
    [Tooltip("Ekran DPI'a göre ölçekle")] public bool autoAdjustForDPI = true;

    // Dahili durum
    public static HarrowManager CurrentCarried { get; private set; }
    private Rigidbody _rb;
    private Collider[] _colliders;
    private Transform _originalParent;
    private Vector3 _originalLocalScale;
    private Camera _cachedCamera;
    private bool _isCarried;

    // Basit cursor sahipliği (Bucket ile benzer mantık, debounce'suz)
    private static HarrowManager s_cursorOwner;
    private bool _cursorOwned;
    private Texture2D _scaledGrabCursor;
    private Vector2 _scaledHotspot;
    private bool _cursorDirty;

    [Header("Hover Scale")]
    [Tooltip("Mouse üstüne geldiğinde tırmığı biraz büyüt")] public bool enableHoverScale = true;
    [Tooltip("Hedef ölçek çarpanı (1 = orijinal)")] public float hoverScale = 1.08f;
    [Tooltip("Hover'a geçiş süresi (sn)")] public float hoverScaleInDuration = 0.12f;
    [Tooltip("Hover'dan çıkış süresi (sn)")] public float hoverScaleOutDuration = 0.12f;
    private Coroutine _hoverScaleCo;
    private bool _hoverScaleActive;
    private Vector3 _initialScale;

    [Header("Taşıma Pozu Seçeneği")]
    [Tooltip("Özel taşıma ofseti kullan (kapalı ise socket'in tam konumu/rotasyonu kullanılır)")]
    public bool useCustomCarriedPose = true;
    [Tooltip("Elde taşırken dünya ölçeğini koru (parent ölçeği farklıysa bozulmayı engeller)")]
    public bool preserveWorldScaleOnCarry = true;
    [Tooltip("Hara üzerinde kavrama noktası (bu nokta socket'e hizalanır)")]
    public Transform gripPoint;
    [Header("Gizmos")]
    [Tooltip("Scene görünümünde taşıma pozunu gösteren küp boyutu")]
    public float gizmoCarrySize = 0.1f;

    [Header("Drop Stabilizasyon / Emniyet")]
    [Tooltip("Bıraktıktan sonra kısa süre zemine oturtmayı stabilize et")] public bool stabilizeAfterDrop = true;
    [Tooltip("Stabilizasyon süresi (sn)")] public float dropStabilizeDuration = 0.4f;
    [Tooltip("Stabilizasyon sırasında X/Z dönmeyi kilitle")] public bool freezeTiltWhileStabilizing = true;
    [Tooltip("Y eksenini minimum değerin altına düşürme")] public bool clampMinYOnDrop = true;
    public float minYOnDrop = 0.0f;
    [Tooltip("Sabit bir Y yüksekliğine zorla (ör. düz sahnede)")] public bool forceFixedDropY = false;
    public float fixedDropY = 0.05f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _originalLocalScale = transform.localScale;
    }

    private void Start()
    {
    // Ensure stable saveId if inspector left empty
    if (string.IsNullOrEmpty(saveId)) saveId = $"harrow_{gameObject.name}";
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
    // Cache initial scale for non-compounding hover scaling
    _initialScale = _originalLocalScale;
    }

    private void Update()
    {
        if (!enableMouseInteraction) return;

        // Sol tık: ya al, ya bırak
        if (Input.GetMouseButtonDown(0))
        {
            if (_isCarried)
            {
                Drop();
                return;
            }
            else if (IsThisHarrowClicked())
            {
                if (IsWithinMousePickupRange())
                    PickUp();
                return;
            }
        }

        // Hover detection (single evaluation)
        bool hovering = !_isCarried && IsHoveringThisHarrow();

        // Hover cursor
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
        if (desiredHover)
            StartHoverScale();
        else
            EndHoverScale();
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

    public static void SetEquipped(bool equipped)
    {
        IsEquipped = equipped;
    }

    public void PickUp()
    {
        if (_isCarried) return;
        if (player == null)
        {
            Debug.LogWarning("HarrowManager: Player referansı bulunamadı.");
            return;
        }
        _originalParent = transform.parent;
        Transform parentTarget = handSocket != null ? handSocket : player;
        // Dünya ölçeğini korumak için önceden kaydet
        Vector3 oldWorldScale = transform.lossyScale;
        transform.SetParent(parentTarget);

        // Eğer gripPoint tanımlıysa, bu noktayı socket merkezine hizala
        if (gripPoint != null)
        {
            // Local uzayda hizalama: root'un local dönüşünü gripPoint'in tersine ayarla
            transform.localRotation = Quaternion.Inverse(gripPoint.localRotation);
            // Ardından pozisyonu, bu yeni dönüştürülmüş eksende gripPoint local pozunun negatifi olacak şekilde ayarla
            transform.localPosition = -(transform.localRotation * gripPoint.localPosition);
            // Ekstra kullanıcı ofsetini istersen uygula
            if (useCustomCarriedPose)
            {
                transform.localPosition += carriedLocalPosition;
                transform.localRotation = transform.localRotation * Quaternion.Euler(carriedLocalEuler);
            }
        }
        else
        {
            // Poz/rot ofseti
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
        // Ölçek: dünya ölçeğini koru veya özel ölçek kullanma
        if (preserveWorldScaleOnCarry && parentTarget != null)
        {
            Vector3 pScale = parentTarget.lossyScale;
            float sx = pScale.x != 0 ? oldWorldScale.x / pScale.x : 1f;
            float sy = pScale.y != 0 ? oldWorldScale.y / pScale.y : 1f;
            float sz = pScale.z != 0 ? oldWorldScale.z / pScale.z : 1f;
            transform.localScale = new Vector3(sx, sy, sz);
        }
        else
        {
            // Aynen kalsın (Inspector'daki local scale)
        }

        SetCarriedPhysics(true);
        _isCarried = true;
        CurrentCarried = this;
        SetEquipped(true);
    }

    public void Drop()
    {
        if (!_isCarried) return;
        _isCarried = false;
        if (CurrentCarried == this) CurrentCarried = null;

        // Zemin bul ve yerleştir
        Vector3 groundPoint;
        Vector3 groundNormal;
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

        transform.localScale = _originalLocalScale;

        SetCarriedPhysics(false);
        RefreshCollidersList();
        NudgeUpIfOverlapping();

        if (stabilizeAfterDrop)
        {
            StopAllCoroutines();
            StartCoroutine(StabilizeAfterDropCoroutine());
        }

        SetEquipped(false);
    }

    private bool IsThisHarrowClicked()
    {
        if (_cachedCamera == null) return false;
        Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, hoverMask, QueryTriggerInteraction.Ignore))
        {
            var hm = hit.collider.GetComponentInParent<HarrowManager>();
            return hm == this;
        }
        return false;
    }

    private bool IsHoveringThisHarrow()
    {
        if (_cachedCamera == null) return false;
        Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, hoverMask, QueryTriggerInteraction.Ignore))
            return false;
        var hm = hit.collider.GetComponentInParent<HarrowManager>();
        return hm == this && IsWithinMousePickupRange();
    }

    private bool IsWithinMousePickupRange()
    {
        if (player == null) return false;
        float allowed = Mathf.Max(0.01f, mousePickupRange > 0f ? mousePickupRange : pickupRange);
        Vector3 pp = player.position;
        Vector3 bp = transform.position;
        float dx = pp.x - bp.x;
        float dz = pp.z - bp.z;
        float sqr = dx * dx + dz * dz; // planar
        return sqr <= allowed * allowed;
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
                var c = _colliders[i];
                if (c == null) continue;
                c.isTrigger = carried; // eldeyken temas etmesin
                c.enabled = true;
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
        Bounds b;
        if (!TryGetCombinedBounds(out b)) return 0.15f;
        return Mathf.Max(b.extents.x, b.extents.z);
    }

    private float GetHalfHeight()
    {
        Bounds b;
        if (!TryGetCombinedBounds(out b)) return 0.1f;
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
            b.center += Vector3.up * step;
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
            // Force/clamp Y
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

    private void OnDisable()
    {
        if (s_cursorOwner == this)
        {
            ResetCursorIfOwned();
            s_cursorOwner = null;
        }
        if (CurrentCarried == this) CurrentCarried = null;
        // Reset hover scale
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
        // Ensure restored scale
        if (_hoverScaleCo != null) StopCoroutine(_hoverScaleCo);
        if (_hoverScaleActive || !NearlyEqual(transform.localScale, _initialScale))
        {
            transform.localScale = _initialScale;
            _hoverScaleActive = false;
        }
    }

    private static bool NearlyEqual(in Vector3 a, in Vector3 b, float eps = 0.0005f)
    {
        return (a - b).sqrMagnitude <= eps * eps;
    }

    // ===== ISaveable =====
    public Dictionary<string, object> GetSaveData()
    {
        var data = new Dictionary<string, object>();
    // Include saveId for diagnostics (object identity is primarily handled by GameSaveManager)
    data["saveId"] = string.IsNullOrEmpty(saveId) ? gameObject.name : saveId;
        data["isCarried"] = _isCarried;
        data["isEquipped"] = IsEquipped;
        data["posX"] = transform.position.x;
        data["posY"] = transform.position.y;
        data["posZ"] = transform.position.z;
        data["rotX"] = transform.eulerAngles.x;
        data["rotY"] = transform.eulerAngles.y;
        data["rotZ"] = transform.eulerAngles.z;
        data["sX"] = transform.localScale.x;
        data["sY"] = transform.localScale.y;
        data["sZ"] = transform.localScale.z;
    Debug.Log($"[Harrow Save] id={saveId}, pos={transform.position}, rotY={transform.eulerAngles.y:F1}");
        return data;
    }

    public void LoadSaveData(Dictionary<string, object> data)
    {
        if (data == null) return;
        // Always ensure object is dropped and unequipped on load for stability
        if (_isCarried)
        {
            // Reset to world before applying transform
            transform.SetParent(_originalParent);
            SetCarriedPhysics(false);
            _isCarried = false;
            if (CurrentCarried == this) CurrentCarried = null;
        }
        SetEquipped(false);

        // Read transform
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

    Debug.Log($"[Harrow Load] id={saveId}, pos=({px:F2},{py:F2},{pz:F2}), rotY={ry:F1}");

        // Optional: respect saved equipped state off only; ignore true to avoid spawning in hand after scene load
        // If you want to re-equip on load, uncomment below and ensure player/socket is valid
        // bool savedEquipped = GetBool(data, "isEquipped");
        // if (savedEquipped) { PickUp(); }
    }

    private static float GetFloat(Dictionary<string, object> data, string key, float def)
    {
        if (!data.TryGetValue(key, out var v) || v == null) return def;
        float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f); return f;
    }
    private static bool GetBool(Dictionary<string, object> data, string key, bool def = false)
    {
        if (!data.TryGetValue(key, out var v) || v == null) return def;
        bool.TryParse(v.ToString(), out bool b); return b;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 0.2f, 0.35f);
        Vector3 pc = player != null ? player.position : transform.position;
        Gizmos.DrawWireSphere(pc, pickupRange);

        // Taşıma pozu önizleme (handSocket gerektirir)
        if (handSocket != null)
        {
            var prev = Gizmos.matrix;
            // Hedef local poz/rot: özel veya sıfır
            Vector3 pos = useCustomCarriedPose ? carriedLocalPosition : Vector3.zero;
            Quaternion rot = useCustomCarriedPose ? Quaternion.Euler(carriedLocalEuler) : Quaternion.identity;
            Matrix4x4 m = Matrix4x4.TRS(handSocket.position, handSocket.rotation, Vector3.one) * Matrix4x4.TRS(pos, rot, Vector3.one);
            Gizmos.matrix = m;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.7f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * gizmoCarrySize);
            // Eksen çizgileri
            Gizmos.color = Color.red;   Gizmos.DrawLine(Vector3.zero, Vector3.right * gizmoCarrySize);
            Gizmos.color = Color.green; Gizmos.DrawLine(Vector3.zero, Vector3.up * gizmoCarrySize);
            Gizmos.color = Color.blue;  Gizmos.DrawLine(Vector3.zero, Vector3.forward * gizmoCarrySize);
            Gizmos.matrix = prev;
        }
    }
#endif
}
