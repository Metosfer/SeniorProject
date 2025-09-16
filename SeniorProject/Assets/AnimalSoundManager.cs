using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AnimalSoundManager - Hayvan seslerini 3D spatial audio ile yönetir
/// Oyuncuya yakınlık bazlı ses seviyesi kontrolü
/// </summary>
public class AnimalSoundManager : MonoBehaviour
{
    [Header("Hayvan Ses Ayarları")]
    [Tooltip("Bu hayvanın ses klipleri listesi")]
    public List<AudioClip> animalSounds = new List<AudioClip>();
    [Tooltip("Hayvan türü tanımlaması (debug için)")]
    public string animalType = "Generic Animal";
    
    [Header("3D Ses Ayarları")]
    [Tooltip("Maksimum ses seviyesi (0-1 arası)")]
    [Range(0f, 1f)]
    public float maxVolume = 0.8f;
    [Tooltip("Minimum ses seviyesi (0-1 arası)")]
    [Range(0f, 1f)]
    public float minVolume = 0.1f;
    [Tooltip("Ses duyulma mesafesi (Unity units)")]
    [Range(1f, 50f)]
    public float hearingDistance = 15f;
    [Tooltip("Maksimum ses mesafesi (tam ses seviyesi)")]
    [Range(0.5f, 10f)]
    public float maxVolumeDistance = 3f;
    
    [Header("Ses Çalma Ayarları")]
    [Tooltip("Sesler arası minimum bekleme süresi (saniye)")]
    [Range(2f, 30f)]
    public float minSoundInterval = 5f;
    [Tooltip("Sesler arası maksimum bekleme süresi (saniye)")]
    [Range(5f, 60f)]
    public float maxSoundInterval = 15f;
    [Tooltip("Oyuncu yokken ses çalsın mı?")]
    public bool playWithoutPlayer = false;
    [Tooltip("Pitch varyasyonu (ses tonları çeşitliliği)")]
    [Range(0f, 0.5f)]
    public float pitchVariation = 0.2f;
    
    [Header("Debug & Test")]
    [Tooltip("Inspector'dan manuel ses çalma (test için)")]
    public bool playTestSound = false;
    [Tooltip("Gizmos ile mesafe alanlarını göster")]
    public bool showDebugGizmos = true;
    [Tooltip("Mesafe bilgilerini console'a yazdır")]
    public bool debugDistanceInfo = false;
    
    [Header("Durum Bilgileri (Sadece Görüntüleme)")]
    [SerializeField] private float currentPlayerDistance = 0f;
    [SerializeField] private float currentVolume = 0f;
    [SerializeField] private bool isPlayerInRange = false;
    [SerializeField] private float nextSoundTime = 0f;
    
    // Private Components
    private AudioSource audioSource;
    private Transform playerTransform;
    private Coroutine soundLoopCoroutine;
    
    // Sound timing
    private float lastSoundTime = 0f;
    
    void Start()
    {
        InitializeAudioSource();
        InitializeVolumeSettings();
        FindPlayerTransform();
        StartSoundLoop();
        
        // İlk ses için rastgele gecikme
        nextSoundTime = Time.time + Random.Range(1f, 5f);
        
        Debug.Log($"🐾 {animalType} AnimalSoundManager initialized with {animalSounds.Count} sounds");
    }
    
    void Update()
    {
        UpdatePlayerDistance();
        UpdateVolumeBasedOnDistance();
        HandleTestControls();
    }
    
    /// <summary>
    /// AudioSource component'ini initialize et
    /// </summary>
    private void InitializeAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // 3D Spatial Audio ayarları
        audioSource.spatialBlend = 1f;           // 3D ses (1 = tam 3D, 0 = 2D)
        audioSource.rolloffMode = AudioRolloffMode.Linear; // Mesafe ile lineer azalma
        audioSource.minDistance = maxVolumeDistance;       // Tam ses mesafesi
        audioSource.maxDistance = hearingDistance;         // Ses duyulmama mesafesi
        audioSource.playOnAwake = false;
        audioSource.loop = false;               // Manuel loop kontrolü
        audioSource.priority = 128;             // Orta öncelik
        audioSource.volume = maxVolume;
        
        Debug.Log($"🔊 {animalType} AudioSource configured for 3D spatial audio");
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
            ApplyMasterVolume(settings.masterVolume);
            
            // Settings değişikliklerini dinle
            SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
            
