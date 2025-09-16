using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Binds Settings UI controls to SettingsManager and applies live.
public class SettingsPanelBinder : MonoBehaviour
{
    [Header("Audio")]
    public Slider volumeSlider;
    public Slider musicVolumeSlider; // Müzik ses kontrolü için yeni slider
    public Slider soundEffectsVolumeSlider; // Sound effects ses kontrolü için yeni slider

    [Header("Graphics")]
    public TMP_Dropdown graphicsDropdown;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown displayModeDropdown; // Fullscreen/Borderless/Windowed
    public Toggle vSyncToggle;
    public TMP_Dropdown fpsCapDropdown; // e.g., Unlimited, 30, 60, 120, 144, 240

    [Header("Camera/Input")]
    public Toggle invertYToggle;
    public Slider sensitivitySlider;
    public Toggle dpiAwareCursorToggle;
    
    [Header("Toggle Labels (Optional)")]
    public TMP_Text invertYLabel;
    public TMP_Text dpiAwareCursorLabel;
    public TMP_Text vSyncLabel;
    
    [Header("Label Texts")]
    public string onText = "On";
    public string offText = "Off";
    public string invertYTitle = "Invert Y";
    public string dpiAwareCursorTitle = "DPI Aware Cursor";
    public string vSyncTitle = "VSync";

    private bool _initialized;

    private void OnEnable()
    {
        InitIfNeeded();
        RefreshFromSettings();
    }

    private void Awake()
    {
        InitIfNeeded();
    }

    private void InitIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        var sm = SettingsManager.Instance;
        if (sm == null) return;

        // Hook listeners
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(v => sm.SetMasterVolume(v));
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(v => {
                sm.SetMusicVolume(v);
                Debug.Log($"Music volume changed to: {v:F2}");
            });
        if (soundEffectsVolumeSlider != null)
            soundEffectsVolumeSlider.onValueChanged.AddListener(v => {
                sm.SetSoundEffectsVolume(v);
                Debug.Log($"Sound effects volume changed to: {v:F2}");
            });
        if (graphicsDropdown != null)
            graphicsDropdown.onValueChanged.AddListener(i => sm.SetGraphicsQuality(i));
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(i => sm.SetResolutionIndex(i));
        if (displayModeDropdown != null)
            displayModeDropdown.onValueChanged.AddListener(i => sm.SetDisplayMode(i));
        if (vSyncToggle != null)
            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.AddListener(b => { sm.SetVSync(b); UpdateToggleLabel(vSyncToggle, ref vSyncLabel, vSyncTitle, b); });
            fpsCapDropdown.onValueChanged.AddListener(OnFpsCapChanged);
        if (invertYToggle != null)
            invertYToggle.onValueChanged.AddListener(b => sm.SetCameraInvertY(b));
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(v => sm.SetCameraSensitivity(v));
        if (dpiAwareCursorToggle != null)
            dpiAwareCursorToggle.onValueChanged.AddListener(b => sm.SetDPIAwareCursor(b));

        sm.OnSettingsChanged += OnAnySettingChanged;

