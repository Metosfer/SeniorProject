using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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
            Debug.Log("[SaveSystemManager] Auto-save on scene change enabled, subscribing to sceneUnloaded event");
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            // Ek güvence: aktif sahne değiştiğinde de kaydet (GameSaveManager önce snapshot alır)
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
        else
        {
            Debug.LogWarning("[SaveSystemManager] Auto-save on scene change is DISABLED!");
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
        Debug.Log($"[SaveSystemManager] Scene unloading: {scene.name}, triggering auto-save...");
        
        // Sahne değişmeden önce save et
        if (GameSaveManager.Instance != null)
        {
            Debug.Log("[SaveSystemManager] GameSaveManager found, calling SaveGame()");
            GameSaveManager.Instance.SaveGame();
            Debug.Log("[SaveSystemManager] Auto-save completed before scene unload");
        }
        else
        {
            Debug.LogError("[SaveSystemManager] GameSaveManager.Instance is null, cannot auto-save!");
        }
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        // GameSaveManager bu event ile önce previous sahnenin snapshot'ını alır.
        // Burada bir frame bekleyip SaveGame çağırarak PlayerPrefs'e yazmayı garantile.
        if (GameSaveManager.Instance != null)
        {
            StartCoroutine(DeferredSave());
        }
    }

    private IEnumerator DeferredSave()
    {
        yield return null; // next frame
        GameSaveManager.Instance.SaveGame();
        Debug.Log("[SaveSystemManager] Deferred SaveGame after activeSceneChanged");
    }
    
    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
    UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
}
