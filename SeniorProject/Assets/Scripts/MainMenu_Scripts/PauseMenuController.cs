using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using System;

public partial class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuPanel;           // Duraklatma menüsü paneli
    public GameObject confirmationDialogPanel;  // Onay diyaloğu paneli
    public TextMeshProUGUI confirmationText;    // Onay diyaloğu metni
    public Button saveAndProceedButton;         // "Save and Proceed" düğmesi
    public Button proceedWithoutSavingButton;   // "Proceed without Saving" düğmesi
    public Button cancelButton;                 // "Cancel" düğmesi
    public Button resumeButton;                 // "Resume" düğmesi
    public Button saveButton;                   // "Save" düğmesi
    public Button settingsButton;               // "Settings" düğmesi
    public Button mainMenuButton;               // "Main Menu" düğmesi
    public Button quitButton;                   // "Quit" düğmesi
    public TextMeshProUGUI saveNotificationText;// Save bildirimi metni
    public GameObject settingsPanel;            // Settings paneli
    public Slider volumeSlider;                 // Master ses ayarı slider'ı
    public Slider musicVolumeSlider;            // Müzik ses ayarı slider'ı
    public Slider soundEffectsVolumeSlider;     // Sound effects ses ayarı slider'ı (footstep vs.)
    public Toggle minimapToggle;                // Minimap açık/kapalı toggle'ı
    public Toggle musicMuteToggle;              // Müzik susturma toggle'ı
    public GameObject minimapCanvas;            // Minimap Canvas'ı (direkt assign için)
    public Dropdown graphicsDropdown;           // Grafik ayarı dropdown'ı
    public Button settingsBackButton;           // Settings panel geri butonu
    public Transform playerTransform;           // Oyuncu pozisyonu için referans

    private bool isPaused = false;              // Oyun duraklatıldı mı?
    private string pendingAction;               // Bekleyen eylem (MainMenu veya Quit)
    private bool wasPauseMenuOpen = false;      // PauseMenu açık mıydı?

    [Header("Interaction Blocking")]
    [Tooltip("Pause menü açıkken sahnede tıklanabilir/etkileşimli scriptleri otomatik olarak devre dışı bırak")] 
    public bool autoBlockInteractions = true;
    [Tooltip("Ek olarak devre dışı bırakılmasını istediğiniz bileşenler (isteğe bağlı)")] 
    public List<Behaviour> additionalBehavioursToBlock = new List<Behaviour>();
    private readonly HashSet<Behaviour> _blockedBehaviours = new HashSet<Behaviour>();
    public static bool IsPausedGlobally { get; private set; }

    void Start()
    {
                // Başlangıçta menüleri gizle
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (confirmationDialogPanel != null) confirmationDialogPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (saveNotificationText != null) saveNotificationText.gameObject.SetActive(false);

        // Düğmelere tıklama olaylarını ekle
        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
        if (saveButton != null) saveButton.onClick.AddListener(SaveGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(MainMenuRequest);
        if (quitButton != null) quitButton.onClick.AddListener(QuitRequest);
        if (saveAndProceedButton != null) saveAndProceedButton.onClick.AddListener(OnSaveAndProceed);
        if (proceedWithoutSavingButton != null) proceedWithoutSavingButton.onClick.AddListener(OnProceedWithoutSaving);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);

        // Ayarları yükle
        LoadAudioSettings();

        // Ayar değişikliklerini dinle
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (soundEffectsVolumeSlider != null) soundEffectsVolumeSlider.onValueChanged.AddListener(SetSoundEffectsVolume);
        if (minimapToggle != null) minimapToggle.onValueChanged.AddListener(SetMinimapEnabled);
        if (musicMuteToggle != null) musicMuteToggle.onValueChanged.AddListener(SetMusicMuted);
        if (graphicsDropdown != null) graphicsDropdown.onValueChanged.AddListener(SetGraphicsQuality);
        
        // SettingsManager'dan ayar değişikliklerini dinle (real-time sync için)
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
        }

        // Sahne yüklendiğinde oyuncunun pozisyonunu yükle
        LoadPlayerPosition();
    }

    void LoadPlayerPosition()
    {
        if (PlayerPrefs.HasKey("PlayerPosX"))
        {
            float posX = PlayerPrefs.GetFloat("PlayerPosX");
            float posY = PlayerPrefs.GetFloat("PlayerPosY");
            float posZ = PlayerPrefs.GetFloat("PlayerPosZ");
            if (playerTransform != null)
            {
                playerTransform.position = new Vector3(posX, posY, posZ);
                Debug.Log("Oyuncu pozisyonu yüklendi: " + playerTransform.position);
            }
            else
            {
                Debug.LogError("PlayerTransform, Unity editöründe atanmamış!");
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If any drag is in progress, cancel it first and consume ESC
            if (DragAndDropHandler.TryCancelCurrentDragAndConsumeEsc())
            {
                return; // drag cancelled; do not open/close pause this frame
            }
            // If Market consumed ESC this frame, do nothing
            if (MarketManager.DidConsumeEscapeThisFrame())
            {
                return;
            }
            // If DragAndDrop consumed ESC this frame, do nothing
            if (DragAndDropHandler.DidConsumeEscapeThisFrame())
            {
                return;
            }
            // Önce diğer panellerin açık olup olmadığını kontrol et
            if (IsAnyOtherPanelActive())
            {
                // Başka paneller açıksa pause menüyü açma
                return;
            }

            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else if (confirmationDialogPanel != null && confirmationDialogPanel.activeSelf)
            {
                OnCancel();
            }
            else if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // Diğer panellerin açık olup olmadığını kontrol eden metod
    private bool IsAnyOtherPanelActive()
    {
        // FlaskManager ve BookManager scriptlerini bul
        FlaskManager flaskManager = FindObjectOfType<FlaskManager>();
        BookManager bookManager = FindObjectOfType<BookManager>();
        MarketManager marketManager = FindObjectOfType<MarketManager>();

        // Flask paneli açık mı kontrol et
        if (flaskManager != null && flaskManager.IsPanelActive())
        {
            return true;
        }

        // Book paneli açık mı kontrol et
        if (bookManager != null && bookManager.IsPanelActive())
        {
            return true;
        }

        // Market paneli açık mı kontrol et
        if (marketManager != null && marketManager.IsPanelActive())
        {
            return true;
        }

        // Başka panel scriptleri eklemek için buraya ekleyebilirsiniz
        
        return false;
    }

    void PauseGame()
    {
        Time.timeScale = 0;
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
            isPaused = true;
            wasPauseMenuOpen = true;
            IsPausedGlobally = true;
            if (autoBlockInteractions)
            {
                BlockSceneInteractions(true);
            }
        }
        else
        {
            Debug.LogError("PauseMenuPanel, Unity editöründe atanmamış!");
        }
    }

    void ResumeGame()
    {
        Time.timeScale = 1;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        isPaused = false;
        wasPauseMenuOpen = false;
        IsPausedGlobally = false;
        if (autoBlockInteractions)
        {
            BlockSceneInteractions(false);
        }
    }

    void SaveGame()
    {
        // Try advanced save system first
        GameSaveManager saveManager = FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveGame();
            StartCoroutine(ShowSaveNotification());
            return;
        }
        
        // Fallback to old system
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string saveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        List<string> saveTimes = GetSaveTimes();
        saveTimes.Add(saveTime);
        if (saveTimes.Count > 3) saveTimes.RemoveAt(0);

        PlayerPrefs.SetString("SavedScene_" + saveTime, currentScene);

        // Oyuncunun pozisyonunu kaydet
        if (playerTransform != null)
        {
            PlayerPrefs.SetFloat("PlayerPosX", playerTransform.position.x);
            PlayerPrefs.SetFloat("PlayerPosY", playerTransform.position.y);
            PlayerPrefs.SetFloat("PlayerPosZ", playerTransform.position.z);
            Debug.Log("Oyuncu pozisyonu kaydedildi: " + playerTransform.position);
        }
        else
        {
            Debug.LogError("PlayerTransform, Unity editöründe atanmamış!");
        }

        PlayerPrefs.SetString("SaveTimes", string.Join(",", saveTimes.ToArray()));
        PlayerPrefs.Save();
        Debug.Log("Oyun kaydedildi: " + saveTime);

        StartCoroutine(ShowSaveNotification());
    }

    void MainMenuRequest()
    {
        ShowSaveConfirmation("MainMenu");
    }

    void QuitRequest()
    {
        ShowSaveConfirmation("Quit");
    }

    List<string> GetSaveTimes()
    {
        string times = PlayerPrefs.GetString("SaveTimes", "");
        return new List<string>(times.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
    }

    void OpenSettings()
    {
        if (settingsPanel != null)
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (confirmationDialogPanel != null) confirmationDialogPanel.SetActive(false);
            
            // Settings panel açıldığında güncel ayarları yükle
            LoadAudioSettings();
            
            settingsPanel.SetActive(true);
            wasPauseMenuOpen = isPaused;
            
            Debug.Log("🎵 Settings panel opened, audio settings refreshed");
        }
        else
        {
            Debug.LogError("SettingsPanel, Unity editöründe atanmamış!");
        }
    }

    void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        
        // Settings kapatılırken ayarları kaydet
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SaveSettings();
            Debug.Log("🎵 Settings saved when closing settings panel");
        }
        
        if (wasPauseMenuOpen && pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Audio ayarlarını yükle (SettingsManager veya PlayerPrefs'ten)
    /// </summary>
    void LoadAudioSettings()
    {
        // SettingsManager'dan ayarları yükle
        if (SettingsManager.Instance != null)
        {
            var settings = SettingsManager.Instance.Current;
            
            if (volumeSlider != null)
            {
                volumeSlider.value = settings.masterVolume;
            }
            
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = settings.musicVolume;
            }
            
            if (soundEffectsVolumeSlider != null)
            {
                soundEffectsVolumeSlider.value = settings.soundEffectsVolume;
            }
            
            if (minimapToggle != null)
            {
                minimapToggle.isOn = settings.minimapEnabled;
            }
            
            if (musicMuteToggle != null)
            {
                musicMuteToggle.isOn = settings.musicMuted;
            }
            
            Debug.Log($"🎵 Settings loaded from SettingsManager: Master={settings.masterVolume:F2}, Music={settings.musicVolume:F2}, SFX={settings.soundEffectsVolume:F2}, Minimap={settings.minimapEnabled}, MusicMuted={settings.musicMuted}");
        }
        else
        {
            // Fallback: PlayerPrefs'ten yükle
            if (volumeSlider != null) volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
            if (musicVolumeSlider != null) musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.2f);
            if (soundEffectsVolumeSlider != null) soundEffectsVolumeSlider.value = PlayerPrefs.GetFloat("SoundEffectsVolume", 0.7f);
            if (minimapToggle != null) minimapToggle.isOn = PlayerPrefs.GetInt("MinimapEnabled", 1) == 1;
            if (musicMuteToggle != null) musicMuteToggle.isOn = PlayerPrefs.GetInt("MusicMuted", 1) == 1; // Default muted
            
            Debug.LogWarning("SettingsManager not available, using PlayerPrefs fallback");
        }
        
        // Graphics ayarını yükle
        if (graphicsDropdown != null) 
        {
            if (SettingsManager.Instance != null)
            {
                graphicsDropdown.value = SettingsManager.Instance.Current.graphicsQuality;
            }
            else
            {
                graphicsDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", 0);
            }
        }
    }
    
    /// <summary>
    /// Master volume ayarını değiştir
    /// </summary>
    void SetMasterVolume(float volume)
    {
        // SettingsManager kullan (eğer varsa)
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMasterVolume(volume);
            Debug.Log($"🎵 Master volume set via SettingsManager: {volume:F2}");
        }
        else
        {
            // Fallback: PlayerPrefs ve AudioListener
            PlayerPrefs.SetFloat("Volume", volume);
            PlayerPrefs.Save();
            AudioListener.volume = volume;
            Debug.Log($"🎵 Master volume set via fallback: {volume:F2}");
        }
    }
    
    /// <summary>
    /// Music volume ayarını değiştir
    /// </summary>
    void SetMusicVolume(float volume)
    {
        // SettingsManager kullan (eğer varsa)
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicVolume(volume);
            Debug.Log($"🎵 Music volume set via SettingsManager: {volume:F2}");
        }
        else
        {
            // Fallback: PlayerPrefs ve SoundManager
            PlayerPrefs.SetFloat("MusicVolume", volume);
            PlayerPrefs.Save();
            
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetMusicVolume(volume);
            }
            
            Debug.Log($"🎵 Music volume set via fallback: {volume:F2}");
        }
    }
    
    /// <summary>
    /// Sound effects volume ayarını değiştir (footstep, player sounds vs.)
    /// </summary>
    void SetSoundEffectsVolume(float volume)
    {
        // SettingsManager kullan (eğer varsa)
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetSoundEffectsVolume(volume);
            Debug.Log($"🔊 Sound effects volume set via SettingsManager: {volume:F2}");
        }
        else
        {
            // Fallback: PlayerPrefs
            PlayerPrefs.SetFloat("SoundEffectsVolume", volume);
            PlayerPrefs.Save();
            
            Debug.Log($"🔊 Sound effects volume set via fallback: {volume:F2}");
        }
    }

    /// <summary>
    /// Minimap aktif/pasif durumunu değiştir
    /// </summary>
    void SetMinimapEnabled(bool enabled)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMinimapEnabled(enabled);
            Debug.Log($"🗺️ Minimap {(enabled ? "enabled" : "disabled")} via SettingsManager");
        }
        else
        {
            // Fallback: PlayerPrefs
            PlayerPrefs.SetInt("MinimapEnabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
            
            // Direkt apply - önce assigned canvas'ı kontrol et
            GameObject targetCanvas = minimapCanvas;
            if (targetCanvas == null)
            {
                // Assign edilmemişse otomatik ara
                targetCanvas = GameObject.FindGameObjectWithTag("MinimapCanvas");
                if (targetCanvas == null)
                {
                    targetCanvas = GameObject.Find("MinimapCanvas");
                }
            }
            
            if (targetCanvas != null)
            {
                targetCanvas.SetActive(enabled);
            }
            
            Debug.Log($"🗺️ Minimap {(enabled ? "enabled" : "disabled")} via fallback");
        }
    }

    /// <summary>
    /// Müzik mute/unmute durumunu değiştir
    /// </summary>
    void SetMusicMuted(bool muted)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicMuted(muted);
            Debug.Log($"🔇 Music {(muted ? "muted" : "unmuted")} via SettingsManager");
        }
        else
        {
            // Fallback: PlayerPrefs
            PlayerPrefs.SetInt("MusicMuted", muted ? 1 : 0);
            PlayerPrefs.Save();
            
            // Direkt SoundManager'a apply
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetMusicMuted(muted);
            }
            
            Debug.Log($"🔇 Music {(muted ? "muted" : "unmuted")} via fallback");
        }
    }

    void SetVolume(float volume)
    {
        // Deprecated - SetMasterVolume kullanılmalı
        SetMasterVolume(volume);
    }

    void SetGraphicsQuality(int qualityIndex)
    {
        // SettingsManager kullan (eğer varsa)
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetGraphicsQuality(qualityIndex);
            Debug.Log($"🎮 Graphics quality set via SettingsManager: {qualityIndex}");
        }
        else
        {
            // Fallback: PlayerPrefs ve QualitySettings
            PlayerPrefs.SetInt("GraphicsQuality", qualityIndex);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(qualityIndex);
            Debug.Log($"� Graphics quality set via fallback: {qualityIndex}");
        }
    }
    
    /// <summary>
    /// SettingsManager'da ayarlar değiştiğinde çağrılır (real-time sync)
    /// </summary>
    void OnSettingsChanged(SettingsManager.GameSettings settings)
    {
        // UI'daki slider'ları güncelle (değer değişmiş olabilir)
        if (volumeSlider != null && Mathf.Abs(volumeSlider.value - settings.masterVolume) > 0.01f)
        {
            volumeSlider.value = settings.masterVolume;
        }
        
        if (musicVolumeSlider != null && Mathf.Abs(musicVolumeSlider.value - settings.musicVolume) > 0.01f)
        {
            musicVolumeSlider.value = settings.musicVolume;
        }
        
        if (soundEffectsVolumeSlider != null && Mathf.Abs(soundEffectsVolumeSlider.value - settings.soundEffectsVolume) > 0.01f)
        {
            soundEffectsVolumeSlider.value = settings.soundEffectsVolume;
        }
        
        if (minimapToggle != null && minimapToggle.isOn != settings.minimapEnabled)
        {
            minimapToggle.isOn = settings.minimapEnabled;
        }
        
        if (musicMuteToggle != null && musicMuteToggle.isOn != settings.musicMuted)
        {
            musicMuteToggle.isOn = settings.musicMuted;
        }
        
        if (graphicsDropdown != null && graphicsDropdown.value != settings.graphicsQuality)
        {
            graphicsDropdown.value = settings.graphicsQuality;
        }
        
        Debug.Log($"🎵 PauseMenu settings synced: Master={settings.masterVolume:F2}, Music={settings.musicVolume:F2}, SFX={settings.soundEffectsVolume:F2}, Minimap={settings.minimapEnabled}, MusicMuted={settings.musicMuted}");
    }
    
    void OnDestroy()
    {
        // SettingsManager event listener'ını temizle
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
        }
    }

    void ShowSaveConfirmation(string action)
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        pendingAction = action;
        if (confirmationText != null && confirmationDialogPanel != null)
        {
            string actionText = action == "MainMenu" ? "returning to the main menu" : "quitting the game";
            confirmationText.text = "Do you want to save the game before " + actionText + "?";
            confirmationDialogPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("ConfirmationText veya ConfirmationDialogPanel, Unity editöründe atanmamış!");
        }
    }

    void OnSaveAndProceed()
    {
        SaveGame();
        ProceedWithAction();
    }

    void OnProceedWithoutSaving()
    {
        ProceedWithAction();
    }

    void OnCancel()
    {
        if (confirmationDialogPanel != null) confirmationDialogPanel.SetActive(false);
        if (wasPauseMenuOpen && pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    void ProceedWithAction()
    {
        if (confirmationDialogPanel != null) confirmationDialogPanel.SetActive(false);
        if (pendingAction == "MainMenu")
        {
            Time.timeScale = 1;
            // Auto-save before leaving to MainMenu if a save system exists
            var saveManager = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
            if (saveManager != null)
            {
                saveManager.SaveGame();
            }
            // Ensure we clear any interaction blocks before switching scenes
            if (autoBlockInteractions) BlockSceneInteractions(false);
            IsPausedGlobally = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        else if (pendingAction == "Quit")
        {
            if (autoBlockInteractions) BlockSceneInteractions(false);
            IsPausedGlobally = false;
            Application.Quit();
        }
    }

    IEnumerator ShowSaveNotification()
    {
        if (saveNotificationText != null)
        {
            saveNotificationText.text = "Game saved successfully";
            saveNotificationText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(2f);
            saveNotificationText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("saveNotificationText, Unity editöründe atanmamış!");
        }
    }
}

// --------------- Helpers ---------------
partial class PauseMenuController
{
    private void BlockSceneInteractions(bool block)
    {
        try
        {
            if (block)
            {
                // Disable known interaction scripts
                DisableAllOfType<HarrowManager>();
                DisableAllOfType<BucketManager>();
                DisableAllOfType<DrawerManager>();
                DisableAllOfType<WellManager>();
                DisableAllOfType<BookManager>();
                DisableAllOfType<FlaskManager>();
                DisableAllOfType<MarketManager>();
                DisableAllOfType<DragAndDropHandler>();
                // Eazy camera input controller (prevents rotating/zooming by input)
                DisableAllOfType<EazyCamera.EazyController>();
                // Optional: player movement/input scripts if present in your project
                TryDisableByTypeName("PlayerMovement");
                TryDisableByTypeName("EzPlayerController");

                // Additional manual behaviours
                foreach (var b in additionalBehavioursToBlock)
                {
                    if (b != null && b.enabled)
                    {
                        b.enabled = false;
                        _blockedBehaviours.Add(b);
                    }
                }
            }
            else
            {
                // Re-enable only the behaviours we disabled
                foreach (var b in _blockedBehaviours)
                {
                    if (b != null)
                    {
                        b.enabled = true;
                    }
                }
                _blockedBehaviours.Clear();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PauseMenu] Interaction block error: {e.Message}");
        }
    }

    private void DisableAllOfType<T>() where T : Behaviour
    {
        var comps = FindObjectsOfType<T>();
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c != null && c.enabled)
            {
                c.enabled = false;
                _blockedBehaviours.Add(c);
            }
        }
    }

    private void TryDisableByTypeName(string typeName)
    {
        var t = Type.GetType(typeName) ?? FindTypeInAssemblies(typeName);
        if (t == null || !typeof(Behaviour).IsAssignableFrom(t)) return;
        var comps = FindObjectsOfType(t) as UnityEngine.Object[];
        if (comps == null) return;
        for (int i = 0; i < comps.Length; i++)
        {
            var b = comps[i] as Behaviour;
            if (b != null && b.enabled)
            {
                b.enabled = false;
                _blockedBehaviours.Add(b);
            }
        }
    }

    private static Type FindTypeInAssemblies(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(typeName);
            if (t != null) return t;
        }
        return null;
    }
}