        // Populate dropdowns with real data
        PopulateGraphicsDropdown();
        PopulateResolutionDropdown();
        PopulateDisplayModeDropdown();
        PopulateFpsDropdown();
    }

    private void OnDestroy()
    {
        var sm = SettingsManager.Instance;
    if (sm != null) sm.OnSettingsChanged -= OnAnySettingChanged;
    }

    private void RefreshFromSettings()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;
        var s = sm.Current;

        if (volumeSlider != null && volumeSlider.value != s.masterVolume)
            volumeSlider.value = s.masterVolume;
        if (musicVolumeSlider != null && Mathf.Abs(musicVolumeSlider.value - s.musicVolume) > 0.0001f)
            musicVolumeSlider.value = s.musicVolume;
        if (soundEffectsVolumeSlider != null && Mathf.Abs(soundEffectsVolumeSlider.value - s.soundEffectsVolume) > 0.0001f)
            soundEffectsVolumeSlider.value = s.soundEffectsVolume;
        if (graphicsDropdown != null && graphicsDropdown.value != s.graphicsQuality)
            graphicsDropdown.value = s.graphicsQuality;
        if (resolutionDropdown != null && s.resolutionIndex >= 0 && resolutionDropdown.value != s.resolutionIndex)
            resolutionDropdown.value = s.resolutionIndex;
        if (displayModeDropdown != null && displayModeDropdown.value != s.displayMode)
            displayModeDropdown.value = s.displayMode;
        if (vSyncToggle != null && vSyncToggle.isOn != s.vSync)
            vSyncToggle.isOn = s.vSync;
        if (fpsCapDropdown != null)
        {
            int idx = FpsToDropdownIndex(s.vSync ? -1 : s.targetFPS);
            if (fpsCapDropdown.value != idx) fpsCapDropdown.value = idx;
        }
        if (invertYToggle != null && invertYToggle.isOn != s.cameraInvertY)
            invertYToggle.isOn = s.cameraInvertY;
        if (sensitivitySlider != null && Mathf.Abs(sensitivitySlider.value - s.cameraMouseSensitivity) > 0.0001f)
            sensitivitySlider.value = s.cameraMouseSensitivity;
    // Update labels to reflect current values
            UpdateToggleLabel(invertYToggle, ref invertYLabel, invertYTitle, s.cameraInvertY);
            UpdateToggleLabel(dpiAwareCursorToggle, ref dpiAwareCursorLabel, dpiAwareCursorTitle, s.dpiAwareCursor);
            UpdateToggleLabel(vSyncToggle, ref vSyncLabel, vSyncTitle, s.vSync);
        if (dpiAwareCursorToggle != null && dpiAwareCursorToggle.isOn != s.dpiAwareCursor)
            dpiAwareCursorToggle.isOn = s.dpiAwareCursor;
    }

    private void OnAnySettingChanged(SettingsManager.GameSettings _)
    {
        RefreshFromSettings();
    }

    // removed rotate cursor size input handling

    private void PopulateGraphicsDropdown()
    {
        if (graphicsDropdown == null) return;
        graphicsDropdown.ClearOptions();
        var names = QualitySettings.names;
        var list = new System.Collections.Generic.List<string>(names);
        graphicsDropdown.AddOptions(list);
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null) return;
        resolutionDropdown.ClearOptions();
        var uniq = GetUniqueResolutions();
        var opts = new System.Collections.Generic.List<string>(uniq.Count);
        for (int i = 0; i < uniq.Count; i++)
        {
            var r = uniq[i];
            opts.Add($"{r.width} x {r.height}");
        }
        resolutionDropdown.AddOptions(opts);
        // Try to match current screen res if no saved index
        var s = SettingsManager.Instance?.Current;
        if (s != null && s.resolutionIndex < 0)
        {
            int best = 0;
            for (int i = 0; i < uniq.Count; i++)
            {
                if (uniq[i].width == Screen.currentResolution.width && uniq[i].height == Screen.currentResolution.height)
                { best = i; break; }
            }
            s.resolutionIndex = best;
        }
    }

    private void PopulateDisplayModeDropdown()
    {
        if (displayModeDropdown == null) return;
        displayModeDropdown.ClearOptions();
        var opts = new System.Collections.Generic.List<string> { "Exclusive Fullscreen", "Fullscreen Window", "Maximized Window", "Windowed" };
        displayModeDropdown.AddOptions(opts);
    }

    private void PopulateFpsDropdown()
    {
        if (fpsCapDropdown == null) return;
        fpsCapDropdown.ClearOptions();
        var opts = new System.Collections.Generic.List<string> { "Unlimited", "30", "60", "120", "144", "240" };
        fpsCapDropdown.AddOptions(opts);
    }

    private int FpsToDropdownIndex(int fps)
    {
        if (fps <= 0) return 0;
        switch (fps)
        {
            case 30: return 1;
            case 60: return 2;
            case 120: return 3;
            case 144: return 4;
            case 240: return 5;
        }
        // fallback to Unlimited
        return 0;
    }

    private int DropdownIndexToFps(int index)
    {
        switch (index)
        {
            case 1: return 30;
            case 2: return 60;
            case 3: return 120;
            case 4: return 144;
            case 5: return 240;
            default: return -1; // Unlimited
        }
    }

    private void OnFpsCapChanged(int index)
    {
        int fps = DropdownIndexToFps(index);
        SettingsManager.Instance?.SetTargetFPS(fps);
    }

    private TMP_Text EnsureLabelRef(Toggle t, TMP_Text existing)
    {
        if (existing != null) return existing;
        if (t == null) return null;
        return t.GetComponentInChildren<TMP_Text>(true);
    }

    private void UpdateToggleLabel(Toggle t, ref TMP_Text labelField, string title, bool state)
    {
        var lab = EnsureLabelRef(t, labelField);
        labelField = lab;
        if (lab != null)
        {
            lab.text = string.Format("{0}: {1}", title, state ? onText : offText);
        }
    }

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
}
