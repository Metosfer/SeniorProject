using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ObjectCarrying : MonoBehaviour
{
    #region Serialized Configuration
    [Header("General References")]
    [Tooltip("Oyuncu Transform referansı (boşsa tag=Player aranır)")] [SerializeField] private Transform playerTransform;

    [Header("Input Settings")] 
    [SerializeField] private KeyCode pickupKey = KeyCode.Tab; 
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape; 
    [Tooltip("Sol tık ile yerleştirme (değiştirmek isterseniz)")] [SerializeField] private int confirmMouseButton = 0; // LMB
    [Tooltip("Sağ tık ile döndürme (hold)")] [SerializeField] private int rotateMouseButton = 1; // RMB

    [Header("Placement Settings")] 
    [Tooltip("Objeyi alabilmek için maksimum mesafe")] [SerializeField] private float pickupRange = 3f; 
    [Tooltip("Zemin raycast için maksimum mesafe")] [SerializeField] private float groundRayDistance = 200f; 
    [Tooltip("Zemine hizalarken yukarı doğru ek offset (gömülmeyi önler)")] [SerializeField] private float verticalLiftWhileCarrying = 0.02f; 
    [Tooltip("Yerleştirme sırasında objeye eklenecek pozisyon offseti")] [SerializeField] private Vector3 placementOffset = Vector3.zero; 
    [Tooltip("Döndürme hızı (derece/sn)")] [SerializeField] private float rotateSpeed = 60f; 
    [Header("Vertical Scroll Adjust")] 
    [Tooltip("Mouse tekerleği ile dikey offset ayarlamayı aç/kapat")] [SerializeField] private bool enableScrollVerticalAdjust = true;
    [Tooltip("Scroll başına eklenecek dikey offset (unit)")] [SerializeField] private float scrollStep = 0.2f;
    [Tooltip("Scroll tutarak hızlı kaydırma çarpanı (Shift basılı)")] [SerializeField] private float fastScrollMultiplier = 3f;
    [Tooltip("Dikey offset alt limit")] [SerializeField] private float minScrollOffset = -1f;
    [Tooltip("Dikey offset üst limit")] [SerializeField] private float maxScrollOffset = 2f;
    [Tooltip("Yerleştirme veya iptalde dikey offseti sıfırla")] [SerializeField] private bool resetScrollOffsetOnEnd = true;

    [Header("Validation / Overlap")] 
    [Tooltip("Bu LayerMask içindeki collider'larla çakışma varsa yerleştirme GEÇERSİZ kabul edilir.")] [SerializeField] private LayerMask invalidOverlapMask; 
    [Tooltip("Ground raycast layer mask (boşsa tüm katmanlar)")] [SerializeField] private LayerMask groundRayMask = ~0; 
    [Tooltip("Overlap kutusuna eklenecek ekstra margin (pozitif = büyütme, negatif = küçültme)")] [SerializeField] private float overlapBoundsExpand = 0f; 

    [Header("Visual Feedback Colors")] 
    [Tooltip("Önizleme GEÇERLİ iken kullanılacak renk (alpha şeffaflık)")] [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.35f); 
    [Tooltip("Önizleme GEÇERSİZ iken kullanılacak renk (alpha şeffaflık)")] [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.35f); 
    [Tooltip("Yerleştirme önizlemesinde base alpha override etmek isterseniz -1 ise renklerin alpha'sını kullanır")] [SerializeField] private float overridePreviewAlpha = -1f; 
    [Tooltip("Önizleme bittiğinde orijinal alpha geri getirilir")]
    [SerializeField] private bool restoreOriginalColors = true; 

    [Header("Collider / Physics")]
    [Tooltip("Taşıma sırasında collider'ları devre dışı bırak")] [SerializeField] private bool disableCollidersWhileCarrying = true; 
    [Tooltip("Taşıma sırasında rigidbody varsa kinematic yap (bırakınca eski haline döndür)")] [SerializeField] private bool makeRigidbodyKinematic = true; 

    [Header("Quality / Smoothing")] 
    [Tooltip("Pozisyonu direkt atamak yerine hafif lerp uygula")] [SerializeField] private bool smoothFollow = true; 
    [SerializeField] private float followLerpSpeed = 25f; 

    [Header("Debug / Gizmos")] 
    [SerializeField] private bool debugLogs = false; 
    [SerializeField] private bool drawOverlapGizmo = false; 
    [Tooltip("Objeyi seçtiğinde pickup menzil dairesini çiz")] [SerializeField] private bool drawPickupRangeOnSelected = true;
    [Tooltip("Seçili değilken de pickup menzili çiz (sahnede kalıcı)")] [SerializeField] private bool drawPickupRangeAlways = false;
    [Tooltip("Pickup menzil gizmo rengi")] [SerializeField] private Color pickupRangeGizmoColor = new Color(0f, 0.9f, 1f, 1f);

    [Header("Scale Bounce Effect")] 
    [Tooltip("Pickup ve place sırasında ölçek bounce animasyonu uygula")] [SerializeField] private bool enableScaleBounce = true;
    [Tooltip("Pickup anındaki hedef ölçek çarpanı")] [SerializeField] private float pickupScaleMultiplier = 1.15f;
    [Tooltip("Yerleştirme anındaki hedef ölçek çarpanı")] [SerializeField] private float placeScaleMultiplier = 0.85f;
    [Tooltip("Bounce süresi (tek yön) saniye")] [SerializeField] private float bounceDuration = 0.15f;
    [Tooltip("Bounce eğrisi (0-1 zaman -> scale çarpanı)")] [SerializeField] private AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0,0,1,1);
    [Tooltip("Bounce tamamlanınca orijinal ölçeğe dön geçiş hızı")] [SerializeField] private float returnSpeed = 12f;

    [Header("Instruction UI")] 
    [Tooltip("Ekranda talimat göstermek için TextMeshProUGUI referansı (opsiyonel)")] [SerializeField] private TextMeshProUGUI instructionText;
    [Tooltip("Menzile girildiğinde gösterilecek pickup mesajı")] [SerializeField] private string pickupMessage = "Press TAB to pick up";
    [Tooltip("Taşıma sırasında valid yerleşim mesajı (place/rotate/cancel)")] [SerializeField] private string carryValidMessage = "LMB: Place  |  RMB: Rotate  |  ESC: Cancel";
    [Tooltip("Taşıma sırasında GEÇERSİZ konum mesajı")] [SerializeField] private string carryInvalidMessage = "Invalid position - adjust  |  ESC: Cancel";
    [Tooltip("Yerleşim onaylandıktan sonra gösterilecek kısa mesaj (boş bırak -> gösterme)")] [SerializeField] private string placedMessage = "Placed";
    [Tooltip("Pickup sonrası fade ile talimat gizle (sn)")] [SerializeField] private float placedMessageDuration = 1.2f;
    [Header("Instruction Stability")]
    [Tooltip("Mesaj gösterme / gizleme kararında küçük dalgalanmaları yok saymak için ek tolerans (metre)")] [SerializeField] private float instructionRangeHysteresis = 0.1f;
    [Tooltip("Aynı state'te iken UI metnini tekrar tekrar set etmeyi minimum bu aralıkla sınırla (sn)")] [SerializeField] private float instructionMinUpdateInterval = 0.05f;
    [Tooltip("Mesaj kaybolmadan önce menzil dışına kesintisiz çıkılması gereken süre (sn)")] [SerializeField] private float instructionExitGraceTime = 0.1f;
    [Tooltip("Sahne yüklendikten sonra UI değişikliklerini bastırma süresi (sn)")] [SerializeField] private float initialUISuppressDuration = 0.1f;
    [Tooltip("UI görünürlük değişimini uygulamadan önce gereken stabil süre (sn)")] [SerializeField] private float uiStateStabilityTime = 0.02f;
    [Header("Instruction Ownership")]
    [Tooltip("Aynı UI label'ını birden fazla obje kullanıyorsa en yakın olan tek başına günceller (flicker engelleme)")] [SerializeField] private bool exclusiveInstructionOwnership = false;
    [Tooltip("Mevcut owner'dan daha yakın sayılmak için gereken ek mesafe avantajı")] [SerializeField] private float ownerSwitchDistanceDelta = 0.15f;
    [Header("Sound Effects")]
    [Tooltip("Objeyi aldığında çalacak ses")] [SerializeField] private AudioClip pickupSFX;
    [Tooltip("Objeyi bıraktığında çalacak ses")] [SerializeField] private AudioClip placeSFX;
    [Tooltip("Objeyi iptal ettiğinde çalacak ses")] [SerializeField] private AudioClip cancelSFX;
    #endregion

    #region Internal State
    private enum InstructionDisplayState
    {
        Hidden,
        ShowingIdle,
        ShowingCarrying,
        ShowingPlaced
    }

    private bool _isCarrying = false; 
    private bool _isValidPlacement = false; 
    private Vector3 _originalPosition; 
    private Quaternion _originalRotation; 
    private List<Collider> _colliders = new List<Collider>();
    private Rigidbody _rb; 

    // Material handling
    private Renderer[] _renderers; 
    private List<Material> _instancedMaterials = new List<Material>();
    private List<Color> _originalColors = new List<Color>();

    // Cached target pos while previewing
    private Vector3 _targetPreviewPosition; 
    private float _currentScrollOffset = 0f;

    // scale bounce state
    private Vector3 _originalScale;
    private Vector3 _currentTargetScale;
    private float _bounceStartTime = -1f;
    private float _bouncePhaseDuration = 0f;
    private bool _bounceActive = false;
    private Vector3 _bounceFromScale;
    private Vector3 _bounceToScale;

    private float _placedMsgHideTime = -1f;
    private bool _instructionInsideRangeCached = false; // son stable durum
    private float _lastInstructionUpdateTime = -1f;
    private float _rangeExitTimestamp = -1f; // menzil dışına ilk çıktığı an
    private string _lastIdleInstructionText = null;
    private string _lastCarryInstructionText = null;
    private InstructionDisplayState _currentInstructionState = InstructionDisplayState.Hidden;
    private string _currentDisplayedText = "";

    private static ObjectCarrying _currentIdleInstructionOwner; // static ownership

    // UI stability state (similar to DryingAreaManager)
    private float _uiSuppressUntil;
    private bool _uiShown;
    private bool _uiDesiredLast;
    private float _uiDesiredSince;

    // Global static state for freezing camera scroll zoom
    public static int ActiveCarryCount { get; private set; } = 0; // kaç obje şu an taşınıyor
    [Header("Camera Interaction")] 
    [Tooltip("Taşıma sırasında kamera zoom (scroll) girişini kilitle")] [SerializeField] private bool freezeCameraZoomWhileCarrying = true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (playerTransform == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) playerTransform = playerGO.transform;
        }
        _renderers = GetComponentsInChildren<Renderer>(true);
        GetComponentsInChildren(_colliders); // extension-like generic collection
        _rb = GetComponent<Rigidbody>();
        _uiSuppressUntil = Time.unscaledTime + Mathf.Max(0f, initialUISuppressDuration);
    }

    private void Update()
    {
        if (!_isCarrying)
        {
            HandlePickupInput();
            UpdateInstructionIdle();
        }
        else
        {
            HandleCarryingUpdate();
            UpdateInstructionCarrying();
        }
        UpdateScaleBounce();
        UpdatePlacedMessageExpiry();
    }
    #endregion

    #region Input Flows
    private void HandlePickupInput()
    {
        if (Input.GetKeyDown(pickupKey))
        {
            if (playerTransform == null)
            {
                if (debugLogs) Debug.LogWarning("[ObjectCarrying] Player referansı yok, pickup iptal.");
                return;
            }
            float dist = Vector3.Distance(playerTransform.position, transform.position);
            if (dist <= pickupRange)
            {
                BeginCarry();
            }
            else if (debugLogs)
            {
                Debug.Log($"[ObjectCarrying] Pickup menzil dışı (dist={dist:F2} > {pickupRange})");
            }
        }
    }

    private void HandleCarryingUpdate()
    {
        // Cancel (consume ESC input to prevent pause menu)
        if (Input.GetKeyDown(cancelKey))
        {
            CancelCarry();
            // Consume the ESC input by marking it as used
            Input.ResetInputAxes();
            return;
        }

        // Rotation (RMB hold)
        if (Input.GetMouseButton(rotateMouseButton))
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.unscaledDeltaTime, Space.World);
        }

        // Update preview position based on mouse ray
        UpdatePreviewPosition();
        ValidatePlacement();
        ApplyPreviewVisual();

        // Confirm (LMB)
        if (Input.GetMouseButtonDown(confirmMouseButton))
        {
            if (_isValidPlacement)
            {
                ConfirmPlacement();
            }
            else if (debugLogs)
            {
                Debug.Log("[ObjectCarrying] Geçersiz konum – yerleştirme reddedildi.");
            }
        }
    }
    #endregion

    #region Carry Lifecycle
    private void BeginCarry()
    {
        if (_isCarrying) return;
        _isCarrying = true;
        if (freezeCameraZoomWhileCarrying) ActiveCarryCount++;
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        TriggerScaleBounce(true);
        _placedMsgHideTime = -1f; // yeni taşıma sırasında placed mesajı olmasın
        
        // Play pickup SFX
        PlaySFX(pickupSFX);

        // Cache colliders
        _colliders.Clear();
        GetComponentsInChildren(_colliders);
        if (disableCollidersWhileCarrying)
        {
            foreach (var c in _colliders) if (c != null) c.enabled = false;
        }
        if (_rb != null && makeRigidbodyKinematic)
        {
            _rb.isKinematic = true;
        }

        PrepareMaterialInstances();
        if (debugLogs) Debug.Log("[ObjectCarrying] Carry mode başladı");
    }

    private void CancelCarry()
    {
        if (!_isCarrying) return;
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;
        RestoreOriginalVisual();
        RestorePhysics();
        _isCarrying = false;
        if (freezeCameraZoomWhileCarrying && ActiveCarryCount > 0) ActiveCarryCount--;
        TriggerScaleBounce(false); // iptalde de küçük bir bounce isterseniz place parametresini kullanabiliriz; burada false ile place bounce kullanıyoruz
        if (resetScrollOffsetOnEnd) _currentScrollOffset = 0f;
        
        // Play cancel SFX
        PlaySFX(cancelSFX);
        
        if (instructionText != null)
        {
            // Cancel sonrası tekrar pickup mesajını göstermek için reset
            _placedMsgHideTime = -1f;
        }
        if (debugLogs) Debug.Log("[ObjectCarrying] Taşıma iptal edildi");
    }

    private void ConfirmPlacement()
    {
        if (!_isCarrying) return;
        RestoreOriginalVisual();
        RestorePhysics();
        _isCarrying = false;
        if (freezeCameraZoomWhileCarrying && ActiveCarryCount > 0) ActiveCarryCount--;
        TriggerScaleBounce(false);
        if (resetScrollOffsetOnEnd) _currentScrollOffset = 0f;
        
        // Play place SFX
        PlaySFX(placeSFX);
        
        if (instructionText != null && !string.IsNullOrEmpty(placedMessage))
        {
            // Show placed message immediately without stability delay
            if (instructionText.gameObject.activeSelf == false)
            {
                instructionText.gameObject.SetActive(true);
                _uiShown = true;
            }
            instructionText.text = placedMessage;
            _currentDisplayedText = placedMessage;
            _currentInstructionState = InstructionDisplayState.ShowingPlaced;
            _placedMsgHideTime = Time.unscaledTime + placedMessageDuration;
        }
        if (debugLogs) Debug.Log("[ObjectCarrying] Yerleştirme onaylandı");
    }

    private void RestorePhysics()
    {
        if (disableCollidersWhileCarrying)
        {
            foreach (var c in _colliders) if (c != null) c.enabled = true;
        }
        if (_rb != null && makeRigidbodyKinematic)
        {
            _rb.isKinematic = false;
        }
    }
    #endregion

    #region Preview & Validation
    private void UpdatePreviewPosition()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, groundRayDistance, groundRayMask, QueryTriggerInteraction.Ignore))
        {
            _targetPreviewPosition = hit.point + Vector3.up * (verticalLiftWhileCarrying + _currentScrollOffset) + placementOffset;
        }
        else
        {
            // Ray bir şeye çarpmadıysa ileri bir noktayı referans al
            _targetPreviewPosition = ray.GetPoint(5f) + Vector3.up * _currentScrollOffset + placementOffset;
        }

        // Scroll input
        if (enableScrollVerticalAdjust && _isCarrying)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                float mult = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? fastScrollMultiplier : 1f;
                _currentScrollOffset += scroll * scrollStep * mult;
                _currentScrollOffset = Mathf.Clamp(_currentScrollOffset, minScrollOffset, maxScrollOffset);
            }
        }

        if (smoothFollow)
        {
            transform.position = Vector3.Lerp(transform.position, _targetPreviewPosition, Time.unscaledDeltaTime * followLerpSpeed);
        }
        else
        {
            transform.position = _targetPreviewPosition;
        }
    }

    private void ValidatePlacement()
    {
        // Renderer bounds kullanarak yaklaşık kutu
        if (_renderers == null || _renderers.Length == 0)
        {
            _isValidPlacement = true; // render yoksa engellemeyelim
            return;
        }
        Bounds total = _renderers[0].bounds;
        for (int i = 1; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            total.Encapsulate(_renderers[i].bounds);
        }
        Vector3 center = total.center;
        Vector3 halfExtents = total.extents;
        if (overlapBoundsExpand != 0f) halfExtents += Vector3.one * overlapBoundsExpand;
        // OverlapBox: çakışan collider var mı?
        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, invalidOverlapMask, QueryTriggerInteraction.Ignore);
        bool foundForeign = false;
        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                bool isSelf = false;
                for (int c = 0; c < _colliders.Count; c++)
                {
                    if (h == _colliders[c]) { isSelf = true; break; }
                }
                if (!isSelf)
                {
                    foundForeign = true; break;
                }
            }
        }
        _isValidPlacement = !foundForeign;
    }

    private void ApplyPreviewVisual()
    {
        if (_instancedMaterials.Count == 0) return;
        Color target = _isValidPlacement ? validColor : invalidColor;
        float alpha = (overridePreviewAlpha >= 0f) ? overridePreviewAlpha : target.a;
        target.a = alpha;
        for (int i = 0; i < _instancedMaterials.Count; i++)
        {
            var m = _instancedMaterials[i];
            if (m == null) continue;
            if (m.HasProperty("_Color"))
            {
                var c = target; // directly set
                m.color = c;
            }
        }
    }

    private void PrepareMaterialInstances()
    {
        _instancedMaterials.Clear();
        _originalColors.Clear();
        if (_renderers == null) return;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            var mats = r.materials; // instanced already (Unity duplicates sharedMaterial when accessing .materials)
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                _instancedMaterials.Add(m);
                if (restoreOriginalColors && m.HasProperty("_Color"))
                {
                    _originalColors.Add(m.color);
                }
                else
                {
                    _originalColors.Add(Color.white);
                }
            }
        }
    }

    private void RestoreOriginalVisual()
    {
        if (!restoreOriginalColors) return;
        int colorIndex = 0;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            var mats = r.materials; // these correspond to _instancedMaterials order
            for (int i = 0; i < mats.Length && colorIndex < _originalColors.Count; i++)
            {
                var m = mats[i];
                if (m != null && m.HasProperty("_Color"))
                {
                    m.color = _originalColors[colorIndex];
                }
                colorIndex++;
            }
        }
    }
    #endregion

    #region Helpers
    private void GetComponentsInChildren(List<Collider> list)
    {
        list.Clear();
        var cols = GetComponentsInChildren<Collider>(true);
        if (cols != null) list.AddRange(cols);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
        
        audioSource.PlayOneShot(clip);
    }
    private void UpdateInstructionUI(bool desiredVisible, string desiredText = null)
    {
        if (instructionText == null) return;
        if (Time.unscaledTime < _uiSuppressUntil)
        {
            if (_uiShown) 
            { 
                instructionText.gameObject.SetActive(false); 
                _uiShown = false; 
            }
            _uiDesiredLast = desiredVisible; 
            _uiDesiredSince = Time.unscaledTime; 
            return;
        }
        if (desiredVisible)
        {
            if (!_uiShown) 
            { 
                instructionText.gameObject.SetActive(true); 
                _uiShown = true; 
            }
            if (desiredText != null && _currentDisplayedText != desiredText)
            {
                instructionText.text = desiredText;
                _currentDisplayedText = desiredText;
            }
            _uiDesiredLast = true; 
            _uiDesiredSince = Time.unscaledTime; 
            return;
        }
        if (desiredVisible != _uiDesiredLast)
        { 
            _uiDesiredLast = desiredVisible; 
            _uiDesiredSince = Time.unscaledTime; 
        }
        if (Time.unscaledTime - _uiDesiredSince >= Mathf.Max(0f, uiStateStabilityTime))
        {
            if (_uiShown != desiredVisible)
            { 
                instructionText.gameObject.SetActive(false); 
                _uiShown = false; 
                _currentDisplayedText = "";
            }
        }
    }
    private void OnDrawGizmos()
    {
        if (drawPickupRangeAlways)
        {
            Gizmos.color = pickupRangeGizmoColor;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Pickup range (only when selected)
        if (drawPickupRangeOnSelected)
        {
            Gizmos.color = pickupRangeGizmoColor;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
        if (drawOverlapGizmo && _renderers != null && _renderers.Length > 0)
        {
            Bounds total = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                total.Encapsulate(_renderers[i].bounds);
            }
            Vector3 half = total.extents + Vector3.one * overlapBoundsExpand;
            Gizmos.color = _isValidPlacement ? Color.green : Color.red;
            Gizmos.matrix = Matrix4x4.TRS(total.center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, half * 2f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
    #endregion

    private void UpdateInstructionIdle()
    {
        if (instructionText == null) return;
        if (_placedMsgHideTime > 0f) return; // placed mesajı gösteriliyorsa override etme
        if (playerTransform == null) 
        { 
            UpdateInstructionUI(false);
            return; 
        }
        
        float dist = Vector3.Distance(playerTransform.position, transform.position);
        bool currentlyInside = dist <= pickupRange;

        // Hysteresis: çıkarken pickupRange + hysteresis aşılana kadar 'inside' kabul et
        if (!_instructionInsideRangeCached && currentlyInside)
        {
            // dıştan içeri gerçekten girdi
            _instructionInsideRangeCached = true;
            _rangeExitTimestamp = -1f; // reset
        }
        else if (_instructionInsideRangeCached && !currentlyInside)
        {
            // Menzilin dışına ilk çıkış anı kaydedilir, grace süresi dolana kadar inside kabul etmeye devam
            if (_rangeExitTimestamp < 0f) _rangeExitTimestamp = Time.unscaledTime;
            // pickupRange + hysteresis mesafesini aşınca veya grace süresi dolunca gerçekten dışarı say
            if (dist > pickupRange + instructionRangeHysteresis && (Time.unscaledTime - _rangeExitTimestamp) >= instructionExitGraceTime)
            {
                _instructionInsideRangeCached = false;
            }
        }

        bool desiredVisible = _instructionInsideRangeCached;
        UpdateInstructionUI(desiredVisible, desiredVisible ? pickupMessage : null);
    }

    private void UpdateInstructionCarrying()
    {
        if (instructionText == null) return;
        
        // Generate desired text
        string desired = _isValidPlacement ? AppendScrollHint(carryValidMessage) : AppendScrollHint(carryInvalidMessage);
        
        // For carrying state, update immediately without stability delay
        if (instructionText.gameObject.activeSelf == false)
        {
            instructionText.gameObject.SetActive(true);
            _uiShown = true;
        }
        if (_currentDisplayedText != desired)
        {
            instructionText.text = desired;
            _currentDisplayedText = desired;
        }
        _currentInstructionState = InstructionDisplayState.ShowingCarrying;
    }

    private void UpdatePlacedMessageExpiry()
    {
        if (instructionText == null) return;
        if (_placedMsgHideTime > 0f && Time.unscaledTime >= _placedMsgHideTime)
        {
            // For placed message expiry, update immediately without stability delay
            instructionText.gameObject.SetActive(false);
            _uiShown = false;
            _currentDisplayedText = "";
            _currentInstructionState = InstructionDisplayState.Hidden;
            _placedMsgHideTime = -1f;
        }
    }

    private string AppendScrollHint(string baseMsg)
    {
        if (!enableScrollVerticalAdjust) return baseMsg;
        const string token = "SCROLL:";
        if (baseMsg.Contains(token)) return baseMsg; // primitive duplication guard if user already added
        return baseMsg + "  |  Scroll: Up/Down";
    }

    private void SetInstructionState(InstructionDisplayState newState, string text = "")
    {
        if (instructionText == null) return;
        
        // Only update if state or text actually changed
        if (_currentInstructionState != newState || _currentDisplayedText != text)
        {
            _currentInstructionState = newState;
            _currentDisplayedText = text;
            
            switch (newState)
            {
                case InstructionDisplayState.Hidden:
                    UpdateInstructionUI(false);
                    break;
                case InstructionDisplayState.ShowingIdle:
                    UpdateInstructionUI(true, text);
                    break;
                case InstructionDisplayState.ShowingCarrying:
                    UpdateInstructionUI(true, text);
                    break;
                case InstructionDisplayState.ShowingPlaced:
                    UpdateInstructionUI(true, text);
                    break;
            }
        }
    }

    #region Scale Bounce
    private void TriggerScaleBounce(bool isPickup)
    {
        if (!enableScaleBounce) return;
        if (_originalScale == Vector3.zero) _originalScale = transform.localScale;
        float mult = isPickup ? pickupScaleMultiplier : placeScaleMultiplier;
        _bounceFromScale = _originalScale;
        _bounceToScale = _originalScale * mult;
        _bounceStartTime = Time.unscaledTime;
        _bouncePhaseDuration = bounceDuration;
        _bounceActive = true;
    }

    private void UpdateScaleBounce()
    {
        if (!enableScaleBounce) return;
        if (_originalScale == Vector3.zero) _originalScale = transform.localScale; // capture once
        if (!_bounceActive)
        {
            // Return to original smoothly if drifted
            if (transform.localScale != _originalScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _originalScale, Time.unscaledDeltaTime * returnSpeed);
            }
            return;
        }
        float elapsed = Time.unscaledTime - _bounceStartTime;
        float t = Mathf.Clamp01(elapsed / _bouncePhaseDuration);
        float curve = bounceCurve != null ? bounceCurve.Evaluate(t) : t;
        transform.localScale = Vector3.LerpUnclamped(_bounceFromScale, _bounceToScale, curve);
        if (t >= 1f)
        {
            // reverse bounce to go back to original automatically
            _bounceFromScale = _bounceToScale;
            _bounceToScale = _originalScale;
            _bounceStartTime = Time.unscaledTime;
            _bouncePhaseDuration = bounceDuration;
            // if already returned close enough, deactivate
            if ((transform.localScale - _originalScale).sqrMagnitude < 0.0001f && curve >= 1f)
            {
                _bounceActive = false;
                transform.localScale = _originalScale;
            }
        }
    }
    #endregion
}
