using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    public Button startButton;          // "Start" düğmesi
    public Button loadButton;           // "Load" düğmesi
    public Button settingsButton;       // "Settings" düğmesi
    public Button quitButton;           // "Quit" düğmesi
    public GameObject loadPanel;        // Load paneli
    public Button[] loadButtons;        // Load seçenekleri için 3 buton
    public GameObject settingsPanel;    // Settings paneli
    public Slider volumeSlider;         // Ses ayarı slider'ı
    public Dropdown graphicsDropdown;   // Grafik ayarı dropdown'ı

    void Start()
    {
        // Düğmelere tıklama olaylarını ekle
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (loadButton != null) loadButton.onClick.AddListener(LoadGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        // Başlangıçta panelleri gizle
        if (loadPanel != null) loadPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Ayarları yükle (SettingsManager varsa onu kullan)
        var sm = SettingsManager.Instance;
        if (sm != null)
        {
            if (volumeSlider != null) volumeSlider.value = sm.Current.masterVolume;
            if (graphicsDropdown != null) graphicsDropdown.value = sm.Current.graphicsQuality;
        }
        else
        {
            if (volumeSlider != null) volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
            if (graphicsDropdown != null) graphicsDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", 0);
        }

        // Ayar değişikliklerini dinle (SettingsManager üzerinden uygula)
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(v => SetVolume(v));
        if (graphicsDropdown != null) graphicsDropdown.onValueChanged.AddListener(i => SetGraphicsQuality(i));
    }

    void Update()
    {
    if (InputHelper.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                settingsPanel.SetActive(false);
            }
            else if (loadPanel != null && loadPanel.activeSelf)
            {
                loadPanel.SetActive(false);
            }
        }
    }

    void StartGame()
    {
        // Try to load the latest save if it exists
        var saveManager = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            var saveTimes = saveManager.GetSaveTimes();
            if (saveTimes != null && saveTimes.Count > 0)
            {
                // Load the most recent save
                string latestSave = saveTimes[saveTimes.Count - 1];
                Debug.Log($"[MainMenu] Loading latest save: {latestSave}");
                saveManager.LoadGame(latestSave);
                return;
            }
            else
            {
                Debug.Log("[MainMenu] No existing saves, starting new game");
            }
        }
        
        // No saves or no save manager - start fresh
        UnityEngine.SceneManagement.SceneManager.LoadScene("FarmScene");
    }

    void LoadGame()
    {
        if (loadPanel == null)
        {
            Debug.LogError("LoadPanel, Unity editöründe atanmamış!");
            return;
        }

        loadPanel.SetActive(true);
        List<string> saveTimes = GetSaveTimes();
        Debug.Log("Save times: " + (saveTimes.Count > 0 ? string.Join(", ", saveTimes) : "No save times found"));

        for (int i = 0; i < loadButtons.Length && i < 3; i++)
        {
            if (loadButtons[i] == null)
            {
                Debug.LogError("Load button " + i + ", Unity editöründe atanmamış!");
                continue;
            }

            if (i < saveTimes.Count)
            {
                TextMeshProUGUI buttonText = loadButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = saveTimes[i];
                    int index = i;
                    loadButtons[i].onClick.RemoveAllListeners();
                    loadButtons[i].onClick.AddListener(() => LoadSpecificSave(saveTimes[index]));
                    loadButtons[i].gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogError("Load button " + i + " does not have a TextMeshProUGUI component!");
                    loadButtons[i].gameObject.SetActive(false);
                }
            }
            else
            {
                loadButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void LoadSpecificSave(string saveTime)
    {
        // Try advanced save system first
        GameSaveManager saveManager = FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            saveManager.LoadGame(saveTime);
            if (loadPanel != null) loadPanel.SetActive(false);
            return;
        }
        
        // Fallback to old system
        string savedScene = PlayerPrefs.GetString("SavedScene_" + saveTime);
        if (!string.IsNullOrEmpty(savedScene))
        {
            // Auto-save current state (if any) before scene change
            var saveManager2 = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
            if (saveManager2 != null)
            {
                saveManager2.SaveGame();
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(savedScene);
            if (loadPanel != null) loadPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("No scene found for save time: " + saveTime);
        }
    }

    List<string> GetSaveTimes()
    {
        // Try advanced save system first
        GameSaveManager saveManager = FindObjectOfType<GameSaveManager>();
        if (saveManager != null)
        {
            return saveManager.GetSaveTimes();
        }
        
        // Fallback to old system
        string times = PlayerPrefs.GetString("SaveTimes", "");
        return new List<string>(times.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
    }

    void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("SettingsPanel, Unity editöründe atanmamış!");
        }
    }

    void SetVolume(float volume)
    {
        var sm = SettingsManager.Instance;
        if (sm != null) sm.SetMasterVolume(volume);
        else
        {
            PlayerPrefs.SetFloat("Volume", volume);
            PlayerPrefs.Save();
        }
    }

    void SetGraphicsQuality(int qualityIndex)
    {
        var sm = SettingsManager.Instance;
        if (sm != null) sm.SetGraphicsQuality(qualityIndex);
        else
        {
            PlayerPrefs.SetInt("GraphicsQuality", qualityIndex);
            PlayerPrefs.Save();
        }
    }

    void QuitGame()
    {
        Application.Quit();
    }
}