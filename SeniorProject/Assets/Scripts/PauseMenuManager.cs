using UnityEngine;

public class PauseMenuManager : MonoBehaviour
{
    [Header("Pause Menu")]
    [Tooltip("Pause menu panel'ı")]
    public GameObject pauseMenuPanel;
    
    [Header("References")]
    [Tooltip("FishingManager referansı (ESC tuşu kontrolü için)")]
    public FishingManager fishingManager;
    
    private bool isPaused = false;
    
    void Update()
    {
        HandlePauseInput();
    }
    
    void HandlePauseInput()
    {
    if (InputHelper.GetKeyDown(KeyCode.Escape))
        {
            // Önce balık tutma oyunu aktif mi kontrol et
            if (fishingManager != null && fishingManager.IsFishingGameActive())
            {
                Debug.Log("Balık tutma oyunu aktif, pause menu açılmıyor.");
                return; // Balık tutma aktifse pause menu açma
            }
            
            // Balık tutma oyunu aktif değilse ve ESC tuşu tüketilmediyse pause menu'yu aç/kapat
            if (fishingManager != null && fishingManager.IsEscapeKeyConsumed())
            {
                Debug.Log("ESC tuşu balık tutma tarafından tüketildi, pause menu açılmıyor.");
                return; // ESC tuşu balık tutma tarafından kullanıldı
            }
            
            // Normal pause menu logic
            TogglePause();
        }
    }
    
    void TogglePause()
    {
        isPaused = !isPaused;
        
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(isPaused);
        }
        
        // Oyunu duraklat/devam ettir
        Time.timeScale = isPaused ? 0f : 1f;
        
        // Cursor'u göster/gizle
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;
        
        Debug.Log(isPaused ? "Oyun duraklatıldı" : "Oyun devam ediyor");
    }
    
    // Pause menu'den oyuna dön butonu için
    public void ResumeGame()
    {
        if (isPaused)
        {
            TogglePause();
        }
    }
    
    // Pause menu'den ana menüye dön butonu için
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Time scale'i resetle
        // SceneManager.LoadScene("MainMenu"); // Ana menü scene'ini yükle
        Debug.Log("Ana menüye dönülüyor...");
    }
    
    // Pause menu'den oyundan çık butonu için
    public void QuitGame()
    {
        Time.timeScale = 1f; // Time scale'i resetle
        Application.Quit();
        Debug.Log("Oyundan çıkılıyor...");
    }
}
