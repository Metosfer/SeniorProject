using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerSoundManager - PlayerMovement ile senkronize ayak sesi sistemi
/// Hız bazlı timing ve sağ/sol ayak sesi desteği
/// </summary>
public class PlayerSoundManager : MonoBehaviour
{
    [Header("Dual Footstep Settings")]
    [Tooltip("Sol ayak ses dosyası")]
    public AudioClip leftFootSound;
    [Tooltip("Sağ ayak ses dosyası")]
    public AudioClip rightFootSound;
    [Tooltip("Fallback: Tek ses dosyası (hem sağ hem sol için)")]
    public AudioClip generalStepSound;
    
    [Header("Audio Source")]
    [Tooltip("Ayak sesleri için AudioSource (boş bırakılırsa otomatik oluşturulur)")]
    public AudioSource stepAudioSource;
    
    [Header("Volume & Pitch")]
    [Range(0f, 1f)]
    [Tooltip("Ayak sesi volume seviyesi")]
    public float stepVolume = 0.7f;
    [Range(0.8f, 1.2f)]
    [Tooltip("Minimum pitch değeri (çeşitlilik için)")]
    public float minPitch = 0.9f;
    [Range(0.8f, 1.2f)]
    [Tooltip("Maximum pitch değeri (çeşitlilik için)")]
    public float maxPitch = 1.1f;
    
    [Header("Speed-Based Timing")]
    [Tooltip("Yavaş yürüme hızında adım arası süre (saniye)")]
    [Range(0.2f, 1f)]
    public float slowWalkStepInterval = 0.6f;
    [Tooltip("Normal yürüme hızında adım arası süre (saniye)")]
    [Range(0.2f, 0.8f)]
    public float normalWalkStepInterval = 0.5f;
    [Tooltip("Koşma hızında adım arası süre (saniye)")]
    [Range(0.1f, 0.4f)]
    public float runStepInterval = 0.3f;
    [Tooltip("Hız eşik değerleri için referans hızlar")]
    [Range(1f, 15f)]
    public float walkSpeedThreshold = 6f;     // Normal yürüme hızı
    [Range(8f, 20f)]
    public float runSpeedThreshold = 12f;     // Koşma hızı
    
    [Header("Ground Detection")]
    [Tooltip("Zemin kontrolü için transform (genelde ayak pozisyonu)")]
    public Transform groundCheckObject;
    [Tooltip("Zemin layer mask")]
    public LayerMask groundLayerMask = 1;
    [Tooltip("Zemin kontrol mesafesi")]
    public float groundCheckDistance = 1.1f;
    
    [Header("Debug & Test")]
    [Tooltip("Inspector'dan manuel sağ ayak sesi test")]
    public bool testRightFoot = false;
    [Tooltip("Inspector'dan manuel sol ayak sesi test")]
    public bool testLeftFoot = false;
    [Tooltip("Console'a footstep timing bilgilerini yazdır")]
    public bool debugFootstepTiming = false;
    
    [Header("Durum Bilgileri (Sadece Görüntüleme)")]
    [SerializeField] private bool isLeftFootNext = true;      // Sıradaki ayak (sol = true, sağ = false)
    [SerializeField] private float currentStepInterval = 0.5f; // Mevcut adım aralığı
    [SerializeField] private float timeSinceLastStep = 0f;     // Son adımdan beri geçen süre
    [SerializeField] private bool isMoving = false;            // Hareket ediyor mu?
    [SerializeField] private bool isGrounded = true;           // Stabilize edilmiş grounded (Inspector gösterim)
    [SerializeField] private float currentPlayerSpeed = 0f;   // Mevcut oyuncu hızı

