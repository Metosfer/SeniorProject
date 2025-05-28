using UnityEngine;
using UnityEngine.UI;
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

        // Ayarları yükle
        if (volumeSlider != null) volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
        if (graphicsDropdown != null) graphicsDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", 0);

        // Ayar değişikliklerini dinle
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(SetVolume);
        if (graphicsDropdown != null) graphicsDropdown.onValueChanged.AddListener(SetGraphicsQuality);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
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
        Application.LoadLevel("FarmScene");
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
        string savedScene = PlayerPrefs.GetString("SavedScene_" + saveTime);
        if (!string.IsNullOrEmpty(savedScene))
        {
            Application.LoadLevel(savedScene);
            if (loadPanel != null) loadPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("No scene found for save time: " + saveTime);
        }
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
            settingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("SettingsPanel, Unity editöründe atanmamış!");
        }
    }

    void SetVolume(float volume)
    {
        PlayerPrefs.SetFloat("Volume", volume);
        PlayerPrefs.Save();
    }

    void SetGraphicsQuality(int qualityIndex)
    {
        PlayerPrefs.SetInt("GraphicsQuality", qualityIndex);
        PlayerPrefs.Save();
    }

    void QuitGame()
    {
        Application.Quit();
    }
}