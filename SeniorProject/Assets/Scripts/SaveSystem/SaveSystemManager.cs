using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Save sistemini başlatan ve yöneten ana sınıf
/// Bu component'i sahneye ekleyerek save sistemini aktifleştirin
/// </summary>
public class SaveSystemManager : MonoBehaviour
{
    [Header("Save System Settings")]
    public bool autoSaveOnSceneChange = true;
    public bool loadOnStart = false;
    public string autoLoadSaveTime = "";
    
    private void Awake()
    {
        // GameSaveManager'ı oluştur
        if (GameSaveManager.Instance == null)
        {
            GameObject gameSaveManagerGO = new GameObject("GameSaveManager");
            gameSaveManagerGO.AddComponent<GameSaveManager>();
            DontDestroyOnLoad(gameSaveManagerGO);
        }
        
        // WorldItemSpawner'ı oluştur
        if (WorldItemSpawner.instance == null)
        {
            GameObject worldItemSpawnerGO = new GameObject("WorldItemSpawner");
            worldItemSpawnerGO.AddComponent<WorldItemSpawner>();
            DontDestroyOnLoad(worldItemSpawnerGO);
        }
        
        // Scene change events'leri dinle
        if (autoSaveOnSceneChange)
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
    }
    
    private void Start()
    {
        // Auto load
        if (loadOnStart && !string.IsNullOrEmpty(autoLoadSaveTime))
        {
            if (GameSaveManager.Instance != null)
            {
                GameSaveManager.Instance.LoadGame(autoLoadSaveTime);
            }
        }
    }
    
    private void OnSceneUnloaded(Scene scene)
    {
        // Sahne değişmeden önce save et
        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.SaveGame();
        }
    }
    
    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
}