    [Header("Ground Stabilization")]
    [Tooltip("Yerde kalma durumunda flicker'ı filtrelemek için coyote time süresi (saniye)")]
    [Range(0f, 0.5f)] public float groundedGraceTime = 0.15f;
    [Tooltip("Ungrounded kabul edilmeden önce izin verilen ardışık false frame sayısı")]
    [Range(0, 10)] public int ungroundedFrameTolerance = 2;
    [Tooltip("Ek sphere/raycast probu yarıçapı (kararlı temas için)")]
    [Range(0.05f, 0.6f)] public float groundProbeRadius = 0.25f;
    [Tooltip("Probe origin Y offset (zemine gömülme / slope geçişlerinde stabilite)")]
    [Range(-0.2f, 0.5f)] public float groundProbeYOffset = 0.05f;
    [Tooltip("Raw (anlık) grounded durumu - debug")]
    [SerializeField] private bool rawGrounded = true;
    [Tooltip("Stabilize edilmiş grounded - internal")]
    [SerializeField] private bool stableGrounded = true;

    [Header("Speed Based Footsteps")] 
    [Tooltip("Ayak sesi başlatmak için minimum hız (örn: 0.5). 0 yaparsanız çok düşük hızlarda da ses olur.")]
    [Range(0f, 5f)] public float minSpeedForFootsteps = 0.6f;
    [Tooltip("Seslerin çalması için yerde olma şartı (false ise havadayken hız yeterliyse çalabilir)")]
    public bool requireGroundedForSteps = true;
    
    // Private References
    private PlayerMovement playerMovement;
    private CharacterController characterController;
    private Vector3 lastPosition;
    private float lastStepTime = 0f;
    private int consecutiveUngroundedFrames = 0;
    private float lastGroundedTime = 0f;
    private bool previousStableGrounded = true;
    
    void Start()
    {
        InitializeComponents();
        InitializeAudioSource();
        InitializeVolumeSettings();
        
        // Başlangıç pozisyonunu kaydet
        lastPosition = transform.position;
        lastStepTime = Time.time;
        lastGroundedTime = Time.time;
        previousStableGrounded = true;
        
        Debug.Log("🦶 PlayerSoundManager initialized with dual-footstep system");
    }
    
    void Update()
    {
        UpdateMovementData();
        UpdateFootstepTiming();
        HandleTestControls();
    }
    
    /// <summary>
    /// Gerekli component referanslarını initialize et
    /// </summary>
    private void InitializeComponents()
    {
        // PlayerMovement referansını al
        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogWarning("⚠️ PlayerSoundManager: PlayerMovement not found! Speed synchronization disabled.");
        }
        
