using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SoundManager - Oyunun genel tema müziklerini yönetir
/// Singleton pattern kullanarak tüm sahnelerde erişilebilir
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Header("Tema Müzikleri")]
    [Tooltip("Ana tema müziği (oyun başlangıcı için)")]
    public AudioClip mainTheme;
    [Tooltip("İkinci tema müziği (alternatif veya oyun içi)")]
    public AudioClip secondaryTheme;
    [Tooltip("Gelecekteki tema müzikleri için liste")]
    public List<AudioClip> additionalThemes = new List<AudioClip>();
    
    [Header("Müzik Ayarları")]
    [Tooltip("Müzik ses seviyesi (0-1 arası)")]
    [Range(0f, 1f)]
    public float musicVolume = 0.2f;
    [Tooltip("Müziği tamamen sustur (diğer sesleri test etmek için)")]
    public bool muteMusicForTesting = false;
    [Tooltip("Müzikler arasında geçiş süresi (saniye)")]
    [Range(0.5f, 5f)]
    public float fadeTransitionTime = 2f;
    [Tooltip("Oyun başlangıcında otomatik olarak ana temayı çal")]
    public bool autoPlayMainThemeOnStart = true;
    [Tooltip("Müzikler döngü halinde çalsın")]
    public bool loopMusic = true;
    [Tooltip("Sahne değişimlerinde müziği koru (DontDestroyOnLoad)")]
    public bool persistAcrossScenes = true;
    
    [Header("Sahne Geçiş Efektleri")]
    [Tooltip("Sahne değişikliğinde fade out/in efekti uygula")]
    public bool enableSceneTransitionFade = true;
    [Tooltip("Sahne çıkışında fade out süresi (saniye)")]
    [Range(0.5f, 5f)]
    public float sceneExitFadeDuration = 1.5f;
    [Tooltip("Yeni sahne de fade in süresi (saniye)")]
    [Range(0.5f, 5f)]
    public float sceneEnterFadeDuration = 1f;
    [Tooltip("Sahne geçişi sırasında müziği tamamen durdur")]
    public bool stopMusicDuringTransition = false;
    [Tooltip("Sahne geçişinde müzik değişikliği varsa cross-fade kullan")]
    public bool useCrossFadeForDifferentTracks = true;
    
    [Header("Sahne Yönetimi")]
    [Tooltip("Yeni sahne yüklendiğinde müzik durumu korunsun")]
    public bool maintainMusicOnSceneChange = true;
    [Tooltip("Belirli sahnelerde otomatik müzik başlat")]
    public bool autoStartMusicOnSceneLoad = true;
    [Tooltip("Ana menü sahne adı (müzik kontrolü için)")]
    public string mainMenuSceneName = "MainMenu";
    [Tooltip("Oyun sahne adı (müzik kontrolü için)")]
    public string gameSceneName = "GameScene";
    
    [Header("Çalma Durumu")]
    [Tooltip("Şu anda çalan müziğin adı (sadece görüntüleme için)")]
    [SerializeField] private string currentPlayingTrack = "None";
    [Tooltip("Müzik çalıyor mu? (sadece görüntüleme için)")]
    [SerializeField] private bool isPlaying = false;
    
    // Singleton instance
    public static SoundManager Instance { get; private set; }
    
    // Private components
    private AudioSource musicAudioSource;
    private Coroutine fadeCoroutine;
    
    // Şu anda çalan müzik bilgisi
    private AudioClip currentClip;
    private int currentThemeIndex = 0;
    
    // Sahne geçiş durumu
    private bool isInSceneTransition = false;
    private string pendingSceneName = "";
    private Coroutine sceneTransitionCoroutine;
    
    void Awake()
    {
        // Inspector'da manuel ayarlanmış yüksek volume değerlerini düzelt
        if (musicVolume > 0.5f)
        {
            Debug.Log($"🔄 SoundManager: Resetting high music volume {musicVolume:F2} to default 0.2");
            musicVolume = 0.2f;
        }
        
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            
            // Sahne değişimlerinde yok olmasın
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
            
            InitializeAudioSource();
            
            // SettingsManager'dan müzik ayarlarını yükle
            LoadMusicSettingsFromSettingsManager();
            
            // Sahne event'lerini dinle
            RegisterSceneEvents();
        }
        else
        {
            // Duplicate instance varsa yok et
            Debug.Log("Duplicate SoundManager found, destroying...");
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // Inspector'da eski yüksek volume değeri varsa düzelt
        if (musicVolume > 0.5f)
        {
            Debug.Log($"🔄 Overriding high inspector musicVolume {musicVolume:F2} to default 0.2");
            musicVolume = 0.2f;
            if (musicAudioSource != null)
            {
                musicAudioSource.volume = musicVolume;
            }
        }
        
        // SettingsManager'ın geç yüklenmesi durumu için kontrol
        EnsureSettingsLoaded();
        
        // Oyun başlangıcında ana temayı çal
        if (autoPlayMainThemeOnStart && mainTheme != null)
        {
            PlayMainTheme();
        }
    }
    
    void Update()
    {
        // Debug bilgilerini güncelle
        UpdateDebugInfo();
        
        // Mute toggle değişikliğini kontrol et (inspector'dan değişince anında uygula)
        if (musicAudioSource != null)
        {
            float expectedVolume = muteMusicForTesting ? 0f : musicVolume;
            if (Mathf.Abs(musicAudioSource.volume - expectedVolume) > 0.01f)
            {
                musicAudioSource.volume = expectedVolume;
                Debug.Log($"🔇 Mute toggle changed - Volume set to: {expectedVolume:F2} (Muted: {muteMusicForTesting})");
            }
        }
        
        // Müzik bittiğinde döngü kontrolü
        if (musicAudioSource != null && !musicAudioSource.isPlaying && currentClip != null && loopMusic)
        {
            // Müzik bittiyse ve döngü aktifse tekrar çal
            musicAudioSource.Play();
        }
        
        // SettingsManager bağlantısını periyodik kontrol et (her 60 frame'de bir)
        if (Time.frameCount % 60 == 0)
        {
            ValidateSettingsManagerConnection();
        }
    }
    
    /// <summary>
    /// SettingsManager bağlantısını kontrol et ve gerekirse yeniden bağla
    /// </summary>
    private void ValidateSettingsManagerConnection()
    {
        if (SettingsManager.Instance != null)
        {
            // SettingsManager varsa ama bağlantı kopmuşsa yeniden bağla
            try
            {
                SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
                SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to reconnect to SettingsManager: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// AudioSource component'ini initialize et
    /// </summary>
    private void InitializeAudioSource()
    {
        // AudioSource component'i ekle veya mevcut olanı al
        musicAudioSource = GetComponent<AudioSource>();
        if (musicAudioSource == null)
        {
            musicAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // AudioSource ayarlarını yapılandır
        float actualVolume = muteMusicForTesting ? 0f : musicVolume;
        musicAudioSource.volume = actualVolume;
        musicAudioSource.loop = loopMusic;
        musicAudioSource.playOnAwake = false;
        musicAudioSource.priority = 64; // Orta öncelik
        
        Debug.Log($"SoundManager AudioSource initialized! (Volume: {actualVolume:F2}, Muted: {muteMusicForTesting})");
    }
    
    /// <summary>
    /// Sahne event'lerini kaydet
    /// </summary>
    private void RegisterSceneEvents()
    {
        // Manuel sahne kontrol sistemini başlat
        StartCoroutine(MonitorSceneChanges());
        
        Debug.Log("🎵 Scene monitoring started for music transitions");
    }
    
    /// <summary>
    /// Sahne event'lerini temizle
    /// </summary>
    private void UnregisterSceneEvents()
    {
        // Coroutine zaten OnDestroy'da durduruluyor
    }
    
    /// <summary>
    /// Sahne değişikliklerini manuel olarak izle
    /// </summary>
    private IEnumerator MonitorSceneChanges()
    {
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string previousSceneName = currentSceneName;
        
        while (this != null)
        {
            currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            // Sahne değişimi algılandı
            if (currentSceneName != previousSceneName && !string.IsNullOrEmpty(previousSceneName))
            {
                Debug.Log($"🎵 Scene changed detected: {previousSceneName} → {currentSceneName}");
                
                if (enableSceneTransitionFade)
                {
                    // Sahne geçiş fade efektini başlat
                    yield return StartCoroutine(SceneTransitionFadeCoroutine(previousSceneName, currentSceneName));
                }
                else
                {
                    // Normal sahne değişimi işlemi
                    HandleSceneChange();
                }
                
                previousSceneName = currentSceneName;
            }
            
            yield return new WaitForSeconds(0.1f); // 100ms'de bir kontrol et
        }
    }
    
    /// <summary>
    /// Sahne geçiş fade efekti coroutine
    /// </summary>
    private IEnumerator SceneTransitionFadeCoroutine(string previousScene, string newScene)
    {
        if (musicAudioSource == null) yield break;
        
        isInSceneTransition = true;
        pendingSceneName = newScene;
        
        float originalVolume = musicAudioSource.volume;
        AudioClip originalClip = currentClip;
        
        // Yeni sahne için uygun müziği belirle
        AudioClip newClip = DetermineSceneMusic(newScene);
        bool musicShouldChange = (newClip != originalClip && newClip != null);
        
        Debug.Log($"🎵 Scene transition fade started: {previousScene} → {newScene}");
        Debug.Log($"🎵 Music change needed: {musicShouldChange} (Current: {originalClip?.name}, New: {newClip?.name})");
        
        // PHASE 1: Fade Out (çıkış sahnesi)
        if (originalClip != null && musicAudioSource.isPlaying)
        {
            float elapsed = 0f;
            while (elapsed < sceneExitFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / sceneExitFadeDuration;
                musicAudioSource.volume = Mathf.Lerp(originalVolume, 0f, t);
                yield return null;
            }
            
            musicAudioSource.volume = 0f;
            Debug.Log($"🎵 Scene exit fade out completed ({sceneExitFadeDuration:F1}s)");
        }
        
        // PHASE 2: Müzik değişikliği (gerekirse)
        if (musicShouldChange && newClip != null)
        {
            if (stopMusicDuringTransition)
            {
                musicAudioSource.Stop();
                yield return new WaitForSeconds(0.2f); // Kısa duraklama
            }
            
            // Yeni müziği ayarla
            musicAudioSource.clip = newClip;
            currentClip = newClip;
            UpdateCurrentTrackInfo(newScene);
            
            if (!musicAudioSource.isPlaying)
            {
                musicAudioSource.Play();
            }
            
            Debug.Log($"🎵 Music changed to: {newClip.name}");
        }
        else if (newClip == null || !musicShouldChange)
        {
            // Aynı müzik devam edecek, sadece volume'u sıfırla
            Debug.Log($"🎵 Continuing with same music: {originalClip?.name}");
        }
        
        // PHASE 3: Fade In (yeni sahne)
        if (musicAudioSource.isPlaying)
        {
            float elapsed = 0f;
            while (elapsed < sceneEnterFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / sceneEnterFadeDuration;
                musicAudioSource.volume = Mathf.Lerp(0f, musicVolume, t);
                yield return null;
            }
            
            musicAudioSource.volume = musicVolume;
            Debug.Log($"🎵 Scene enter fade in completed ({sceneEnterFadeDuration:F1}s)");
        }
        
        // Son işlemler
        isInSceneTransition = false;
        pendingSceneName = "";
        
        // Normal sahne değişimi işlemlerini çalıştır
        HandleSceneChange();
        
        Debug.Log($"🎵 Scene transition fade completed: {previousScene} → {newScene}");
    }
    
    /// <summary>
    /// Sahne için uygun müziği belirle
    /// </summary>
    private AudioClip DetermineSceneMusic(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return null;
        
        // Sahne tipine göre müzik seçimi
        if (sceneName.Contains("Menu") || sceneName == mainMenuSceneName)
        {
            return mainTheme;
        }
        else if (sceneName == gameSceneName || !sceneName.Contains("Menu"))
        {
            // Oyun sahnesi için ikinci tema varsa onu, yoksa ana temayı kullan
            return secondaryTheme != null ? secondaryTheme : mainTheme;
        }
        
        // Default olarak ana tema
        return mainTheme;
    }
    
    /// <summary>
    /// Mevcut çalan müzik bilgilerini güncelle
    /// </summary>
    private void UpdateCurrentTrackInfo(string sceneName)
    {
        if (currentClip == mainTheme)
        {
            currentPlayingTrack = "Main Theme";
            currentThemeIndex = 0;
        }
        else if (currentClip == secondaryTheme)
        {
            currentPlayingTrack = "Secondary Theme";
            currentThemeIndex = 1;
        }
        else if (additionalThemes != null)
        {
            for (int i = 0; i < additionalThemes.Count; i++)
            {
                if (currentClip == additionalThemes[i])
                {
                    currentPlayingTrack = $"Additional Theme {i + 1}";
                    currentThemeIndex = 2 + i;
                    break;
                }
            }
        }
        
        Debug.Log($"🎵 Track info updated: {currentPlayingTrack} for scene {sceneName}");
    }
    
    /// <summary>
    /// Ana tema müziğini çal
    /// </summary>
    public void PlayMainTheme()
    {
        if (mainTheme != null)
        {
            PlayMusic(mainTheme, "Main Theme");
            currentThemeIndex = 0;
        }
        else
        {
            Debug.LogWarning("Main Theme atanmamış!");
        }
    }
    
    /// <summary>
    /// İkinci tema müziğini çal
    /// </summary>
    public void PlaySecondaryTheme()
    {
        if (secondaryTheme != null)
        {
            PlayMusic(secondaryTheme, "Secondary Theme");
            currentThemeIndex = 1;
        }
        else
        {
            Debug.LogWarning("Secondary Theme atanmamış!");
        }
    }
    
    /// <summary>
    /// Belirtilen indexteki ek tema müziğini çal
    /// </summary>
    public void PlayAdditionalTheme(int index)
    {
        if (additionalThemes != null && index >= 0 && index < additionalThemes.Count)
        {
            if (additionalThemes[index] != null)
            {
                PlayMusic(additionalThemes[index], $"Additional Theme {index + 1}");
                currentThemeIndex = 2 + index;
            }
            else
            {
                Debug.LogWarning($"Additional Theme {index + 1} null!");
            }
        }
        else
        {
            Debug.LogWarning($"Geçersiz additional theme index: {index}");
        }
    }
    
    /// <summary>
    /// Müziği direkt çal (fade olmadan)
    /// </summary>
    private void PlayMusic(AudioClip clip, string trackName)
    {
        if (musicAudioSource == null || clip == null) return;
        
        // Önceki fade işlemini durdur
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        
        musicAudioSource.clip = clip;
        // Mute aktifse ses seviyesini 0 yap, yoksa normal volume kullan
        float actualVolume = muteMusicForTesting ? 0f : musicVolume;
        musicAudioSource.volume = actualVolume;
        musicAudioSource.Play();
        
        currentClip = clip;
        currentPlayingTrack = trackName;
        
        Debug.Log($"Playing: {trackName} (Volume: {actualVolume:F2}, Muted: {muteMusicForTesting})");
    }
    
    /// <summary>
    /// Müziği fade ile değiştir
    /// </summary>
    public void PlayMusicWithFade(AudioClip newClip, string trackName)
    {
        if (newClip == null) return;
        
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        fadeCoroutine = StartCoroutine(FadeToNewMusic(newClip, trackName));
    }
    
    /// <summary>
    /// Ana tema müziğini fade ile çal
    /// </summary>
    public void PlayMainThemeWithFade()
    {
        if (mainTheme != null)
        {
            PlayMusicWithFade(mainTheme, "Main Theme");
            currentThemeIndex = 0;
        }
    }
    
    /// <summary>
    /// İkinci tema müziğini fade ile çal
    /// </summary>
    public void PlaySecondaryThemeWithFade()
    {
        if (secondaryTheme != null)
        {
            PlayMusicWithFade(secondaryTheme, "Secondary Theme");
            currentThemeIndex = 1;
        }
    }
    
    /// <summary>
    /// Fade transition coroutine
    /// </summary>
    private IEnumerator FadeToNewMusic(AudioClip newClip, string trackName)
    {
        float startVolume = musicAudioSource.volume;
        
        // Fade out current music
        float elapsed = 0f;
        while (elapsed < fadeTransitionTime / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (fadeTransitionTime / 2f);
            musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        
        // Change the clip
        musicAudioSource.clip = newClip;
        musicAudioSource.Play();
        currentClip = newClip;
        currentPlayingTrack = trackName;
        
        // Fade in new music
        elapsed = 0f;
        while (elapsed < fadeTransitionTime / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (fadeTransitionTime / 2f);
            musicAudioSource.volume = Mathf.Lerp(0f, musicVolume, t);
            yield return null;
        }
        
        musicAudioSource.volume = musicVolume;
        fadeCoroutine = null;
        
        Debug.Log($"Faded to: {trackName}");
    }
    
    /// <summary>
    /// Müziği durdur
    /// </summary>
    public void StopMusic()
    {
        if (musicAudioSource != null)
        {
            musicAudioSource.Stop();
            currentClip = null;
            currentPlayingTrack = "None";
        }
    }
    
    /// <summary>
    /// Müziği fade ile durdur
    /// </summary>
    public void StopMusicWithFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        fadeCoroutine = StartCoroutine(FadeOutAndStop());
    }
    
    /// <summary>
    /// Fade out and stop coroutine
    /// </summary>
    private IEnumerator FadeOutAndStop()
    {
        float startVolume = musicAudioSource.volume;
        float elapsed = 0f;
        
        while (elapsed < fadeTransitionTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTransitionTime;
            musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        
        StopMusic();
        musicAudioSource.volume = musicVolume; // Reset volume for next play
        fadeCoroutine = null;
        
        Debug.Log("Music faded out and stopped");
    }
    
    /// <summary>
    /// Müzik ses seviyesini ayarla
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        float previousVolume = musicVolume;
        musicVolume = Mathf.Clamp01(volume);
        if (musicAudioSource != null)
        {
            // Mute aktifse ses seviyesini 0 yap, yoksa normal volume kullan
            float actualVolume = muteMusicForTesting ? 0f : musicVolume;
            musicAudioSource.volume = actualVolume;
        }
        Debug.Log($"🎵 Music volume changed: {previousVolume:F2} → {musicVolume:F2} (Muted: {muteMusicForTesting})");
    }
    
    /// <summary>
    /// SettingsManager'dan müzik ayarlarını yükle
    /// </summary>
    private void LoadMusicSettingsFromSettingsManager()
    {
        if (SettingsManager.Instance != null)
        {
            var settings = SettingsManager.Instance.Current;
            SetMusicVolume(settings.musicVolume);
            Debug.Log($"🎵 Music settings loaded from SettingsManager: Volume={settings.musicVolume:F2}");
            
            // SettingsManager'dan değişiklikleri dinle
            SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
        }
        else
        {
            // SettingsManager henüz yüklenmemişse default değerleri kullan
            SetMusicVolume(0.2f);
            Debug.LogWarning("SettingsManager not available, using default music volume 0.2");
        }
    }
    
    /// <summary>
    /// SettingsManager'da ayarlar değiştiğinde çağrılır
    /// </summary>
    private void OnSettingsChanged(SettingsManager.GameSettings settings)
    {
        SetMusicVolume(settings.musicVolume);
        Debug.Log($"🎵 Music volume updated from settings: {settings.musicVolume:F2}");
    }
    
    /// <summary>
    /// Start metodunda geç yükleme kontrolü
    /// </summary>
    private void EnsureSettingsLoaded()
    {
        // Eğer Awake'de SettingsManager yoksa, Start'ta tekrar dene
        if (SettingsManager.Instance != null)
        {
            // SettingsManager'a listener ekle (eğer henüz eklenmemişse)
            SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged; // Duplicate prevention
            SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
            
            // Mevcut ayarları uygula
            var settings = SettingsManager.Instance.Current;
            SetMusicVolume(settings.musicVolume);
            Debug.Log($"🎵 Late-loaded music settings: Volume={settings.musicVolume:F2}");
        }
        else
        {
            // SettingsManager hala yoksa 0.2 kullan
            SetMusicVolume(0.2f);
            Debug.Log($"🎵 SettingsManager still not available, using default 0.2");
        }
    }
    
    /// <summary>
    /// Sahne değişiminde müzik durumunu yönet
    /// </summary>
    private void HandleSceneChange()
    {
        if (!autoStartMusicOnSceneLoad) return;
        
        // Eğer sahne geçişi sırasındaysak, işlemi atla
        if (isInSceneTransition)
        {
            Debug.Log("🎵 Skipping HandleSceneChange - transition in progress");
            return;
        }
        
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Eğer müzik çalmıyorsa ve maintainMusicOnSceneChange aktifse müzik başlat
        if (!IsMusicPlaying() && maintainMusicOnSceneChange)
        {
            // Sahne tipine göre uygun müziği başlat
            if (currentSceneName.Contains("Menu") || currentSceneName == mainMenuSceneName)
            {
                if (mainTheme != null)
                {
                    PlayMainTheme();
                    Debug.Log($"🎵 Auto-started main theme for menu scene: {currentSceneName}");
                }
            }
            else if (currentSceneName == gameSceneName || !currentSceneName.Contains("Menu"))
            {
                // Oyun sahnesi için ikinci tema veya ana tema
                if (secondaryTheme != null)
                {
                    PlaySecondaryTheme();
                    Debug.Log($"🎵 Auto-started secondary theme for game scene: {currentSceneName}");
                }
                else if (mainTheme != null)
                {
                    PlayMainTheme();
                    Debug.Log($"🎵 Auto-started main theme for game scene: {currentSceneName}");
                }
            }
        }
        else if (IsMusicPlaying())
        {
            Debug.Log($"🎵 Music already playing, maintaining across scene: {currentSceneName}");
        }
    }
    
    /// <summary>
    /// Manuel olarak sahne değişimi tetikleme (başka scriptlerden çağrılabilir)
    /// </summary>
    public void OnSceneChanged()
    {
        HandleSceneChange();
    }
    
    /// <summary>
    /// Manuel sahne geçiş fade'i tetikleme (özel durumlar için)
    /// </summary>
    public void TriggerSceneTransitionFade(string previousScene, string newScene)
    {
        if (enableSceneTransitionFade)
        {
            StartCoroutine(SceneTransitionFadeCoroutine(previousScene, newScene));
        }
    }
    
    /// <summary>
    /// Sahne geçiş durumunu kontrol et
    /// </summary>
    public bool IsInSceneTransition()
    {
        return isInSceneTransition;
    }
    
    /// <summary>
    /// Sahne geçiş ayarlarını runtime'da değiştir
    /// </summary>
    public void SetSceneTransitionSettings(bool enableFade, float exitDuration, float enterDuration)
    {
        enableSceneTransitionFade = enableFade;
        sceneExitFadeDuration = Mathf.Clamp(exitDuration, 0.1f, 10f);
        sceneEnterFadeDuration = Mathf.Clamp(enterDuration, 0.1f, 10f);
        
        Debug.Log($"🎵 Scene transition settings updated: Enable={enableFade}, Exit={exitDuration:F1}s, Enter={enterDuration:F1}s");
    }
    
    /// <summary>
    /// SoundManager'ı yeniden başlat (debugging için)
    /// </summary>
    public void RestartSoundManager()
    {
        LoadMusicSettingsFromSettingsManager();
        EnsureSettingsLoaded();
        
        if (autoPlayMainThemeOnStart && !IsMusicPlaying() && mainTheme != null)
        {
            PlayMainTheme();
        }
        
        Debug.Log("🎵 SoundManager restarted!");
    }
    
    /// <summary>
    /// Mevcut müzik ayarlarını logla (debugging için)
    /// </summary>
    public void LogCurrentMusicState()
    {
        Debug.Log($"🎵 === SoundManager State ===");
        Debug.Log($"🎵 Current Track: {currentPlayingTrack}");
        Debug.Log($"🎵 Is Playing: {IsMusicPlaying()}");
        Debug.Log($"🎵 Music Volume: {musicVolume:F2}");
        Debug.Log($"🎵 Current Theme Index: {currentThemeIndex}");
        Debug.Log($"🎵 AudioSource Volume: {(musicAudioSource != null ? musicAudioSource.volume.ToString("F2") : "null")}");
        Debug.Log($"🎵 SettingsManager Connected: {(SettingsManager.Instance != null)}");
        Debug.Log($"🎵 === End State ===");
    }

    void OnDestroy()
    {
        // SettingsManager listener'ını temizle
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
        }
        
        // Sahne event'lerini temizle
        UnregisterSceneEvents();
        
        // Coroutine'leri durdur
        if (sceneTransitionCoroutine != null)
        {
            StopCoroutine(sceneTransitionCoroutine);
        }
        
        Debug.Log("🎵 SoundManager destroyed and cleaned up");
    }
    
    /// <summary>
    /// Müzik çalıyor mu kontrol et
    /// </summary>
    public bool IsMusicPlaying()
    {
        return musicAudioSource != null && musicAudioSource.isPlaying;
    }
    
    /// <summary>
    /// Şu anda çalan müziğin adını al
    /// </summary>
    public string GetCurrentTrackName()
    {
        return currentPlayingTrack;
    }
    
    /// <summary>
    /// Sıradaki tema müziğine geç
    /// </summary>
    public void NextTheme()
    {
        int totalThemes = 2 + (additionalThemes != null ? additionalThemes.Count : 0);
        
        if (totalThemes <= 1) return;
        
        currentThemeIndex = (currentThemeIndex + 1) % totalThemes;
        
        switch (currentThemeIndex)
        {
            case 0:
                PlayMainThemeWithFade();
                break;
            case 1:
                PlaySecondaryThemeWithFade();
                break;
            default:
                int additionalIndex = currentThemeIndex - 2;
                if (additionalThemes != null && additionalIndex < additionalThemes.Count)
                {
                    PlayMusicWithFade(additionalThemes[additionalIndex], $"Additional Theme {additionalIndex + 1}");
                }
                break;
        }
    }
    
    /// <summary>
    /// Önceki tema müziğine geç
    /// </summary>
    public void PreviousTheme()
    {
        int totalThemes = 2 + (additionalThemes != null ? additionalThemes.Count : 0);
        
        if (totalThemes <= 1) return;
        
        currentThemeIndex--;
        if (currentThemeIndex < 0)
        {
            currentThemeIndex = totalThemes - 1;
        }
        
        switch (currentThemeIndex)
        {
            case 0:
                PlayMainThemeWithFade();
                break;
            case 1:
                PlaySecondaryThemeWithFade();
                break;
            default:
                int additionalIndex = currentThemeIndex - 2;
                if (additionalThemes != null && additionalIndex < additionalThemes.Count)
                {
                    PlayMusicWithFade(additionalThemes[additionalIndex], $"Additional Theme {additionalIndex + 1}");
                }
                break;
        }
    }
    
    /// <summary>
    /// Debug bilgilerini güncelle
    /// </summary>
    private void UpdateDebugInfo()
    {
        if (musicAudioSource != null)
        {
            isPlaying = musicAudioSource.isPlaying;
            
            if (!isPlaying && currentClip != null)
            {
                currentPlayingTrack = "Stopped";
            }
        }
        else
        {
            isPlaying = false;
            currentPlayingTrack = "No AudioSource";
        }
    }
    
    /// <summary>
    /// Tema müziklerini test etmek için public metodlar
    /// </summary>
    [System.Serializable]
    public class DebugControls
    {
        [Header("Test Controls (Runtime Only)")]
        public bool playMainTheme;
        public bool playSecondaryTheme;
        public bool nextTheme;
        public bool previousTheme;
        public bool stopMusic;
        public bool toggleMute; // Inspector'dan mute toggle'ı
        
        [Space]
        [Header("Scene Transition Tests")]
        public bool testSceneTransition;
        public string testPreviousScene = "MainMenu";
        public string testNewScene = "GameScene";
        
        [Space]
        [Range(0f, 1f)]
        public float testVolume = 0.2f;
        
        [Space]
        [Header("Transition Settings Test")]
        public bool updateTransitionSettings;
        [Range(0.1f, 5f)]
        public float testExitDuration = 1.5f;
        [Range(0.1f, 5f)]
        public float testEnterDuration = 1f;
    }
    
    [Header("Debug & Test")]
    public DebugControls debugControls;
    
    /// <summary>
    /// Editor'da test kontrollerini işle
    /// </summary>
    void OnValidate()
    {
        if (Application.isPlaying && debugControls != null)
        {
            if (debugControls.playMainTheme)
            {
                debugControls.playMainTheme = false;
                PlayMainTheme();
            }
            
            if (debugControls.playSecondaryTheme)
            {
                debugControls.playSecondaryTheme = false;
                PlaySecondaryTheme();
            }
            
            if (debugControls.nextTheme)
            {
                debugControls.nextTheme = false;
                NextTheme();
            }
            
            if (debugControls.previousTheme)
            {
                debugControls.previousTheme = false;
                PreviousTheme();
            }
            
            if (debugControls.stopMusic)
            {
                debugControls.stopMusic = false;
                StopMusicWithFade();
            }
            
            if (debugControls.toggleMute)
            {
                debugControls.toggleMute = false;
                muteMusicForTesting = !muteMusicForTesting;
                // Volume'u anında güncelle
                if (musicAudioSource != null)
                {
                    float actualVolume = muteMusicForTesting ? 0f : musicVolume;
                    musicAudioSource.volume = actualVolume;
                    Debug.Log($"🔇 Mute toggled: {muteMusicForTesting} (Volume: {actualVolume:F2})");
                }
            }
            
            if (debugControls.testSceneTransition)
            {
                debugControls.testSceneTransition = false;
                TriggerSceneTransitionFade(debugControls.testPreviousScene, debugControls.testNewScene);
            }
            
            if (debugControls.updateTransitionSettings)
            {
                debugControls.updateTransitionSettings = false;
                SetSceneTransitionSettings(enableSceneTransitionFade, debugControls.testExitDuration, debugControls.testEnterDuration);
            }
            
            SetMusicVolume(debugControls.testVolume);
        }
    }
}