            Debug.Log($"🔊 {animalType} volume initialized from SettingsManager: {settings.masterVolume:F2}");
        }
        else
        {
            // Fallback: PlayerPrefs'ten yükle
            float savedVolume = PlayerPrefs.GetFloat("Volume", 1f);
            ApplyMasterVolume(savedVolume);
            
            Debug.LogWarning($"⚠️ {animalType}: SettingsManager not found, using PlayerPrefs fallback");
        }
    }
    
    /// <summary>
    /// SettingsManager'dan ayar değişikliği geldiğinde çağrılır
    /// </summary>
    private void OnSettingsChanged(SettingsManager.GameSettings settings)
    {
        ApplyMasterVolume(settings.masterVolume);
        Debug.Log($"🔊 {animalType} volume updated: {settings.masterVolume:F2}");
    }
    
    /// <summary>
    /// Master volume'u mevcut maxVolume ile çarparak uygula
    /// </summary>
    private void ApplyMasterVolume(float masterVolume)
    {
        if (audioSource != null)
        {
            // maxVolume ve masterVolume'u çarp, sonucu mesafe hesaplamasında kullan
            float finalMaxVolume = maxVolume * masterVolume;
            
            // Eğer mesafe bazlı volume hesabı yapılıyorsa onu da güncelle
            UpdateVolumeBasedOnDistance();
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
        
        StopSoundLoop();
        Debug.Log($"🐾 {animalType} AnimalSoundManager destroyed");
    }
    
    /// <summary>
    /// Player transform'unu bul
    /// </summary>
    private void FindPlayerTransform()
    {
        // Player tag'i ile oyuncuyu bul
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            Debug.Log($"🎯 {animalType} found player: {player.name}");
        }
        else
        {
            Debug.LogWarning($"⚠️ {animalType} could not find Player! Make sure player has 'Player' tag.");
        }
    }
    
    /// <summary>
    /// Oyuncu mesafesini güncelle
    /// </summary>
    private void UpdatePlayerDistance()
    {
        if (playerTransform == null) 
        {
            currentPlayerDistance = float.MaxValue;
            isPlayerInRange = false;
            return;
        }
        
        currentPlayerDistance = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerInRange = currentPlayerDistance <= hearingDistance;
        
        if (debugDistanceInfo && Time.frameCount % 60 == 0) // Her saniyede bir log
        {
            Debug.Log($"🐾 {animalType} - Player distance: {currentPlayerDistance:F1}m, In range: {isPlayerInRange}");
        }
    }
    
    /// <summary>
    /// Mesafeye göre ses seviyesini güncelle
    /// </summary>
    private void UpdateVolumeBasedOnDistance()
    {
        if (audioSource == null || playerTransform == null) return;
        
        // SettingsManager'dan master volume al
        float masterVolume = 1f;
        if (SettingsManager.Instance != null)
        {
            masterVolume = SettingsManager.Instance.Current.masterVolume;
        }
        else
        {
            masterVolume = PlayerPrefs.GetFloat("Volume", 1f);
        }
        
        // Mesafe bazlı volume hesaplama
        float volumeMultiplier = 1f;
        
        if (currentPlayerDistance <= maxVolumeDistance)
        {
            // Çok yakın - maksimum ses
            volumeMultiplier = 1f;
        }
        else if (currentPlayerDistance >= hearingDistance)
        {
            // Çok uzak - minimum ses
            volumeMultiplier = 0f;
        }
        else
        {
            // Aradaki mesafe - lineer interpolasyon
            float distanceRatio = (currentPlayerDistance - maxVolumeDistance) / (hearingDistance - maxVolumeDistance);
            volumeMultiplier = Mathf.Lerp(1f, 0f, distanceRatio);
        }
        
        // Final volume hesaplama (maxVolume, masterVolume ve mesafe ile)
        float baseVolume = Mathf.Lerp(minVolume, maxVolume, volumeMultiplier);
        currentVolume = baseVolume * masterVolume;
        
        // AudioSource'a uygula (sadece çalarken)
        if (audioSource.isPlaying)
        {
            audioSource.volume = currentVolume;
        }
    }
    
    /// <summary>
    /// Ses döngüsünü başlat
    /// </summary>
    private void StartSoundLoop()
    {
        if (soundLoopCoroutine != null)
        {
            StopCoroutine(soundLoopCoroutine);
        }
        
        soundLoopCoroutine = StartCoroutine(SoundLoopCoroutine());
    }
    
    /// <summary>
    /// Ses döngüsü coroutine
    /// </summary>
    private IEnumerator SoundLoopCoroutine()
    {
        while (true)
        {
            // Oyuncu range'de mi veya oyuncu olmadan çalsın mı kontrol et
            bool shouldPlay = isPlayerInRange || playWithoutPlayer;
            
            if (shouldPlay && animalSounds.Count > 0 && Time.time >= nextSoundTime)
            {
                PlayRandomSound();
                
                // Bir sonraki ses için rastgele bekleme süresi
                float nextInterval = Random.Range(minSoundInterval, maxSoundInterval);
                nextSoundTime = Time.time + nextInterval;
                
                Debug.Log($"🐾 {animalType} played sound. Next in {nextInterval:F1}s");
            }
            
            // Kısa aralıklarla kontrol et
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    /// <summary>
    /// Rastgele hayvan sesi çal
    /// </summary>
    public void PlayRandomSound()
    {
        if (animalSounds.Count == 0 || audioSource == null)
        {
            Debug.LogWarning($"⚠️ {animalType} has no sounds to play!");
            return;
        }
        
        // Rastgele ses seç
        AudioClip randomSound = animalSounds[Random.Range(0, animalSounds.Count)];
        
        // Pitch varyasyonu ekle
        float basePitch = 1f;
        float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = basePitch + pitchOffset;
        
        // SettingsManager'dan master volume al ve uygula
        float masterVolume = 1f;
        if (SettingsManager.Instance != null)
        {
            masterVolume = SettingsManager.Instance.Current.masterVolume;
        }
        else
        {
            masterVolume = PlayerPrefs.GetFloat("Volume", 1f);
        }
        
        // Mesafe bazlı volume hesaplama ile master volume'u çarp
        UpdateVolumeBasedOnDistance(); // currentVolume'u günceller
        
        // Ses seviyesini ayarla ve çal
        audioSource.volume = currentVolume; // currentVolume zaten master volume ile çarpılmış
        audioSource.clip = randomSound;
        audioSource.Play();
        
        lastSoundTime = Time.time;
        
        Debug.Log($"🔊 {animalType} playing: {randomSound.name} (Pitch: {audioSource.pitch:F2}, Volume: {currentVolume:F2}, Master: {masterVolume:F2}, Distance: {currentPlayerDistance:F1}m)");
    }
    
    /// <summary>
    /// Belirli ses çal (index ile)
    /// </summary>
    public void PlaySpecificSound(int soundIndex)
    {
        if (soundIndex < 0 || soundIndex >= animalSounds.Count)
        {
            Debug.LogWarning($"⚠️ {animalType} invalid sound index: {soundIndex}");
            return;
        }
        
        AudioClip sound = animalSounds[soundIndex];
        
        // Pitch varyasyonu ekle
        float basePitch = 1f;
        float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = basePitch + pitchOffset;
        
        // Mesafe bazlı volume hesaplama ile master volume'u çarp
        UpdateVolumeBasedOnDistance(); // currentVolume'u günceller
        
        // Ses seviyesini ayarla ve çal
        audioSource.volume = currentVolume; // currentVolume zaten master volume ile çarpılmış
        audioSource.clip = sound;
        audioSource.Play();
        
        Debug.Log($"🔊 {animalType} playing specific sound: {sound.name} (Index: {soundIndex}, Volume: {currentVolume:F2})");
    }
    
    /// <summary>
    /// Ses döngüsünü durdur
    /// </summary>
    public void StopSoundLoop()
    {
        if (soundLoopCoroutine != null)
        {
            StopCoroutine(soundLoopCoroutine);
            soundLoopCoroutine = null;
        }
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        Debug.Log($"🛑 {animalType} sound loop stopped");
    }
    
    /// <summary>
    /// Ses döngüsünü yeniden başlat
    /// </summary>
    public void RestartSoundLoop()
    {
        StopSoundLoop();
        StartSoundLoop();
        Debug.Log($"🔄 {animalType} sound loop restarted");
    }
    
    /// <summary>
    /// Test kontrollerini işle
    /// </summary>
    private void HandleTestControls()
    {
        if (playTestSound)
        {
            playTestSound = false; // Reset button
            PlayRandomSound();
        }
    }
    
    /// <summary>
    /// Ses ayarlarını runtime'da güncelle
    /// </summary>
    public void UpdateSoundSettings(float newMaxVolume, float newHearingDistance, float newMaxVolumeDistance)
    {
        maxVolume = Mathf.Clamp01(newMaxVolume);
        hearingDistance = Mathf.Max(1f, newHearingDistance);
        maxVolumeDistance = Mathf.Clamp(newMaxVolumeDistance, 0.5f, hearingDistance);
        
        // AudioSource ayarlarını güncelle
        if (audioSource != null)
        {
            audioSource.minDistance = maxVolumeDistance;
            audioSource.maxDistance = hearingDistance;
        }
        
        Debug.Log($"🔧 {animalType} sound settings updated: MaxVol={maxVolume:F2}, HearDist={hearingDistance:F1}, MaxVolDist={maxVolumeDistance:F1}");
    }
    
    /// <summary>
    /// Oyuncu range'de mi kontrol et
    /// </summary>
    public bool IsPlayerInRange()
    {
        return isPlayerInRange;
    }
    
    /// <summary>
    /// Mevcut oyuncu mesafesini al
    /// </summary>
    public float GetPlayerDistance()
    {
        return currentPlayerDistance;
    }
    
    /// <summary>
    /// Gizmos ile debug alanlarını göster
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Maksimum ses alanı (yeşil)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxVolumeDistance);
        
        // Duyma alanı (kırmızı)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hearingDistance);
        
        // Oyuncu mesafesi çizgisi (mavi)
        if (playerTransform != null)
        {
            Gizmos.color = isPlayerInRange ? Color.blue : Color.gray;
            Gizmos.DrawLine(transform.position, playerTransform.position);
            
            // Oyuncu pozisyonu (sarı nokta)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerTransform.position, 0.5f);
        }
        
        // Hayvan pozisyonu (beyaz küp)
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}