        // CharacterController referansını al
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogWarning("⚠️ PlayerSoundManager: CharacterController not found!");
        }
    }
    
    /// <summary>
    /// AudioSource'u initialize et
    /// </summary>
    private void InitializeAudioSource()
    {
        // AudioSource yoksa oluştur
        if (stepAudioSource == null)
        {
            stepAudioSource = gameObject.AddComponent<AudioSource>();
            stepAudioSource.playOnAwake = false;
            stepAudioSource.spatialBlend = 0f; // 2D ses (oyuncu ses efekti)
            stepAudioSource.priority = 64;     // Orta öncelik
        }
        
        stepAudioSource.volume = stepVolume;
        
        Debug.Log("🔊 PlayerSoundManager AudioSource initialized");
    }
    
    /// <summary>
    /// Volume ayarlarını initialize et ve SettingsManager'a bağlan
    /// </summary>
    private void InitializeVolumeSettings()
    {
        // SettingsManager'dan mevcut volume ayarını al
        if (SettingsManager.Instance != null)
        {
            var settings = SettingsManager.Instance.Current;
            UpdateVolumeFromSettings(settings.soundEffectsVolume);
            
            // Settings değişikliklerini dinle
            SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
            
            Debug.Log($"🔊 PlayerSoundManager volume initialized from SettingsManager: {settings.soundEffectsVolume:F2}");
        }
        else
        {
            // Fallback: PlayerPrefs'ten yükle
            float savedVolume = PlayerPrefs.GetFloat("SoundEffectsVolume", 0.7f);
            UpdateVolumeFromSettings(savedVolume);
            
            Debug.LogWarning("⚠️ PlayerSoundManager: SettingsManager not found, using PlayerPrefs fallback");
        }
    }
    
    /// <summary>
    /// SettingsManager'dan ayar değişikliği geldiğinde çağrılır
    /// </summary>
    private void OnSettingsChanged(SettingsManager.GameSettings settings)
    {
        UpdateVolumeFromSettings(settings.soundEffectsVolume);
        Debug.Log($"🔊 PlayerSoundManager volume updated: {settings.soundEffectsVolume:F2}");
    }
    
    /// <summary>
    /// Volume ayarını uygula
    /// </summary>
    private void UpdateVolumeFromSettings(float soundEffectsVolume)
    {
        if (stepAudioSource != null)
        {
            // stepVolume ile soundEffectsVolume'u çarp
            stepAudioSource.volume = stepVolume * soundEffectsVolume;
        }
    }
    
    /// <summary>
    /// Component destroy edildiğinde event listener'ı temizle
    /// </summary>
    void OnDestroy()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
        }
    }
    
    /// <summary>
    /// Hareket verilerini güncelle
    /// </summary>
    private void UpdateMovementData()
    {
        // Hız bilgisi (varsa PlayerMovement'tan daha doğru alınır)
        if (playerMovement != null)
            currentPlayerSpeed = playerMovement.CurrentSpeed();
        else
        {
            Vector3 currentPosition = transform.position;
            Vector3 horizontalMovement = new Vector3(currentPosition.x, 0, currentPosition.z) - new Vector3(lastPosition.x, 0, lastPosition.z);
            currentPlayerSpeed = horizontalMovement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = currentPosition;
        }

        // Raw grounded tespiti
        rawGrounded = ComputeRawGrounded();

        // Stabilizasyon (coyote time + frame toleransı)
        if (rawGrounded)
        {
            stableGrounded = true;
            lastGroundedTime = Time.time;
            consecutiveUngroundedFrames = 0;
        }
        else
        {
            consecutiveUngroundedFrames++;
            // Grace time ve frame toleransı dolunca gerçekten ungrounded kabul et
            if (Time.time - lastGroundedTime > groundedGraceTime && consecutiveUngroundedFrames > ungroundedFrameTolerance)
            {
                stableGrounded = false;
            }
        }

        // Inspector gösterimi için
        isGrounded = stableGrounded;
        
        // Transisyon debug
        if (debugFootstepTiming && previousStableGrounded != stableGrounded)
        {
            Debug.Log($"🟢 Grounded State Changed -> {(stableGrounded ? "GROUND" : "AIR")} (Raw={rawGrounded})");
        }
        previousStableGrounded = stableGrounded;
        
        // Hareket ediyor mu kontrolü
        isMoving = currentPlayerSpeed > 0.1f;
        
        // Hıza göre adım aralığını hesapla
        currentStepInterval = CalculateStepInterval(currentPlayerSpeed);
        
        // Son adımdan beri geçen süreyi güncelle
        timeSinceLastStep = Time.time - lastStepTime;
    }

    /// <summary>
    /// Raw (anlık) grounded bilgisini hesaplar (multi-probe + CharacterController + PlayerMovement fallback)
    /// </summary>
    private bool ComputeRawGrounded()
    {
        // Öncelik: PlayerMovement özel grounded (daha doğru olabilir)
        if (playerMovement != null)
        {
            try { if (playerMovement.IsGrounded()) return true; } catch { /* ignore */ }
        }

        // CharacterController varsa hızlı kontrol
        if (characterController != null && characterController.isGrounded)
            return true;

        // Sphere / Ray probeleri
        Vector3 baseOrigin = (groundCheckObject != null ? groundCheckObject.position : transform.position) + Vector3.up * groundProbeYOffset;
        float maxDistance = groundCheckDistance;
        RaycastHit hit;

        // SphereCast (daha stabil)
        if (Physics.SphereCast(baseOrigin, groundProbeRadius, Vector3.down, out hit, maxDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y > 0.05f) return true; // eğimli yüzey toleransı
        }

        // Çoklu ray (merkez + 4 yön)
        Vector3 r = new Vector3(groundProbeRadius * 0.8f, 0, 0);
        Vector3 f = new Vector3(0, 0, groundProbeRadius * 0.8f);
        Vector3[] offsets = new Vector3[] { Vector3.zero, r, -r, f, -f };
        foreach (var o in offsets)
        {
            Vector3 origin = baseOrigin + o;
            if (Physics.Raycast(origin, Vector3.down, out hit, maxDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.05f)
                    return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Hıza göre adım aralığını hesapla
    /// </summary>
    private float CalculateStepInterval(float speed)
    {
        if (speed <= 0.1f)
        {
            return slowWalkStepInterval; // Duruyorsa en yavaş
        }
        else if (speed <= walkSpeedThreshold)
        {
            // Yavaş - normal yürüme arası interpolasyon
            float t = speed / walkSpeedThreshold;
            return Mathf.Lerp(slowWalkStepInterval, normalWalkStepInterval, t);
        }
        else if (speed <= runSpeedThreshold)
        {
            // Normal yürüme - koşma arası interpolasyon
            float t = (speed - walkSpeedThreshold) / (runSpeedThreshold - walkSpeedThreshold);
            return Mathf.Lerp(normalWalkStepInterval, runStepInterval, t);
        }
        else
        {
            // Çok hızlı koşma
            return runStepInterval;
        }
    }
    
    /// <summary>
    /// Ayak sesi timing'ini kontrol et ve gerekirse ses çal
    /// </summary>
    private void UpdateFootstepTiming()
    {
        // Footstep çalma koşulları (hız bazlı):
        // 1. Hız minSpeedForFootsteps üstünde
        // 2. (Opsiyonel) Yerde olma şartı (requireGroundedForSteps)
        // 3. Adım aralığı süresi dolmuş
        bool speedCondition = currentPlayerSpeed >= minSpeedForFootsteps;
        bool groundCondition = !requireGroundedForSteps || stableGrounded;
        bool intervalCondition = timeSinceLastStep >= currentStepInterval;
        bool shouldPlayFootstep = speedCondition && groundCondition && intervalCondition;
        
        if (shouldPlayFootstep)
        {
            PlayFootstep();
            lastStepTime = Time.time;
            timeSinceLastStep = 0f;
            
            // Ayak sırasını değiştir
            isLeftFootNext = !isLeftFootNext;
        }
        else if (debugFootstepTiming)
        {
            // Neden çalmadığını görmek için kısa debug (seyrekleştirme için her 0.25s)
            if (intervalCondition && timeSinceLastStep > currentStepInterval * 0.5f)
            {
                Debug.Log($"⏳ SkipFootstep - Speed {currentPlayerSpeed:F2}/{minSpeedForFootsteps:F2} ok? {currentPlayerSpeed >= minSpeedForFootsteps} | Ground {(stableGrounded ? "Y" : "N")} need? {requireGroundedForSteps} | Interval {timeSinceLastStep:F2}/{currentStepInterval:F2}");
            }
        }
    }
    
    /// <summary>
    /// Sıradaki ayak sesini çal (sağ/sol alternating)
    /// </summary>
    private void PlayFootstep()
    {
        AudioClip soundToPlay = null;
        string footType = "";
        
        // Hangi ayak sesi çalınacak?
        if (isLeftFootNext)
        {
            soundToPlay = leftFootSound != null ? leftFootSound : generalStepSound;
            footType = "Left";
        }
        else
        {
            soundToPlay = rightFootSound != null ? rightFootSound : generalStepSound;
            footType = "Right";
        }
        
        // Ses dosyası yoksa çıkış
        if (soundToPlay == null)
        {
            Debug.LogWarning("⚠️ No footstep sound assigned!");
            return;
        }
        
        // Ses çal
        PlayFootstepSound(soundToPlay, footType);
    }
    
    /// <summary>
    /// Footstep sesini rastgele pitch ile çal
    /// </summary>
    private void PlayFootstepSound(AudioClip clip, string footType)
    {
        if (stepAudioSource.isPlaying)
        {
            stepAudioSource.Stop(); // Önceki sesi kes (hızlı hareket için)
        }
        
        // Rastgele pitch değeri
        float randomPitch = Random.Range(minPitch, maxPitch);
        
        // AudioSource ayarları
        stepAudioSource.clip = clip;
        stepAudioSource.pitch = randomPitch;
        
        // Volume ayarını SettingsManager'dan al ve uygula
        float soundEffectsVolume = 0.7f;
        if (SettingsManager.Instance != null)
        {
            soundEffectsVolume = SettingsManager.Instance.Current.soundEffectsVolume;
        }
        else
        {
            soundEffectsVolume = PlayerPrefs.GetFloat("SoundEffectsVolume", 0.7f);
        }
        stepAudioSource.volume = stepVolume * soundEffectsVolume;
        
        // Ses çal
        stepAudioSource.Play();
        
        if (debugFootstepTiming)
        {
            Debug.Log($"🦶 {footType} footstep - Speed: {currentPlayerSpeed:F2} (min {minSpeedForFootsteps:F2}), Interval: {currentStepInterval:F2}s, Pitch: {randomPitch:F2}, GroundOK: {!requireGroundedForSteps || stableGrounded}, Volume: {stepAudioSource.volume:F2}");
        }
    }
    
    /// <summary>
    /// Manuel zemin kontrolü (fallback)
    /// </summary>
    private bool IsGroundedCheck() => stableGrounded; // Eski API'yi korumak için (geri uyum)
    
    /// <summary>
    /// Test kontrollerini işle
    /// </summary>
    private void HandleTestControls()
    {
        if (testRightFoot)
        {
            testRightFoot = false;
            AudioClip rightSound = rightFootSound != null ? rightFootSound : generalStepSound;
            if (rightSound != null)
            {
                PlayFootstepSound(rightSound, "Right (Test)");
            }
        }
        
        if (testLeftFoot)
        {
            testLeftFoot = false;
            AudioClip leftSound = leftFootSound != null ? leftFootSound : generalStepSound;
            if (leftSound != null)
            {
                PlayFootstepSound(leftSound, "Left (Test)");
            }
        }
    }
    
    /// <summary>
    /// Ayak sesi ayarlarını runtime'da güncelle
    /// </summary>
    public void UpdateFootstepSettings(float newVolume, float newMinPitch, float newMaxPitch)
    {
        stepVolume = Mathf.Clamp01(newVolume);
        minPitch = Mathf.Clamp(newMinPitch, 0.5f, 2f);
        maxPitch = Mathf.Clamp(newMaxPitch, minPitch, 2f);
        
        if (stepAudioSource != null)
        {
            stepAudioSource.volume = stepVolume;
        }
        
        Debug.Log($"🦶 Footstep settings updated: Volume={stepVolume:F2}, Pitch={minPitch:F2}-{maxPitch:F2}");
    }
    
    /// <summary>
    /// Hız eşiklerini runtime'da güncelle
    /// </summary>
    public void UpdateSpeedThresholds(float walkThreshold, float runThreshold)
    {
        walkSpeedThreshold = Mathf.Max(1f, walkThreshold);
        runSpeedThreshold = Mathf.Max(walkSpeedThreshold, runThreshold);
        
        Debug.Log($"🦶 Speed thresholds updated: Walk={walkSpeedThreshold:F1}, Run={runSpeedThreshold:F1}");
    }
    
    /// <summary>
    /// Mevcut hareket durumunu al (diğer scriptler için)
    /// </summary>
    public bool IsMoving() => isMoving;
    
    /// <summary>
    /// Mevcut oyuncu hızını al
    /// </summary>
    public float GetCurrentSpeed() => currentPlayerSpeed;
    
    /// <summary>
    /// Sıradaki ayak tipini al
    /// </summary>
    public string GetNextFootType() => isLeftFootNext ? "Left" : "Right";
    
    /// <summary>
    /// Debug için gizmos çiz
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // Zemin kontrol raycast'ini göster
        Vector3 rayStart = groundCheckObject != null ? groundCheckObject.position : transform.position;
    Gizmos.color = stableGrounded ? new Color(0.2f, 0.9f, 0.2f) : Color.red;
    Gizmos.DrawLine(rayStart + Vector3.up * groundProbeYOffset, rayStart + Vector3.up * groundProbeYOffset + Vector3.down * groundCheckDistance);
    // SphereCast gösterimi
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(rayStart + Vector3.up * groundProbeYOffset, groundProbeRadius);
        
        // GroundCheck objesi varsa onu da göster
        if (groundCheckObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheckObject.position, 0.1f);
        }
        
        // Hareket göstergesi
        if (isMoving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.2f);
        }
    }
}
