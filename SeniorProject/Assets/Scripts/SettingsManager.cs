using UnityEngine;
using System;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Serializable]
    public class GameSettings
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.2f; // Müzik ses seviyesi (0-1 arası) - default düşük
        public float soundEffectsVolume = 0.7f; // Player ses efektleri (footstep vs.) - default orta seviye
        public bool musicMuted = true; // Müzik tamamen susturulmuş mu? - default muted
        public int graphicsQuality = 0; // QualitySettings index
        public bool minimapEnabled = true; // Minimap açık/kapalı durumu

        // Camera/Input
        public bool cameraRotateOnlyWithRMB = false;
        public bool cameraInvertY = false;
        public float cameraMouseSensitivity = 1.0f; // multiplier
        public bool cameraShowRotateCursor = true;
        public int rotateCursorSize = 32;
        public bool dpiAwareCursor = true;

    // Display/Graphics (classic)
    public int resolutionIndex = -1; // -1 = keep current
    public int displayMode = (int)FullScreenMode.FullScreenWindow; // serialized as int
    public bool vSync = true; // QualitySettings.vSyncCount>0
    public int targetFPS = -1; // -1 uncapped; ignored if vSync
    }

    public GameSettings Current = new GameSettings();
    public event Action<GameSettings> OnSettingsChanged;

    private const string KEY = "GameSettings_v1";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Eski high MusicVolume değerini düzelt (tek seferlik)
        if (PlayerPrefs.HasKey("MusicVolume") && PlayerPrefs.GetFloat("MusicVolume") > 0.5f)
        {
            Debug.Log($"🔄 Resetting old MusicVolume {PlayerPrefs.GetFloat("MusicVolume"):F2} to default 0.2");
            PlayerPrefs.SetFloat("MusicVolume", 0.2f);
            PlayerPrefs.Save();
        }
        
        LoadSettings();
        ApplyAll();
    }

    public void SetMasterVolume(float v)
    {
        Current.masterVolume = Mathf.Clamp01(v);
        ApplyAudio();
        SaveSettings();
        RaiseChanged();
    }

    public void SetGraphicsQuality(int idx)
    {
        idx = Mathf.Clamp(idx, 0, QualitySettings.names.Length - 1);
        Current.graphicsQuality = idx;
        ApplyGraphics();
        SaveSettings();
        RaiseChanged();
    }

    public void SetCameraRotateOnlyWithRMB(bool val)
    {
        Current.cameraRotateOnlyWithRMB = val;
        ApplyCameraToControllers();
        SaveSettings();
        RaiseChanged();
    }

    public void SetCameraInvertY(bool val)
    {
        Current.cameraInvertY = val;
        ApplyCameraToControllers();
        SaveSettings();
        RaiseChanged();
    }

    public void SetCameraSensitivity(float val)
    {
        Current.cameraMouseSensitivity = Mathf.Max(0.05f, val);
        ApplyCameraToControllers();
        SaveSettings();
        RaiseChanged();
    }

    public void SetCameraShowRotateCursor(bool val)
    {
        Current.cameraShowRotateCursor = val;
        ApplyCameraToControllers();
        SaveSettings();
        RaiseChanged();
    }

    public void SetRotateCursorSize(int px)
    {
        Current.rotateCursorSize = Mathf.Clamp(px, 8, 256);
        ApplyCameraToControllers();
        SaveSettings();
        RaiseChanged();
    }

    public void SetDPIAwareCursor(bool val)
    {
        Current.dpiAwareCursor = val;
        ApplyCameraToControllers();
        SaveSettings();
        RaiseChanged();
    }
    
    public void SetMusicVolume(float v)
    {
        Current.musicVolume = Mathf.Clamp01(v);
        ApplyAudio();
        SaveSettings();
        RaiseChanged();
    }
    
    public void SetSoundEffectsVolume(float v)
    {
        Current.soundEffectsVolume = Mathf.Clamp01(v);
        ApplyAudio();
        SaveSettings();
        RaiseChanged();
    }

    public void SetMinimapEnabled(bool enabled)
    {
        Current.minimapEnabled = enabled;
        ApplyMinimap();
        SaveSettings();
        RaiseChanged();
    }

    public void SetMusicMuted(bool muted)
    {
        Current.musicMuted = muted;
        ApplyAudio();
        SaveSettings();
        RaiseChanged();
    }

    public void ApplyAll()
    {
        ApplyAudio();
        ApplyGraphics();
        ApplyCameraToControllers();
        ApplyMinimap();
    }

    private void ApplyAudio()
    {
        AudioListener.volume = Mathf.Clamp01(Current.masterVolume);
        
        // SoundManager'a müzik ses seviyesini uygula
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMusicVolume(Current.musicVolume);
            SoundManager.Instance.SetMusicMuted(Current.musicMuted);
        }
        else
        {
            // SoundManager henüz yüklenmemişse sahne de arayalım
            var soundManager = FindObjectOfType<SoundManager>();
            if (soundManager != null)
            {
                soundManager.SetMusicVolume(Current.musicVolume);
                soundManager.SetMusicMuted(Current.musicMuted);
                Debug.Log("🎵 Found SoundManager in scene and applied music volume");
            }
        }
    }

    private void ApplyGraphics()
    {
        int idx = Mathf.Clamp(Current.graphicsQuality, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(idx, true);

        // VSync & FPS
        QualitySettings.vSyncCount = Current.vSync ? 1 : 0;
        Application.targetFrameRate = Current.vSync ? -1 : Current.targetFPS;

        // Display mode
        var mode = (FullScreenMode)Mathf.Clamp(Current.displayMode, 0, (int)FullScreenMode.Windowed);

        // Resolution (optional) — only if valid index found
        if (Current.resolutionIndex >= 0)
        {
            var list = GetUniqueResolutions();
            if (Current.resolutionIndex < list.Count)
            {
                var r = list[Current.resolutionIndex];
                var rr = r.refreshRateRatio;
                Screen.SetResolution(r.width, r.height, mode, rr);
            }
            else
            {
                // Fallback: apply mode only
                Screen.fullScreenMode = mode;
            }
        }
        else
        {
            // Apply display mode if resolution untouched
            Screen.fullScreenMode = mode;
        }
    }

    private void ApplyMinimap()
    {
        // Önce PauseMenuController'dan direkt assigned canvas'ı kontrol et
        var pauseMenuController = FindObjectOfType<PauseMenuController>();
        GameObject minimapCanvas = null;
        
        if (pauseMenuController != null && pauseMenuController.minimapCanvas != null)
        {
            minimapCanvas = pauseMenuController.minimapCanvas;
            Debug.Log("🗺️ Using assigned minimap canvas from PauseMenuController");
        }
        else
        {
            // Assign edilmemişse otomatik ara
            minimapCanvas = GameObject.FindGameObjectWithTag("MinimapCanvas");
            if (minimapCanvas == null)
            {
                minimapCanvas = GameObject.Find("MinimapCanvas");
            }
        }
        
        if (minimapCanvas != null)
        {
            minimapCanvas.SetActive(Current.minimapEnabled);
            Debug.Log($"Minimap {(Current.minimapEnabled ? "enabled" : "disabled")}");
        }
        else
        {
            Debug.LogWarning("Minimap Canvas bulunamadı! GameObject'e 'MinimapCanvas' tag'i veya ismi ekleyin, ya da PauseMenuController'a direkt assign edin.");
        }
    }

    // Public setters for classic settings
    public void SetResolutionIndex(int index)
    {
        var list = GetUniqueResolutions();
        if (list.Count == 0) return;
        index = Mathf.Clamp(index, 0, list.Count - 1);
        Current.resolutionIndex = index;
        var r = list[index];
    var rr = r.refreshRateRatio;
    Screen.SetResolution(r.width, r.height, (FullScreenMode)Current.displayMode, rr);
        SaveSettings();
        RaiseChanged();
    }

    public void SetDisplayMode(int modeInt)
    {
        modeInt = Mathf.Clamp(modeInt, 0, (int)FullScreenMode.Windowed);
        Current.displayMode = modeInt;
        Screen.fullScreenMode = (FullScreenMode)modeInt;
        SaveSettings();
        RaiseChanged();
    }

    public void SetVSync(bool enabled)
    {
        Current.vSync = enabled;
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        if (enabled) Application.targetFrameRate = -1;
        SaveSettings();
        RaiseChanged();
    }

    public void SetTargetFPS(int fps)
    {
        if (fps < 0) fps = -1;
        Current.targetFPS = fps;
        if (!Current.vSync) Application.targetFrameRate = fps;
        SaveSettings();
        RaiseChanged();
    }

    // Build a unique resolution list by width x height with highest refresh per pair
    private static System.Collections.Generic.List<Resolution> GetUniqueResolutions()
    {
        var src = Screen.resolutions;
        var dict = new System.Collections.Generic.Dictionary<(int,int), Resolution>();
        for (int i = 0; i < src.Length; i++)
        {
            var r = src[i];
            var key = (r.width, r.height);
            if (!dict.TryGetValue(key, out var exist) || r.refreshRateRatio.value > exist.refreshRateRatio.value)
            {
                dict[key] = r;
            }
        }
        var list = new System.Collections.Generic.List<Resolution>(dict.Values);
        list.Sort((a,b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
        return list;
    }

    private void ApplyCameraToControllers()
    {
        var controllers = FindObjectsOfType<EazyCamera.EazyController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            var c = controllers[i];
            if (c == null) continue;
            TryApplyToController(c);
        }
    }

    private void TryApplyToController(EazyCamera.EazyController ctrl)
    {
        // Use public APIs when available; else reflection/setters
        // We know EazyController has private fields; add helper methods there if needed. For now, use reflection guardedly.
        try
        {
            var t = ctrl.GetType();
            var fRmb = t.GetField("_rotateOnlyWhileRightMouse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fShow = t.GetField("_changeCursorWhileRotating", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fSize = t.GetField("_rotateCursorSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fDpi = t.GetField("_autoAdjustRotateCursorForDPI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fSens = t.GetField("_mouseSensitivity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fInv = t.GetField("_invertY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fRmb != null) fRmb.SetValue(ctrl, Current.cameraRotateOnlyWithRMB);
            if (fShow != null) fShow.SetValue(ctrl, Current.cameraShowRotateCursor);
            if (fSize != null) fSize.SetValue(ctrl, Current.rotateCursorSize);
            if (fDpi != null) fDpi.SetValue(ctrl, Current.dpiAwareCursor);
            if (fSens != null) fSens.SetValue(ctrl, Current.cameraMouseSensitivity);
            if (fInv != null) fInv.SetValue(ctrl, Current.cameraInvertY);
        }
        catch { }
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonUtility.ToJson(Current);
            PlayerPrefs.SetString(KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to save settings: {e.Message}");
        }
    }

    public void LoadSettings()
    {
        try
        {
            if (PlayerPrefs.HasKey(KEY))
            {
                string json = PlayerPrefs.GetString(KEY, "");
                if (!string.IsNullOrEmpty(json))
                {
                    Current = JsonUtility.FromJson<GameSettings>(json);
                }
            }
            else
            {
                // Back-compat from existing prefs if present
                if (PlayerPrefs.HasKey("Volume")) Current.masterVolume = PlayerPrefs.GetFloat("Volume", 1f);
                if (PlayerPrefs.HasKey("MusicVolume")) Current.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.2f);
                if (PlayerPrefs.HasKey("GraphicsQuality")) Current.graphicsQuality = PlayerPrefs.GetInt("GraphicsQuality", 0);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to load settings: {e.Message}");
            Current = new GameSettings();
        }
    }

    private void RaiseChanged()
    {
        try { OnSettingsChanged?.Invoke(Current); } catch { }
    }
    
    /// <summary>
    /// Debug: Tüm ayarları sıfırla ve default değerlere döndür
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void ResetAllSettings()
    {
        // Eski PlayerPrefs'leri temizle
        if (PlayerPrefs.HasKey("Volume")) PlayerPrefs.DeleteKey("Volume");
        if (PlayerPrefs.HasKey("MusicVolume")) PlayerPrefs.DeleteKey("MusicVolume");
        if (PlayerPrefs.HasKey("GraphicsQuality")) PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.DeleteKey(KEY);
        
        // Default ayarlara döndür
        Current = new GameSettings();
        SaveSettings();
        ApplyAll();
        
        Debug.Log("🔄 All settings reset to default values (Music Volume: 0.2)");
    }
}
