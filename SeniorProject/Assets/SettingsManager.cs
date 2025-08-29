using UnityEngine;
using System;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Serializable]
    public class GameSettings
    {
        public float masterVolume = 1f;
        public int graphicsQuality = 0; // QualitySettings index

        // Camera/Input
        public bool cameraRotateOnlyWithRMB = false;
        public bool cameraInvertY = false;
        public float cameraMouseSensitivity = 1.0f; // multiplier
        public bool cameraShowRotateCursor = true;
        public int rotateCursorSize = 32;
        public bool dpiAwareCursor = true;
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

    public void ApplyAll()
    {
        ApplyAudio();
        ApplyGraphics();
        ApplyCameraToControllers();
    }

    private void ApplyAudio()
    {
        AudioListener.volume = Mathf.Clamp01(Current.masterVolume);
    }

    private void ApplyGraphics()
    {
        int idx = Mathf.Clamp(Current.graphicsQuality, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(idx);
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
}
