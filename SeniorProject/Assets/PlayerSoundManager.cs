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
    [SerializeField] private bool isGrounded = true;           // Yerde mi?
    [SerializeField] private float currentPlayerSpeed = 0f;   // Mevcut oyuncu hızı
    
    // Private References
    private PlayerMovement playerMovement;
    private CharacterController characterController;
    private Vector3 lastPosition;
    private float lastStepTime = 0f;
    
    void Start()
    {
        InitializeComponents();
        InitializeAudioSource();
        InitializeVolumeSettings();
        
        // Başlangıç pozisyonunu kaydet
        lastPosition = transform.position;
        lastStepTime = Time.time;
        
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
        // PlayerMovement'tan hız bilgisini al
        if (playerMovement != null)
        {
            currentPlayerSpeed = playerMovement.CurrentSpeed();
            isGrounded = playerMovement.IsGrounded();
        }
        else
        {
            // Fallback: Manuel hareket kontrolü
            Vector3 currentPosition = transform.position;
            Vector3 horizontalMovement = new Vector3(currentPosition.x, 0, currentPosition.z) - 
                                       new Vector3(lastPosition.x, 0, lastPosition.z);
            currentPlayerSpeed = horizontalMovement.magnitude / Time.deltaTime;
            lastPosition = currentPosition;
            
            // Manuel zemin kontrolü
            isGrounded = IsGroundedCheck();
        }
        
        // Hareket ediyor mu kontrolü
        isMoving = currentPlayerSpeed > 0.1f;
        
        // Hıza göre adım aralığını hesapla
        currentStepInterval = CalculateStepInterval(currentPlayerSpeed);
        
        // Son adımdan beri geçen süreyi güncelle
        timeSinceLastStep = Time.time - lastStepTime;
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
        // Footstep çalma koşulları:
        // 1. Hareket ediyor
        // 2. Yerde
        // 3. Yeterli süre geçmiş
        bool shouldPlayFootstep = isMoving && isGrounded && timeSinceLastStep >= currentStepInterval;
        
        if (shouldPlayFootstep)
        {
            PlayFootstep();
            lastStepTime = Time.time;
            timeSinceLastStep = 0f;
            
            // Ayak sırasını değiştir
            isLeftFootNext = !isLeftFootNext;
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
            Debug.Log($"🦶 {footType} footstep - Speed: {currentPlayerSpeed:F1}, Interval: {currentStepInterval:F2}s, Pitch: {randomPitch:F2}, Volume: {stepAudioSource.volume:F2}");
        }
    }
    
    /// <summary>
    /// Manuel zemin kontrolü (fallback)
    /// </summary>
    private bool IsGroundedCheck()
    {
        // CharacterController varsa onun kontrolünü kullan
        if (characterController != null)
        {
            return characterController.isGrounded;
        }
        
        // GroundCheck objesi varsa raycast kullan
        if (groundCheckObject != null)
        {
            return Physics.Raycast(groundCheckObject.position, Vector3.down, groundCheckDistance, groundLayerMask);
        }
        
        // Fallback: Player pozisyonundan raycast
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayerMask);
    }
    
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
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(rayStart, rayStart + Vector3.down * groundCheckDistance);
        
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
