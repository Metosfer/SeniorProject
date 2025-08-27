using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using System;

public class PauseMenuController : MonoBehaviour
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
    public Slider volumeSlider;                 // Ses ayarı slider'ı
    public Dropdown graphicsDropdown;           // Grafik ayarı dropdown'ı
    public Transform playerTransform;           // Oyuncu pozisyonu için referans

    private bool isPaused = false;              // Oyun duraklatıldı mı?
    private string pendingAction;               // Bekleyen eylem (MainMenu veya Quit)
    private bool wasPauseMenuOpen = false;      // PauseMenu açık mıydı?

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
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => ShowSaveConfirmation("MainMenu"));
        if (quitButton != null) quitButton.onClick.AddListener(() => ShowSaveConfirmation("Quit"));
        if (saveAndProceedButton != null) saveAndProceedButton.onClick.AddListener(OnSaveAndProceed);
        if (proceedWithoutSavingButton != null) proceedWithoutSavingButton.onClick.AddListener(OnProceedWithoutSaving);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);

        // Ayarları yükle
        if (volumeSlider != null) volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
        if (graphicsDropdown != null) graphicsDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", 0);

        // Ayar değişikliklerini dinle
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(SetVolume);
        if (graphicsDropdown != null) graphicsDropdown.onValueChanged.AddListener(SetGraphicsQuality);

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
            // If Market consumed ESC this frame, do nothing
            if (MarketManager.DidConsumeEscapeThisFrame())
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
            settingsPanel.SetActive(true);
            wasPauseMenuOpen = isPaused;
        }
        else
        {
            Debug.LogError("SettingsPanel, Unity editöründe atanmamış!");
        }
    }

    void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (wasPauseMenuOpen && pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
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
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        else if (pendingAction == "Quit")
        {
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