using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameSaveData
{
    public string saveTime;
    public string sceneName;
    public PlayerSaveData playerData;
    public List<ScenePlayerState> playerSceneStates;
    public List<WorldItemSaveData> worldItems;
    public List<PlantSaveData> plants;
    public InventorySaveData inventoryData;
    // Yeni inventory save format
    public List<PlayerInventoryItemData> playerInventoryData;
    // Dictionary yerine List kullanarak JSON serialization problemini çözüyoruz
    public List<SceneObjectSaveData> sceneObjects;
    // Flask save data
    public FlaskSaveData flaskData;
    // Market save data
    public MarketSaveData marketData;
    
    public GameSaveData()
    {
        worldItems = new List<WorldItemSaveData>();
        plants = new List<PlantSaveData>();
        sceneObjects = new List<SceneObjectSaveData>();
        playerInventoryData = new List<PlayerInventoryItemData>();
        flaskData = new FlaskSaveData();
        marketData = new MarketSaveData();
        playerSceneStates = new List<ScenePlayerState>();
    }
}

[System.Serializable]
public class PlayerInventoryItemData
{
    public string itemName;
    public int quantity;
    public int slotIndex;
}

[System.Serializable]
public class PlayerSaveData
{
    public Vector3 position;
    public Vector3 rotation;
    public string currentScene;
    public int money; // Player's current money balance
}

[System.Serializable]
public class ScenePlayerState
{
    public string sceneName;
    public Vector3 position;
    public Vector3 rotation;
}

[System.Serializable]
public class WorldItemSaveData
{
    public string itemName;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
    public int quantity;
    public string sceneName;
    public string persistentId;
    public string origin;
}

[System.Serializable]
public class PlantSaveData
{
    public string itemName;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
    public string sceneName;
    public string plantId; // Unique identifier for each plant
    public bool isCollected; // Whether this plant has been collected
}

[System.Serializable]
public class InventorySaveData
{
    public List<InventorySlotSaveData> slots;
    
    public InventorySaveData()
    {
        slots = new List<InventorySlotSaveData>();
    }
}

[System.Serializable]
public class InventorySlotSaveData
{
    public string itemName;
    public int quantity;
    public int slotIndex;
}

[System.Serializable]
public class FlaskSaveData
{
    public List<FlaskSlotSaveData> slots;
    
    public FlaskSaveData()
    {
        slots = new List<FlaskSlotSaveData>();
    }
}

[System.Serializable]
public class FlaskSlotSaveData
{
    public string itemName;
    public int count;
    public int slotIndex;
    
    public FlaskSlotSaveData()
    {
        itemName = "";
        count = 0;
        slotIndex = -1;
    }
    
    public FlaskSlotSaveData(string name, int cnt, int index)
    {
        itemName = name;
        count = cnt;
        slotIndex = index;
    }
}

[System.Serializable]
public class MarketSaveData
{
    public int playerMoney;
    public List<OfferSaveData> offers;

    public MarketSaveData()
    {
        offers = new List<OfferSaveData>();
    }
}

[System.Serializable]
public class OfferSaveData
{
    public string itemName;
    public int price;
    public int stock;
}

[System.Serializable]
public class SceneObjectSaveData
{
    public string objectId;
    public bool isActive;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
    public string sceneName; // which scene this object belongs to
    // Dictionary yerine List kullanarak JSON serialization problemini çözüyoruz
    public List<string> componentDataKeys;
    public List<string> componentDataValues;
    
    public SceneObjectSaveData()
    {
        componentDataKeys = new List<string>();
        componentDataValues = new List<string>();
    }
    
    // Dictionary benzeri kullanım için helper metodlar
    public void AddComponentData(string key, object value)
    {
        componentDataKeys.Add(key);
        componentDataValues.Add(value?.ToString() ?? "");
    }
    
    public string GetComponentData(string key)
    {
        int index = componentDataKeys.IndexOf(key);
        return index >= 0 ? componentDataValues[index] : "";
    }
}

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance { get; private set; }
    
    [Header("Save Settings")]
    public int maxSaveSlots = 3;
    
    private const string SAVE_PREFIX = "GameSave_";
    private const string SAVE_TIMES_KEY = "SaveTimes";
    private const string SAVE_DIRECTORY_NAME = "GameSaves";
    private const int PLAYER_PREFS_STRING_LIMIT = 16000;
    
    private GameSaveData currentSaveData;
    // Sahne restorasyon sürecinin devam edip etmediğini izlemek için flag
    private bool _isRestoringScene = false;
    public bool IsRestoringScene => _isRestoringScene;
    public float lastRestoreCompletedTime { get; private set; } = -1f;
    [Header("Restore Reliability")]
    [Tooltip("İlk restore sonrası ek tekrar sayısı (problemli objeler için tekrar uygulanır)")] public int extraRestorePasses = 2;
    [Tooltip("Ek restore pasları arası gecikme (s)")] public float extraRestoreDelay = 0.5f;
    [Tooltip("FarmingArea / ObjectCarrying snapshot özetlerini kaydet sırasında logla")] public bool debugLogFarmingSnapshots = true;
    // Scene unload sırasında yok edilmeden hemen önce snapshot alınan world item sahneleri takip et
    private readonly HashSet<string> _pendingWorldItemSnapshots = new HashSet<string>();
    private bool _worldItemSnapshotScheduled;
    private readonly HashSet<string> _worldItemsPendingRemoval = new HashSet<string>();
    private bool _playerPrefsSizeWarningLogged;
    private bool _isSceneUnloading;
    public bool IsSceneUnloading => _isSceneUnloading;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // Sahne değişimi öncesi auto-save için sceneUnloaded event'ini dinle
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SaveOrUpdateScenePlayerState(string sceneName, Vector3 position, Vector3 rotation)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (currentSaveData.playerSceneStates == null)
        {
            currentSaveData.playerSceneStates = new List<ScenePlayerState>();
        }

        var state = currentSaveData.playerSceneStates.FirstOrDefault(s => s != null && s.sceneName == sceneName);
        if (state == null)
        {
            state = new ScenePlayerState { sceneName = sceneName };
            currentSaveData.playerSceneStates.Add(state);
        }

        state.position = position;
        state.rotation = rotation;
    }

    private ScenePlayerState GetScenePlayerState(string sceneName)
    {
        if (currentSaveData?.playerSceneStates == null || string.IsNullOrEmpty(sceneName)) return null;
        return currentSaveData.playerSceneStates.FirstOrDefault(s => s != null && s.sceneName == sceneName);
    }

    public void MarkWorldItemPendingRemoval(string persistentId)
    {
        if (string.IsNullOrEmpty(persistentId)) return;
        _worldItemsPendingRemoval.Add(persistentId);
    }

    public void ConfirmWorldItemRemoval(string persistentId)
    {
        if (string.IsNullOrEmpty(persistentId)) return;
        _worldItemsPendingRemoval.Remove(persistentId);
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _isSceneUnloading = false;

        // MainMenu ve menü sahnelerinde restore yapma
        if (scene.name == "MainMenu" || scene.name.Contains("Menu"))
        {
            Debug.Log($"[GameSaveManager] Skipping restore for menu scene: {scene.name}");
            return;
        }

        // Eğer currentSaveData null ise (ilk başlangıç veya MainMenu'den geliyoruz), en son kaydı yükle
        if (currentSaveData == null)
        {
            var saveTimes = GetSaveTimes();
            if (saveTimes != null && saveTimes.Count > 0)
            {
                string latestSave = saveTimes[saveTimes.Count - 1]; // en son kayıt
                Debug.Log($"[GameSaveManager] No active save data, loading latest save: {latestSave}");
                if (TryLoadJsonPayload(latestSave, out string jsonData))
                {
                    currentSaveData = JsonUtility.FromJson<GameSaveData>(jsonData);
                    Debug.Log($"[GameSaveManager] Loaded save data from {latestSave}");
                }
                else
                {
                    Debug.LogWarning($"[GameSaveManager] Failed to load latest save, starting fresh");
                    currentSaveData = new GameSaveData();
                }
            }
            else
            {
                Debug.Log("[GameSaveManager] No existing saves found, starting fresh");
                currentSaveData = new GameSaveData();
            }
        }

        // CRITICAL: Wait for all objects to initialize (Awake/Start)
        // Build needs more time than editor
        StartCoroutine(RestoreSceneDataDelayed());
    }
    
    private System.Collections.IEnumerator RestoreSceneDataDelayed()
    {
        Debug.Log("[GameSaveManager] Waiting for scene objects to initialize...");
        
        // Wait for end of frame to ensure all Awake calls complete
        yield return new WaitForEndOfFrame();
        
        // EXTRA SAFETY: Wait one more frame for Start methods
        yield return new WaitForEndOfFrame();
        
        Debug.Log("[GameSaveManager] Scene objects initialized, starting restore...");
        RestoreSceneData();
    }
    
    private void OnSceneUnloaded(Scene scene)
    {
        _isSceneUnloading = true;
        Debug.Log($"[GameSaveManager] Scene unloading: {scene.name}");
        // DO NOT call SaveGame here - objects are already destroyed!
        // Door transitions already handle save via OnBeforeSceneChange
        // This event fires AFTER all objects are destroyed, so any save attempt will capture empty state
    }
    
    public void SaveGame(bool forceFullSceneSnapshot = false)
    {
        // Mevcut kayıt üzerine yaz, yeni kayıt oluşturma (scene geçişlerinde)
        string saveTime;
        if (currentSaveData != null && !string.IsNullOrEmpty(currentSaveData.saveTime))
        {
            // Mevcut save'in üzerine yaz
            saveTime = currentSaveData.saveTime;
            Debug.Log($"[GameSaveManager] Updating existing save: {saveTime}");
        }
        else
        {
            // İlk kez kaydediliyor
            saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Debug.Log($"[GameSaveManager] Creating new save: {saveTime}");
        }
        
        string sanitized = SanitizeSaveTime(saveTime);
        // Reuse existing save data to preserve cross-scene state (e.g., collected plants)
        if (currentSaveData == null)
        {
            currentSaveData = new GameSaveData();
        }
        currentSaveData.saveTime = saveTime;
        currentSaveData.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Player data'yı kaydet
        SavePlayerData();
        
        // World items'ları kaydet
        SaveWorldItems();
        
        // Plants'ları kaydet
        SavePlants();
        
        // Inventory'yi kaydet
        SaveInventoryData();
        
        // Flask data'sını kaydet
        SaveFlaskData();

    // Market data'sını kaydet
    SaveMarketData();
        
        // Scene objects'leri kaydet
        if (forceFullSceneSnapshot)
        {
            CaptureSceneObjectsSnapshotNow();
        }
        else
        {
            SaveSceneObjects();
        }
        
    // Save data'yı JSON'a çevir ve disk + PlayerPrefs'e kaydet
        string jsonData = JsonUtility.ToJson(currentSaveData, true);
        PersistSavePayload(saveTime, sanitized, jsonData);
        
        // Save times listesini güncelle
        UpdateSaveTimesList(saveTime);
        
        // CRITICAL: PlayerPrefs.Save() disk'e yazmayı zorlar (build'de önemli)
        PlayerPrefs.Save();
        
        Debug.Log($"[GameSaveManager] Game saved successfully at {saveTime} - JSON size: {jsonData.Length} bytes");
    }
    
    public void LoadGame(string saveTime)
    {
        if (!TryLoadJsonPayload(saveTime, out string jsonData))
        {
            Debug.LogError($"Save data not found for time: {saveTime}");
            return;
        }
        currentSaveData = JsonUtility.FromJson<GameSaveData>(jsonData);
        
        if (currentSaveData == null)
        {
            Debug.LogError("Failed to parse save data!");
            return;
        }
        
        // Hedef sahneye geç
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != currentSaveData.sceneName)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentSaveData.sceneName);
        }
        else
        {
            // Aynı sahnedeyse direkt restore et
            RestoreSceneData();
        }
        
        Debug.Log($"Game loaded from {saveTime}");
    }
    
    private void SavePlayerData()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (currentSaveData.playerData == null)
            {
                currentSaveData.playerData = new PlayerSaveData();
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Vector3 position = player.transform.position;
            Vector3 rotation = player.transform.eulerAngles;

            currentSaveData.playerData.position = position;
            currentSaveData.playerData.rotation = rotation;
            currentSaveData.playerData.currentScene = sceneName;

            SaveOrUpdateScenePlayerState(sceneName, position, rotation);
            Debug.Log($"[GameSaveManager] Saved player data for scene {sceneName}: pos={position}, rot={rotation}");
            
            // Save player's money
            if (MoneyManager.Instance != null)
            {
                currentSaveData.playerData.money = MoneyManager.Instance.Balance;
            }
        }
        else
        {
            Debug.LogWarning("[GameSaveManager] SavePlayerData: Player not found!");
        }
    }
    
    private void SaveWorldItems()
    {
        _worldItemSnapshotScheduled = false;
        if (currentSaveData.worldItems == null) currentSaveData.worldItems = new List<WorldItemSaveData>();

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool hasPendingSnapshot = _pendingWorldItemSnapshots.Contains(currentScene);

        var worldItems = FindObjectsOfType<WorldItem>();
        bool hasLiveWorldItems = worldItems != null && worldItems.Length > 0;

        // Eğer sahnede canlı world item yoksa ve OnDestroy ile snapshot alındıysa, mevcut kaydı koru
        if (!hasLiveWorldItems && hasPendingSnapshot)
        {
            _pendingWorldItemSnapshots.Remove(currentScene);
            Debug.Log($"[SaveWorldItems] No live world items found but preserved pending snapshot for scene {currentScene}");
            return;
        }

        // Yeni snapshot alınacağından, mevcut sahne kayıtlarını temizle
        currentSaveData.worldItems.RemoveAll(w => w != null && w.sceneName == currentScene);

        var newEntries = new List<WorldItemSaveData>();
        var seenIds = new HashSet<string>();
        var presentIds = new HashSet<string>();

        if (worldItems != null)
        {
            foreach (WorldItem worldItem in worldItems)
            {
                if (worldItem == null || worldItem.item == null) continue;
                // Skip if this world item is actually a plant container
                if (worldItem.GetComponent<Plant>() != null || worldItem.GetComponentInParent<Plant>() != null)
                {
                    continue;
                }
                string persistentId = worldItem.PersistentId;
                if (!string.IsNullOrEmpty(persistentId))
                {
                    presentIds.Add(persistentId);
                    if (_worldItemsPendingRemoval.Contains(persistentId))
                    {
                        continue;
                    }
                    if (!seenIds.Add(persistentId))
                    {
                        continue;
                    }
                }
                newEntries.Add(new WorldItemSaveData
                {
                    itemName = worldItem.item.itemName,
                    position = worldItem.transform.position,
                    rotation = worldItem.transform.eulerAngles,
                    scale = worldItem.transform.localScale,
                    quantity = worldItem.quantity,
                    sceneName = currentScene,
                    persistentId = persistentId,
                    origin = worldItem.Origin.ToString()
                });
            }
        }

        currentSaveData.worldItems.AddRange(newEntries);
        _pendingWorldItemSnapshots.Remove(currentScene);
        Debug.Log($"[SaveWorldItems] Snapshot updated for scene {currentScene} with {newEntries.Count} entries");

        if (_worldItemsPendingRemoval.Count > 0)
        {
            var confirmedIds = new List<string>();
            foreach (var pendingId in _worldItemsPendingRemoval)
            {
                if (!presentIds.Contains(pendingId))
                {
                    confirmedIds.Add(pendingId);
                }
            }

            if (confirmedIds.Count > 0)
            {
                foreach (var id in confirmedIds)
                {
                    _worldItemsPendingRemoval.Remove(id);
                    Debug.Log($"[SaveWorldItems] Confirmed removal for world item {id}");
                }
            }
        }
    }

    private System.Collections.IEnumerator SaveWorldItemsDeferred()
    {
        yield return null; // bir frame bekle, Destroy edilen objeler temizlensin
        _worldItemSnapshotScheduled = false;
        SaveWorldItems();
    }

    public void RequestWorldItemSnapshotRefresh()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_worldItemSnapshotScheduled) return;
        _worldItemSnapshotScheduled = true;
        StartCoroutine(SaveWorldItemsDeferred());
    }
    
    private void SavePlants()
    {
        if (currentSaveData == null)
        {
            Debug.LogError("Cannot save plants: currentSaveData is null");
            return;
        }
        
        // Önce mevcut plant verilerini koru, sadece bu sahnedeki bitkileri güncelle
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Bu sahnedeki collected bitkileri koru
        List<PlantSaveData> collectedPlantsInScene = currentSaveData.plants.FindAll(p => p.sceneName == currentScene && p.isCollected);
        
        // Bu sahnedeki tüm plant verilerini temizle
        currentSaveData.plants.RemoveAll(p => p.sceneName == currentScene);
        
        // Collected bitkileri geri ekle
        currentSaveData.plants.AddRange(collectedPlantsInScene);
        
        // Mevcut sahnedeki bitkileri kaydet
        Plant[] plants = FindObjectsOfType<Plant>();
        foreach (Plant plant in plants)
        {
            if (plant.item != null)
            {
                // Compute plantId consistently
                string thisPlantId = MakePlantId(plant.item.itemName, plant.transform.position);
                var alreadyCollected = currentSaveData.plants.Any(p => p.sceneName == currentScene && p.plantId == thisPlantId && p.isCollected);
                // Also skip if an identical non-collected entry already exists (avoid duplicates on repeated saves)
                var alreadySaved = currentSaveData.plants.Any(p => p.sceneName == currentScene && p.plantId == thisPlantId && !p.isCollected);
                if (alreadyCollected)
                {
                    // Don’t re-add this plant to save list
                    continue;
                }
                if (alreadySaved)
                {
                    // Already tracked as present; avoid duplicate entries
                    continue;
                }

                PlantSaveData plantData = new PlantSaveData();
                plantData.itemName = plant.item.itemName;
                plantData.position = plant.transform.position;
                plantData.rotation = plant.transform.eulerAngles;
                plantData.scale = plant.transform.localScale;
                plantData.sceneName = currentScene;
                
                // Unique ID oluştur (pozisyon ve item name'e göre)
                plantData.plantId = thisPlantId;
                plantData.isCollected = false;
                
                currentSaveData.plants.Add(plantData);
//                Debug.Log($"Saved plant: {plantData.itemName} at {plantData.position} with ID: {plantData.plantId}");
            }
        }
        
   //     Debug.Log($"Total plants in current scene: {plants.Length}, Total plants saved across all scenes: {currentSaveData.plants.Count}");
    }
    
    private void SaveInventoryData()
    {
        currentSaveData.inventoryData = new InventorySaveData();
        
        if (InventoryManager.Instance != null)
        {
            SCInventory inventory = InventoryManager.Instance.GetPlayerInventory();
            Debug.Log($"Saving inventory with {inventory.inventorySlots.Count} slots");
            
            for (int i = 0; i < inventory.inventorySlots.Count; i++)
            {
                Slot slot = inventory.inventorySlots[i];
                if (slot != null && slot.item != null && slot.itemCount > 0)
                {
                    InventorySlotSaveData slotData = new InventorySlotSaveData();
                    slotData.itemName = slot.item.itemName;
                    slotData.quantity = slot.itemCount;
                    slotData.slotIndex = i;
                    
                    currentSaveData.inventoryData.slots.Add(slotData);
                    Debug.Log($"Saved item: {slot.item.itemName} x{slot.itemCount} in slot {i}");
                }
            }
            
            Debug.Log($"Total items saved: {currentSaveData.inventoryData.slots.Count}");
        }
        else
        {
            Debug.LogWarning("InventoryManager.Instance is null, cannot save inventory");
        }
    }
    
    private void SaveFlaskData()
    {
        Debug.Log("Saving Flask data...");
        // Find FlaskManager in the scene
        FlaskManager flaskManager = FindObjectOfType<FlaskManager>();
        if (flaskManager != null)
        {
            Debug.Log($"Found FlaskManager, saving {flaskManager.storedSlots.Count} slots");
            if (currentSaveData.flaskData == null)
            {
                currentSaveData.flaskData = new FlaskSaveData();
            }
            if (currentSaveData.flaskData.slots == null)
            {
                currentSaveData.flaskData.slots = new List<FlaskSlotSaveData>();
            }
            // Clear only when we are about to populate with fresh data
            currentSaveData.flaskData.slots.Clear();
            
            for (int i = 0; i < flaskManager.storedSlots.Count; i++)
            {
                var slot = flaskManager.storedSlots[i];
                if (slot != null && slot.item != null && slot.count > 0)
                {
                    FlaskSlotSaveData slotData = new FlaskSlotSaveData(
                        slot.item.itemName, 
                        slot.count, 
                        i
                    );
                    currentSaveData.flaskData.slots.Add(slotData);
                    Debug.Log($"Saved Flask slot {i}: {slot.item.itemName} x{slot.count}");
                }
            }
            
            Debug.Log($"Total Flask slots saved: {currentSaveData.flaskData.slots.Count}");
        }
        else
        {
            Debug.LogWarning("FlaskManager not found in scene, keeping previous Flask data snapshot");
        }
    }

    // Write-only API for components (like FlaskManager) to push their state before destruction
    public void CaptureFlaskState(FlaskManager flaskManager)
    {
        if (flaskManager == null) return;
        if (currentSaveData == null) currentSaveData = new GameSaveData();
        if (currentSaveData.flaskData == null) currentSaveData.flaskData = new FlaskSaveData();
        if (currentSaveData.flaskData.slots == null) currentSaveData.flaskData.slots = new List<FlaskSlotSaveData>();

        currentSaveData.flaskData.slots.Clear();
        for (int i = 0; i < flaskManager.storedSlots.Count; i++)
        {
            var slot = flaskManager.storedSlots[i];
            if (slot != null && slot.item != null && slot.count > 0)
            {
                currentSaveData.flaskData.slots.Add(new FlaskSlotSaveData(slot.item.itemName, slot.count, i));
            }
        }
        Debug.Log($"[GameSaveManager] Captured Flask state with {currentSaveData.flaskData.slots.Count} filled slots");
    }

    private void SaveSceneObjects(bool incremental = false)
    {
        if (currentSaveData == null)
        {
            Debug.LogError("[SaveSceneObjects] currentSaveData is NULL - cannot save!");
            return;
        }
        
        // Collect all ISaveable components first
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // CRITICAL: Find all MonoBehaviours first, then filter ISaveable
        // This is more reliable than FindObjectsOfType<ISaveable>() in build
        var allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true); // include inactive
        var saveableComponents = allMonoBehaviours.OfType<ISaveable>().ToArray();
        
        Debug.Log($"[SaveSceneObjects] Scene: {currentScene}");
        Debug.Log($"[SaveSceneObjects] Total MonoBehaviours: {allMonoBehaviours.Length}");
        Debug.Log($"[SaveSceneObjects] ISaveable components: {saveableComponents.Length}");
        
        if (saveableComponents.Length == 0)
        {
            // Incremental modda boş bulduysak eski kayıtları KORU (örn: sahne unload sırasında son objeler yok olurken çağrılmış olabilir)
            if (!incremental)
            {
                Debug.LogWarning($"[SaveSceneObjects] WARNING: No ISaveable components found in {currentScene}!");
                Debug.LogWarning("[SaveSceneObjects] This may indicate timing issues or missing interfaces.");
            }
            return;
        }
        
        var byObject = new Dictionary<GameObject, List<ISaveable>>();
        foreach (var comp in saveableComponents)
        {
            var mb = comp as MonoBehaviour;
            if (mb == null) continue;
            var go = mb.gameObject;
            if (!byObject.TryGetValue(go, out var list))
            {
                list = new List<ISaveable>();
                byObject[go] = list;
            }
            list.Add(comp);
        }

        var newEntries = new List<SceneObjectSaveData>();
        foreach (var kvp in byObject)
        {
            var go = kvp.Key;
            var comps = kvp.Value;

            var sod = new SceneObjectSaveData();
            sod.objectId = TryGetStableObjectId(go, comps);
            sod.isActive = go.activeSelf;
            sod.position = go.transform.position;
            sod.rotation = go.transform.eulerAngles;
            sod.scale = go.transform.localScale;
            sod.sceneName = currentScene;

            bool isHarrow = go.GetComponent<HarrowManager>() != null;
            bool isFarming = go.GetComponent<FarmingAreaManager>() != null;
            if (isHarrow || isFarming)
            {
                Debug.Log($"[SaveSceneObjects] {go.name} -> id={sod.objectId}, pos={sod.position}, comps={string.Join(",", comps.Select(c=>c.GetType().Name))}");
            }

            foreach (var comp in comps)
            {
                var typeName = comp.GetType().Name;
                Dictionary<string, object> data = null;
                try { data = comp.GetSaveData(); }
                catch (Exception ex)
                {
                    Debug.LogError($"Save error in {typeName} on {go.name}: {ex.Message}");
                    continue;
                }
                if (data == null) continue;
                
                if (isHarrow || isFarming)
                {
                    Debug.Log($"[SaveSceneObjects] {go.name}.{typeName} save data keys: {string.Join(", ", data.Keys)}");
                }
                
                foreach (var entry in data)
                {
                    string key = $"{typeName}.{entry.Key}";
                    string value = SerializeValue(entry.Value);
                    sod.AddComponentData(key, value);
                }
            }

            newEntries.Add(sod);
        }

        if (newEntries.Count > 0)
        {
            if (incremental)
            {
                // Merge: var olanları güncelle, yoksa ekle; hiç bir şeyi topluca silme
                int updates = 0, adds = 0;
                foreach (var e in newEntries)
                {
                    int idx = currentSaveData.sceneObjects.FindIndex(s => s != null && s.objectId == e.objectId && (string.IsNullOrEmpty(s.sceneName) || s.sceneName == e.sceneName));
                    if (idx >= 0)
                    {
                        currentSaveData.sceneObjects[idx] = e; updates++;
                    }
                    else
                    {
                        currentSaveData.sceneObjects.Add(e); adds++;
                    }
                }
                Debug.Log($"[SaveSceneObjects] Incremental snapshot: {updates} updated, {adds} added (total now {currentSaveData.sceneObjects.Count})");
            }
            else
            {
                // Replace only if we actually found something; prevents wiping on scene unload
                int removed = currentSaveData.sceneObjects.RemoveAll(s => s != null && (string.IsNullOrEmpty(s.sceneName) || s.sceneName == currentScene));
                currentSaveData.sceneObjects.AddRange(newEntries);
                Debug.Log($"[SaveSceneObjects] Replaced {removed} entries for scene '{currentScene}' with {newEntries.Count} new entries. Total now: {currentSaveData.sceneObjects.Count}");
            }
        }
        else if(!incremental)
        {
            Debug.LogWarning($"[SaveSceneObjects] Found 0 ISaveable objects for scene '{currentScene}'. Keeping previous saved entries to avoid data loss.");
        }
        if (!incremental && debugLogFarmingSnapshots)
        {
            LogFarmingAreaSnapshotSummary(currentScene);
        }
    }

    private void LogFarmingAreaSnapshotSummary(string currentScene)
    {
        try
        {
            int famCount = 0;
            foreach (var sod in currentSaveData.sceneObjects)
            {
                if (sod == null) continue;
                if (!string.IsNullOrEmpty(sod.sceneName) && sod.sceneName != currentScene) continue;
                bool hasFamKey = sod.componentDataKeys != null && sod.componentDataKeys.Exists(k => k.StartsWith("FarmingAreaManager.Plot0."));
                if (!hasFamKey) continue;
                famCount++;
                int prepared = 0, occupied = 0, ready = 0, growing = 0, waiting = 0;
                foreach (var k in sod.componentDataKeys)
                {
                    if (!k.StartsWith("FarmingAreaManager.Plot")) continue;
                    if (k.EndsWith("prepared")) prepared += GetBoolFromSod(sod, k) ? 1 : 0;
                    else if (k.EndsWith("occupied")) occupied += GetBoolFromSod(sod, k) ? 1 : 0;
                    else if (k.EndsWith("ready")) ready += GetBoolFromSod(sod, k) ? 1 : 0;
                    else if (k.EndsWith("growing")) growing += GetBoolFromSod(sod, k) ? 1 : 0;
                    else if (k.EndsWith("waiting")) waiting += GetBoolFromSod(sod, k) ? 1 : 0;
                }
                Debug.Log($"[SaveSceneObjects][FarmingSummary] id={sod.objectId} prepared={prepared} occupied={occupied} growing={growing} waiting={waiting} ready={ready}");
            }
            Debug.Log($"[SaveSceneObjects][FarmingSummary] Total FarmingArea snapshots: {famCount}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveSceneObjects][FarmingSummary] Logging failed: {ex.Message}");
        }
    }

    private bool GetBoolFromSod(SceneObjectSaveData sod, string key)
    {
        int idx = sod.componentDataKeys.IndexOf(key);
        if (idx < 0) return false;
        var val = sod.componentDataValues[idx];
        return val == "true" || val == "True" || val == "1";
    }

    private void SaveMarketData()
    {
        if (currentSaveData == null) currentSaveData = new GameSaveData();
        if (currentSaveData.marketData == null) currentSaveData.marketData = new MarketSaveData();

        var market = FindObjectOfType<MarketManager>();
        // Always save money even if there's no Market component in this scene
        if (MoneyManager.Instance != null)
            currentSaveData.marketData.playerMoney = MoneyManager.Instance.Balance;
        else if (market != null)
            currentSaveData.marketData.playerMoney = market.playerMoney;
        // else keep previous value if neither exists

        // Offers only if a Market is present
        currentSaveData.marketData.offers.Clear();
        if (market != null)
        {
            var offers = GetActiveOffers(market);
            if (offers != null)
            {
                foreach (var o in offers)
                {
                    var save = new OfferSaveData { itemName = o.item?.itemName ?? string.Empty, price = o.price, stock = o.stock };
                    currentSaveData.marketData.offers.Add(save);
                }
            }
        }
    }

    private void RestoreMarketData()
    {
        if (currentSaveData?.marketData == null) return;
        var market = FindObjectOfType<MarketManager>();

        // Restore shared MoneyManager if available; also set market fallback to keep UI consistent
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.SetBalance(currentSaveData.marketData.playerMoney);
        if (market != null)
        {
            market.playerMoney = currentSaveData.marketData.playerMoney;
            market.ApplySavedOffers(currentSaveData.marketData.offers);
        }
    }

    // Reflection helpers to avoid tight coupling with MarketManager's internal Offer type
    private class OfferShim { public SCItem item; public int price; public int stock; }
    private List<OfferShim> GetActiveOffers(MarketManager market)
    {
        try
        {
            var fi = typeof(MarketManager).GetField("_activeOffers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var list = fi?.GetValue(market) as System.Collections.IEnumerable;
            if (list == null) return null;
            var result = new List<OfferShim>();
            foreach (var it in list)
            {
                var t = it.GetType();
                var item = t.GetField("item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(it) as SCItem;
                var price = (int)(t.GetField("price", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(it) ?? 0);
                var stock = (int)(t.GetField("stock", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(it) ?? 0);
                result.Add(new OfferShim { item = item, price = price, stock = stock });
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GetActiveOffers reflection failed: {e.Message}");
            return null;
        }
    }
    
    private void RestoreSceneData()
    {
        if (currentSaveData == null)
        {
            Debug.LogWarning("[GameSaveManager] RestoreSceneData called but currentSaveData is null!");
            return;
        }
        // Restorasyon başladı
        _isRestoringScene = true;
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[GameSaveManager] Starting scene data restoration for: {currentScene}");
        
        // Player pozisyonunu restore et
        RestorePlayerData();
        
    // Mevcut world item'ları temizle
    ClearExistingWorldItems();
        
    // Plant'ları önce restore et (FarmingAreaManager'ın bulması için)
    RestorePlants();
        
    // Scene object'leri restore et (FarmingAreaManager/HarrowManager gibi ISaveable'lar)
    RestoreSceneObjects();
        
    // World item'ları en son restore et (bitki pozisyonlarıyla çakışmayı azaltmak için)
    RestoreWorldItems();
        
        // Inventory'yi restore et (biraz gecikme ile)
        Invoke(nameof(DelayedInventoryRestore), 0.2f);
        
        // Flask data'sını restore et (inventory'den sonra)
        Invoke(nameof(DelayedFlaskRestore), 0.3f);

    // Market data'sını restore et (UI & camera hazırken)
    Invoke(nameof(DelayedMarketRestore), 0.35f);
        
        // Bazı renderer'lar için MPB uygulaması ilk frame'de gecikebilir; görselleri nudge et
        Invoke(nameof(PostRestoreNudge), 0.1f);
        Debug.Log($"[GameSaveManager] Scene data restoration completed for: {currentScene}");
        // Restorasyon bitti
        _isRestoringScene = false;
        lastRestoreCompletedTime = Time.realtimeSinceStartup;
        if (extraRestorePasses > 0 && gameObject.activeInHierarchy)
        {
            StartCoroutine(PerformExtraRestorePasses());
        }
    }

    private System.Collections.IEnumerator PerformExtraRestorePasses()
    {
        for (int i = 0; i < extraRestorePasses; i++)
        {
            yield return new WaitForSeconds(extraRestoreDelay);
            if (currentSaveData == null) yield break;
            Debug.Log($"[GameSaveManager] Extra restore pass {i+1}/{extraRestorePasses} starting...");
            _isRestoringScene = true;
            try
            {
                RestoreSceneObjects();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameSaveManager] Extra restore pass {i+1} failed: {ex.Message}");
            }
            _isRestoringScene = false;
        }
    }

    private void PostRestoreNudge()
    {
        // Soil görsellerini tekrar tetikleyelim
        foreach (var fam in FindObjectsOfType<FarmingAreaManager>())
        {
            var mi = typeof(FarmingAreaManager).GetMethod("UpdateAllSoilPlaceVisuals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            try { mi?.Invoke(fam, null); } catch {}
        }
    }
    
    private void DelayedInventoryRestore()
    {
        RestoreInventoryData();
    }
    
    private void DelayedFlaskRestore()
    {
        RestoreFlaskData();
    }

    private void DelayedMarketRestore()
    {
        RestoreMarketData();
    }
    
    private void RestorePlayerData()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[GameSaveManager] RestorePlayerData: Player not found!");
            return;
        }

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        ScenePlayerState sceneState = GetScenePlayerState(currentScene);

        Vector3? targetPosition = null;
        Vector3? targetRotation = null;

        if (sceneState != null)
        {
            targetPosition = sceneState.position;
            targetRotation = sceneState.rotation;
            Debug.Log($"[GameSaveManager] Restoring player from scene-specific state for {currentScene}: pos={targetPosition}, rot={targetRotation}");
        }
        else if (currentSaveData.playerData != null && currentSaveData.playerData.currentScene == currentScene)
        {
            targetPosition = currentSaveData.playerData.position;
            targetRotation = currentSaveData.playerData.rotation;
            Debug.Log($"[GameSaveManager] Restoring player from global playerData for {currentScene}: pos={targetPosition}, rot={targetRotation}");
        }
        else
        {
            Debug.LogWarning($"[GameSaveManager] No saved position found for scene {currentScene}. Player will stay at default position.");
        }

        if (targetPosition.HasValue)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.position = targetPosition.Value;
            if (targetRotation.HasValue)
            {
                player.transform.eulerAngles = targetRotation.Value;
            }

            if (controller != null)
            {
                controller.enabled = true;
            }
            
            Debug.Log($"[GameSaveManager] Player position restored to: {player.transform.position}");
        }

        if (currentSaveData.playerData != null && MoneyManager.Instance != null)
        {
            MoneyManager.Instance.SetBalance(currentSaveData.playerData.money);
        }
    }
    
    private void ClearExistingWorldItems()
    {
        // Mevcut world item'ları sil (Plant hiyerarşisine ait olanları asla silme)
        WorldItem[] existingWorldItems = FindObjectsOfType<WorldItem>();
        foreach (WorldItem wi in existingWorldItems)
        {
            if (wi == null) continue;
            bool isPlantRelated = wi.GetComponent<Plant>() != null || wi.GetComponentInParent<Plant>() != null || wi.GetComponentInChildren<Plant>() != null;
            if (isPlantRelated) continue; // Bitkiler korunmalı
            DestroyImmediate(wi.gameObject);
        }
        
        DroppedItem[] existingDroppedItems = FindObjectsOfType<DroppedItem>();
        foreach (DroppedItem di in existingDroppedItems)
        {
            if (di == null) continue;
            bool isPlantRelated = di.GetComponent<Plant>() != null || di.GetComponentInParent<Plant>() != null || di.GetComponentInChildren<Plant>() != null;
            if (isPlantRelated) continue; // Bitkiler korunmalı
            DestroyImmediate(di.gameObject);
        }
    }
    
    private void RestoreWorldItems()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        // Build a set of plant signatures to avoid restoring overlapping world items
        var plantKeys = new HashSet<string>();
        if (currentSaveData.plants != null)
        {
            foreach (var p in currentSaveData.plants)
            {
                if (p.sceneName != currentScene || p.isCollected) continue;
                var rx = Mathf.Round(p.position.x * 100f) / 100f;
                var ry = Mathf.Round(p.position.y * 100f) / 100f;
                var rz = Mathf.Round(p.position.z * 100f) / 100f;
                plantKeys.Add($"{p.itemName}_{rx}_{ry}_{rz}");
            }
        }
        
        foreach (WorldItemSaveData itemData in currentSaveData.worldItems)
        {
            if (itemData.sceneName != currentScene) continue;

            // Skip if this world item overlaps a saved plant (same item and near-same position)
            var kx = Mathf.Round(itemData.position.x * 100f) / 100f;
            var ky = Mathf.Round(itemData.position.y * 100f) / 100f;
            var kz = Mathf.Round(itemData.position.z * 100f) / 100f;
            string key = $"{itemData.itemName}_{kx}_{ky}_{kz}";
            if (plantKeys.Contains(key))
            {
                continue;
            }

            SCItem item = FindItemByName(itemData.itemName);
            if (item == null) continue;

            var origin = ParseWorldItemOrigin(itemData.origin);
            bool preferDrop = origin == WorldItem.WorldItemOrigin.ManualDrop;
            Quaternion rotation = Quaternion.Euler(itemData.rotation);
            Vector3 scale = itemData.scale == Vector3.zero ? Vector3.one : itemData.scale;

            WorldItem spawned = WorldItemSpawner.SpawnItem(
                item,
                itemData.position,
                Mathf.Max(1, itemData.quantity),
                string.IsNullOrEmpty(itemData.persistentId) ? null : itemData.persistentId,
                origin,
                rotation,
                scale,
                preferDrop);

            if (spawned == null) continue;

            // Ensure transform precisely matches saved values after overrides (guard against prefabs overriding scale)
            spawned.transform.position = itemData.position;
            spawned.transform.rotation = rotation;
            spawned.transform.localScale = scale;
        }
    }

    private static WorldItem.WorldItemOrigin ParseWorldItemOrigin(string raw)
    {
        if (!string.IsNullOrEmpty(raw) && Enum.TryParse(raw, out WorldItem.WorldItemOrigin parsed))
        {
            return parsed;
        }
        return WorldItem.WorldItemOrigin.ScenePlaced;
    }
    
    private void ClearExistingPlants()
    {
        // Bu metodu artık kullanmayacağız
        // Bitkileri akıllı bir şekilde restore edeceğiz
        Debug.Log("ClearExistingPlants called but doing nothing - using smart plant restoration instead");
    }
    
    private void RestorePlants()
    {
        if (currentSaveData == null || currentSaveData.plants == null)
        {
            Debug.Log("No plant data to restore");
            return;
        }
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
    // Mevcut sahnedeki bitkilerin ID'lerini topla
        Plant[] existingPlants = FindObjectsOfType<Plant>();
        HashSet<string> existingPlantIds = new HashSet<string>();
        
        foreach (Plant plant in existingPlants)
        {
            if (plant.item != null)
            {
    string plantId = MakePlantId(plant.item.itemName, plant.transform.position);
                existingPlantIds.Add(plantId);
            }
        }

        // 1) Bu sahnede collected olarak işaretlenen bitkileri sahneden kaldır
        foreach (var savedPlant in currentSaveData.plants)
        {
            if (savedPlant.sceneName != currentScene || !savedPlant.isCollected) continue;
            // Find matching live plant by id
            for (int i = 0; i < existingPlants.Length; i++)
            {
                var p = existingPlants[i]; if (p == null || p.item == null) continue;
                string pid = MakePlantId(p.item.itemName, p.transform.position);
                if (pid == savedPlant.plantId)
                {
                    DestroyImmediate(p.gameObject);
                    break;
                }
            }
        }
        
        // Save edilen bitkileri kontrol et
        foreach (PlantSaveData plantData in currentSaveData.plants)
        {
            if (plantData.sceneName == currentScene && !plantData.isCollected)
            {
                // Skip plants that are near farming area plots (FarmingAreaManager will handle these)
                bool nearFarmingPlot = false;
                foreach (var fam in FindObjectsOfType<FarmingAreaManager>())
                {
                    foreach (var plotPoint in fam.plotPoints)
                    {
                        if (plotPoint != null)
                        {
                            float distance = Vector3.Distance(plantData.position, plotPoint.position);
                            if (distance < 1.0f) // Within 1 meter of a plot point
                            {
                                nearFarmingPlot = true;
                                break;
                            }
                        }
                    }
                    if (nearFarmingPlot) break;
                }
                if (nearFarmingPlot) continue;
                
                // Bu bitki zaten sahnede var mı kontrol et
                if (!existingPlantIds.Contains(plantData.plantId))
                {
                    // Bitki sahnede yok, restore et
                    SCItem item = FindItemByName(plantData.itemName);
                    if (item != null)
                    {
                        GameObject plantObject = null;
                        
                        // Önce itemPrefab'ı dene (sabit bitki için)
                        if (item.itemPrefab != null)
                        {
                            plantObject = Instantiate(item.itemPrefab, plantData.position, Quaternion.Euler(plantData.rotation));
                        }
                        // Eğer itemPrefab yoksa dropPrefab'ı kullan ama Rigidbody'yi kaldır
                        else if (item.dropPrefab != null)
                        {
                            plantObject = Instantiate(item.dropPrefab, plantData.position, Quaternion.Euler(plantData.rotation));
                            
                            // Rigidbody varsa kaldır veya kinematic yap
                            Rigidbody rb = plantObject.GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                rb.isKinematic = true; // Rigidbody'yi kinematic yap (fizik simülasyonu yapmasın)
                                rb.useGravity = false; // Yerçekimini kapat
                                Debug.Log($"Made rigidbody kinematic for restored plant: {plantData.itemName}");
                            }
                            // Remove WorldItem/DroppedItem components so this plant won't be saved as a world item later
                            var wis = plantObject.GetComponentsInChildren<WorldItem>(true);
                            foreach (var wi in wis) Destroy(wi);
                            var drops = plantObject.GetComponentsInChildren<DroppedItem>(true);
                            foreach (var d in drops) Destroy(d);
                        }
                        
                        if (plantObject != null)
                        {
                            plantObject.transform.localScale = plantData.scale;
                            
                            // Plant component'ını kontrol et ve ayarla
                            Plant plantComponent = plantObject.GetComponent<Plant>();
                            if (plantComponent == null)
                            {
                                plantComponent = plantObject.AddComponent<Plant>();
                            }
                            else
                            {
                                // Ensure there is exactly one Plant component
                                var extras = plantObject.GetComponents<Plant>();
                                for (int i = 1; i < extras.Length; i++) Destroy(extras[i]);
                            }
                            plantComponent.item = item;
                            
                            Debug.Log($"Restored plant: {plantData.itemName} at {plantData.position} using {(item.itemPrefab != null ? "itemPrefab" : "dropPrefab")}");
                        }
                        else
                        {
                            Debug.LogError($"No suitable prefab found for plant: {plantData.itemName}");
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Plant restoration completed for scene: {currentScene}");
    }

    // Consistent plant ID builder (2 decimals, invariant)
    private static string MakePlantId(string itemName, Vector3 pos)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        float rx = Mathf.Round(pos.x * 100f) / 100f;
        float ry = Mathf.Round(pos.y * 100f) / 100f;
        float rz = Mathf.Round(pos.z * 100f) / 100f;
        return string.Concat(itemName, "_",
            rx.ToString("F2", ci), "_",
            ry.ToString("F2", ci), "_",
            rz.ToString("F2", ci));
    }
    
    private void RestoreInventoryData()
    {
        Debug.Log("=== STARTING INVENTORY RESTORATION ===");
        
        // Önce yeni format'ı kontrol et
        if (currentSaveData?.playerInventoryData != null && currentSaveData.playerInventoryData.Count > 0)
        {
            Debug.Log($"Restoring inventory with NEW format - {currentSaveData.playerInventoryData.Count} saved items");
            RestoreNewFormatInventory();
        }
        // Eski format'ı kontrol et
        else if (currentSaveData?.inventoryData != null && currentSaveData.inventoryData.slots.Count > 0)
        {
            Debug.Log($"Restoring inventory with OLD format - {currentSaveData.inventoryData.slots.Count} saved items");
            RestoreOldFormatInventory();
        }
        else
        {
            Debug.LogWarning("No inventory data to restore");
        }
        
        Debug.Log("=== INVENTORY RESTORATION COMPLETED ===");
    }
    
    private void RestoreNewFormatInventory()
    {
        var inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogError("InventoryManager instance not found!");
            return;
        }
        
        var inventory = inventoryManager.GetPlayerInventory();
        if (inventory == null)
        {
            Debug.LogError("Player inventory not found!");
            return;
        }
        
        Debug.Log($"Current inventory has {inventory.slots.Count} slots");
        
        // Önce inventory'yi temizle
        inventory.ResetInventory();
        Debug.Log("Inventory reset completed");
        
        // Her bir item'ı restore et
        foreach (var itemData in currentSaveData.playerInventoryData)
        {
            Debug.Log($"Attempting to restore: {itemData.itemName} x{itemData.quantity} to slot {itemData.slotIndex}");
            
            // Item'ı FindItemByName ile yükle (SCItem döner)
            SCItem item = FindItemByName(itemData.itemName);
            if (item != null)
            {
                // Item'ı inventory'ye ekle
                bool added = inventory.AddItem(item, itemData.quantity);
                if (added)
                {
                    Debug.Log($"Successfully restored: {itemData.itemName} x{itemData.quantity}");
                }
                else
                {
                    Debug.LogError($"Failed to restore: {itemData.itemName} x{itemData.quantity}");
                }
            }
            else
            {
                Debug.LogError($"Could not find item: {itemData.itemName}");
            }
        }
        
        // UI'ı güncelle
        inventoryManager.RefreshInventoryUI();
    }
    
    private void RestoreOldFormatInventory()
    {
        if (InventoryManager.Instance != null)
        {
            SCInventory inventory = InventoryManager.Instance.GetPlayerInventory();
            
            // Önce inventory'yi temizle
            inventory.ResetInventory();
            Debug.Log("Inventory reset completed");
            
            // Save edilen item'ları geri yükle
            foreach (InventorySlotSaveData slotData in currentSaveData.inventoryData.slots)
            {
                SCItem item = FindItemByName(slotData.itemName);
                if (item != null)
                {
                    // Her item'ı tek tek ekle (stacking otomatik olacak)
                    for (int i = 0; i < slotData.quantity; i++)
                    {
                        bool added = inventory.AddItem(item);
                        if (!added)
                        {
                            Debug.LogWarning($"Could not add item {item.itemName} - inventory might be full");
                            break;
                        }
                    }
                    Debug.Log($"Restored item: {item.itemName} x{slotData.quantity}");
                }
                else
                {
                    Debug.LogError($"Could not find item: {slotData.itemName}");
                }
            }
            
            // UI'ı güncelle
            InventoryManager.Instance.RefreshInventoryUI();
            Debug.Log("Inventory UI refreshed");
        }
        else
        {
            Debug.LogError("InventoryManager.Instance is null, cannot restore inventory");
        }
    }
    
    private void RestoreFlaskData()
    {
        Debug.Log("=== STARTING FLASK RESTORATION ===");
        
        if (currentSaveData?.flaskData == null || 
            currentSaveData.flaskData.slots == null || 
            currentSaveData.flaskData.slots.Count == 0)
        {
            Debug.LogWarning("No Flask data to restore");
            return;
        }
        
        // Find FlaskManager in the scene
        FlaskManager flaskManager = FindObjectOfType<FlaskManager>();
        if (flaskManager == null)
        {
            Debug.LogError("FlaskManager not found in scene, cannot restore Flask data");
            return;
        }
        
        Debug.Log($"Found FlaskManager, restoring {currentSaveData.flaskData.slots.Count} Flask slots");
        
        // Clear existing Flask data
        flaskManager.ResetFlaskData();
        
        // Restore each Flask slot
        foreach (FlaskSlotSaveData slotData in currentSaveData.flaskData.slots)
        {
            // Find the item by name
            SCItem item = FindItemByName(slotData.itemName);
            if (item != null)
            {
                // Ensure slot index is valid
                if (slotData.slotIndex >= 0 && slotData.slotIndex < flaskManager.storedSlots.Count)
                {
                    var slot = flaskManager.storedSlots[slotData.slotIndex];
                    slot.item = item;
                    slot.count = slotData.count;
                    Debug.Log($"Restored Flask slot {slotData.slotIndex}: {item.itemName} x{slotData.count}");
                }
                else
                {
                    Debug.LogError($"Invalid Flask slot index: {slotData.slotIndex}");
                }
            }
            else
            {
                Debug.LogError($"Could not find item for Flask: {slotData.itemName}");
            }
        }
        
        // Refresh Flask UI
        flaskManager.RefreshUI();
        Debug.Log("=== FLASK RESTORATION COMPLETED ===");
    }

    private void RestoreSceneObjects()
    {
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[RestoreSceneObjects] ===== Starting restoration for scene: {currentScene} =====");
        
        if (currentSaveData == null)
        {
            Debug.LogError("[RestoreSceneObjects] currentSaveData is NULL - cannot restore!");
            return;
        }
        
        if (currentSaveData.sceneObjects == null || currentSaveData.sceneObjects.Count == 0)
        {
            Debug.LogWarning($"[RestoreSceneObjects] No saved scene objects found for {currentScene}");
            return;
        }
        
        Debug.Log($"[RestoreSceneObjects] Total saved objects in save data: {currentSaveData.sceneObjects.Count}");
        
        // Index saved scene-objects by objectId for quick lookup (only for current scene)
        var map = new Dictionary<string, SceneObjectSaveData>();
        foreach (var sod in currentSaveData.sceneObjects)
        {
            if (string.IsNullOrEmpty(sod.objectId)) continue;
            // Legacy entries may have empty sceneName; accept them for any scene
            if (!string.IsNullOrEmpty(sod.sceneName) && sod.sceneName != currentScene) continue;
            map[sod.objectId] = sod;
            Debug.Log($"[RestoreSceneObjects] Indexed saved object: {sod.objectId} at {sod.position}");
        }

        Debug.Log($"[RestoreSceneObjects] Found {map.Count} saved objects for current scene");

        // CRITICAL: Find all MonoBehaviours first (same as SaveSceneObjects)
        var allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true); // include inactive
        var liveComps = allMonoBehaviours.OfType<ISaveable>().ToArray();
        
        Debug.Log($"[RestoreSceneObjects] Total MonoBehaviours in scene: {allMonoBehaviours.Length}");
        Debug.Log($"[RestoreSceneObjects] ISaveable components found: {liveComps.Length}");
        
        if (liveComps.Length == 0)
        {
            Debug.LogWarning("[RestoreSceneObjects] WARNING: No ISaveable components found in scene!");
            Debug.LogWarning("[RestoreSceneObjects] Cannot restore any object states.");
            return;
        }
        
        // Group live ISaveable components by GameObject (mirror save-time grouping)
        var byObject = new Dictionary<GameObject, List<ISaveable>>();
        foreach (var comp in liveComps)
        {
            var mb = comp as MonoBehaviour;
            if (mb == null) continue;
            var go = mb.gameObject;
            if (!byObject.TryGetValue(go, out var list))
            {
                list = new List<ISaveable>();
                byObject[go] = list;
            }
            list.Add(comp);
        }
        
        Debug.Log($"[RestoreSceneObjects] Found {byObject.Count} live GameObjects with ISaveable components");

        // Track which saved entries were applied
        var usedSaved = new HashSet<string>();

        // For each GameObject, compute objectId using ALL its ISaveable comps just like in SaveSceneObjects
        foreach (var kvp in byObject)
        {
            var go = kvp.Key;
            var comps = kvp.Value;

            string objectId = TryGetStableObjectId(go, comps);
            SceneObjectSaveData sod = null;
            if (!string.IsNullOrEmpty(objectId) && map.TryGetValue(objectId, out sod))
            {
                usedSaved.Add(objectId);
                if (go.GetComponent<HarrowManager>() != null || go.GetComponent<FarmingAreaManager>() != null)
                {
                    Debug.Log($"[RestoreSceneObjects] direct match for {go.name} id={objectId} at savedPos={(sod!=null?sod.position:Vector3.zero)}");
                }
            }
            else
            {
                // Fallback: find closest unused saved entry (same object type likely) within a small radius
                float best = float.MaxValue;
                SceneObjectSaveData bestSod = null;
                bool targetIsFarming = go.GetComponent<FarmingAreaManager>() != null;

                // First pass: try to match by component signature when possible
                foreach (var entry in map)
                {
                    if (usedSaved.Contains(entry.Key)) continue;
                    var candidate = entry.Value;
                    if (targetIsFarming)
                    {
                        bool hasFAKeys = candidate.componentDataKeys != null && candidate.componentDataKeys.Exists(k => k.StartsWith("FarmingAreaManager.", StringComparison.Ordinal));
                        if (!hasFAKeys) continue;
                    }
                    float d = (candidate.position - go.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        bestSod = candidate;
                    }
                }
                // If nothing found in typed pass, allow any entry
                if (bestSod == null)
                {
                    foreach (var entry in map)
                    {
                        if (usedSaved.Contains(entry.Key)) continue;
                        var candidate = entry.Value;
                        float d = (candidate.position - go.transform.position).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            bestSod = candidate;
                        }
                    }
                }
                // Accept if within threshold (e.g., 5m)
                if (bestSod != null && best <= 5f * 5f)
                {
                    sod = bestSod;
                    // Try to find its key to mark used
                    foreach (var kv in map)
                    {
                        if (kv.Value == sod) { usedSaved.Add(kv.Key); break; }
                    }
                    Debug.Log($"RestoreSceneObjects: Fallback matched '{go.name}' by proximity (id={objectId} ~ savedPos={sod.position}).");
                }
                else
                {
                    continue; // No match for this object
                }
            }

            // Apply saved transform once per object
            ApplyTransform(go.transform, sod.position, sod.rotation, sod.scale);
            if (go.activeSelf != sod.isActive) go.SetActive(sod.isActive);
            if (go.GetComponent<HarrowManager>() != null || go.GetComponent<FarmingAreaManager>() != null)
            {
                Debug.Log($"[RestoreSceneObjects] applied transform to {go.name}: pos={sod.position}, rot={sod.rotation}");
            }

            // Dispatch component-specific data
            foreach (var comp in comps)
            {
                var typeName = comp.GetType().Name;
                var dict = new Dictionary<string, object>();
                for (int i = 0; i < sod.componentDataKeys.Count; i++)
                {
                    string key = sod.componentDataKeys[i];
                    if (!key.StartsWith(typeName + ".", StringComparison.Ordinal)) continue;
                    string shortKey = key.Substring(typeName.Length + 1);
                    dict[shortKey] = sod.componentDataValues[i];
                }
                try { 
                    Debug.Log($"[GameSaveManager] Calling LoadSaveData on {typeName} for {go.name}");
                    comp.LoadSaveData(dict); 
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Load error in {typeName} on {go.name}: {ex.Message}");
                }
            }
        }

        Debug.Log("Scene objects restored via ISaveable");
    }

    private static void ApplyTransform(Transform t, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        // Try to disable CharacterController if present (to avoid teleport issues)
        var cc = t.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        t.position = pos;
        t.eulerAngles = euler;
        t.localScale = scale;
        if (cc != null) cc.enabled = true;
    }

    private static string SerializeValue(object value)
    {
        if (value == null) return "";
        if (value is bool b) return b ? "true" : "false";
        if (value is int || value is float || value is double || value is long) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        if (value is Vector3 v) return $"{v.x:R},{v.y:R},{v.z:R}";
        return value.ToString();
    }

    private static string TryGetStableObjectId(GameObject go, IList<ISaveable> comps)
    {
        List<string> ids = null;
        foreach (var c in comps)
        {
            var mb = c as MonoBehaviour; if (mb == null) continue;
            var type = mb.GetType();
            var fi = type.GetField("saveId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                var v = fi.GetValue(mb) as string; if (!string.IsNullOrEmpty(v)) (ids ??= new List<string>()).Add(v);
            }
            var pi = type.GetProperty("saveId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (pi != null && pi.PropertyType == typeof(string))
            {
                var v = pi.GetValue(mb) as string; if (!string.IsNullOrEmpty(v)) (ids ??= new List<string>()).Add(v);
            }
        }
        if (ids != null && ids.Count > 0)
        {
            if (ids.Count > 1)
            {
                ids.Sort(StringComparer.Ordinal);
                if (ids.Distinct().Count() > 1)
                {
                    Debug.LogWarning($"[GameSaveManager] Multiple differing saveIds detected on '{go.name}': {string.Join(",", ids)} - using '{ids[0]}' deterministically.");
                }
            }
            return ids[0];
        }
        return GetHierarchyPath(go);
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null) return string.Empty;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Transform t = go.transform;
        while (t != null)
        {
            if (sb.Length > 0) sb.Insert(0, "/");
            sb.Insert(0, t.name);
            t = t.parent;
        }
        return sb.ToString();
    }
    
    private SCItem FindItemByName(string itemName)
    {
        // SCResources klasöründeki tüm SCItem'ları bul
        SCItem[] allItems = Resources.LoadAll<SCItem>("");
        foreach (SCItem item in allItems)
        {
            if (item.itemName == itemName)
            {
                return item;
            }
        }
        return null;
    }
    
    private string GetObjectId(GameObject obj)
    {
        // GameObject için unique bir ID oluştur
        return obj.name + "_" + obj.transform.position.ToString();
    }
    
    private void UpdateSaveTimesList(string saveTime)
    {
        var saveTimes = GetTrackedSaveTimes();
        saveTimes.Remove(saveTime);
        saveTimes.Add(saveTime);
        saveTimes.RemoveAll(t => string.IsNullOrEmpty(t) || !HasSaveData(t));

        while (saveTimes.Count > maxSaveSlots)
        {
            string oldestSave = saveTimes[0];
            RemoveSaveStorage(oldestSave);
            saveTimes.RemoveAt(0);
        }

        PlayerPrefs.SetString(SAVE_TIMES_KEY, string.Join(",", saveTimes));
    }
    
    public List<string> GetSaveTimes()
    {
        var tracked = GetTrackedSaveTimes();
        tracked.RemoveAll(t => string.IsNullOrEmpty(t) || !HasSaveData(t));

        var known = new HashSet<string>(tracked);
        foreach (var discovered in EnumerateSaveTimesFromFiles())
        {
            if (!string.IsNullOrEmpty(discovered) && known.Add(discovered))
            {
                tracked.Add(discovered);
            }
        }

        return tracked;
    }
    
    public bool HasSaveData(string saveTime)
    {
        string alt = SanitizeSaveTime(saveTime);
        if (TryReadSaveFromFile(alt, out _)) return true;
        if (PlayerPrefs.HasKey(SAVE_PREFIX + saveTime)) return true;
        return PlayerPrefs.HasKey(SAVE_PREFIX + alt);
    }

    private List<string> GetTrackedSaveTimes()
    {
        string times = PlayerPrefs.GetString(SAVE_TIMES_KEY, "");
        var list = new List<string>(times.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        list.RemoveAll(t => string.IsNullOrEmpty(t) || !HasSaveData(t));
        return list;
    }

    private string GetSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, SAVE_DIRECTORY_NAME);
    }

    private string GetSaveFilePath(string sanitizedSaveTime)
    {
        return Path.Combine(GetSaveDirectoryPath(), $"{SAVE_PREFIX}{sanitizedSaveTime}.json");
    }

    private void EnsureSaveDirectory()
    {
        string dir = GetSaveDirectoryPath();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void PersistSavePayload(string saveTime, string sanitized, string jsonData)
    {
        try
        {
            EnsureSaveDirectory();
            string path = GetSaveFilePath(sanitized);
            
            // CRITICAL: File.WriteAllText is synchronous and flushes immediately
            File.WriteAllText(path, jsonData ?? string.Empty);
            
            // EXTRA SAFETY: Verify file was written (build-specific check)
            if (File.Exists(path))
            {
                Debug.Log($"[PersistSavePayload] File written successfully: {path} ({jsonData?.Length ?? 0} bytes)");
            }
            else
            {
                Debug.LogError($"[PersistSavePayload] File write verification FAILED: {path}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write save file for {saveTime}: {ex.Message}");
        }

        if (string.IsNullOrEmpty(jsonData))
        {
            PlayerPrefs.DeleteKey(SAVE_PREFIX + saveTime);
            if (sanitized != saveTime) PlayerPrefs.DeleteKey(SAVE_PREFIX + sanitized);
            return;
        }

        if (jsonData.Length > PLAYER_PREFS_STRING_LIMIT)
        {
            if (!_playerPrefsSizeWarningLogged)
            {
                Debug.LogWarning($"Save payload size ({jsonData.Length} chars) exceeds PlayerPrefs limit; relying on disk files only.");
                _playerPrefsSizeWarningLogged = true;
            }
            PlayerPrefs.DeleteKey(SAVE_PREFIX + saveTime);
            if (sanitized != saveTime) PlayerPrefs.DeleteKey(SAVE_PREFIX + sanitized);
            return;
        }

        try
        {
            PlayerPrefs.SetString(SAVE_PREFIX + saveTime, jsonData);
            if (sanitized != saveTime)
            {
                PlayerPrefs.SetString(SAVE_PREFIX + sanitized, jsonData);
            }
        }
        catch (PlayerPrefsException ex)
        {
            if (!_playerPrefsSizeWarningLogged)
            {
                Debug.LogWarning($"PlayerPrefs save failed, using disk files only: {ex.Message}");
                _playerPrefsSizeWarningLogged = true;
            }
            PlayerPrefs.DeleteKey(SAVE_PREFIX + saveTime);
            if (sanitized != saveTime) PlayerPrefs.DeleteKey(SAVE_PREFIX + sanitized);
        }
    }

    private bool TryLoadJsonPayload(string saveTime, out string jsonData)
    {
        string sanitized = SanitizeSaveTime(saveTime);
        if (TryReadSaveFromFile(sanitized, out jsonData))
        {
            return true;
        }

        string key = SAVE_PREFIX + saveTime;
        if (PlayerPrefs.HasKey(key))
        {
            jsonData = PlayerPrefs.GetString(key);
            return !string.IsNullOrEmpty(jsonData);
        }

        string altKey = SAVE_PREFIX + sanitized;
        if (PlayerPrefs.HasKey(altKey))
        {
            jsonData = PlayerPrefs.GetString(altKey);
            return !string.IsNullOrEmpty(jsonData);
        }

        jsonData = null;
        return false;
    }

    private bool TryReadSaveFromFile(string sanitized, out string jsonData)
    {
        jsonData = null;
        if (string.IsNullOrEmpty(sanitized)) return false;

        string path = GetSaveFilePath(sanitized);
        if (!File.Exists(path)) return false;

        try
        {
            jsonData = File.ReadAllText(path);
            return !string.IsNullOrEmpty(jsonData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read save file '{path}': {ex.Message}");
            jsonData = null;
            return false;
        }
    }

    private void RemoveSaveStorage(string saveTime)
    {
        string sanitized = SanitizeSaveTime(saveTime);
        try
        {
            string filePath = GetSaveFilePath(sanitized);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to delete save file for {saveTime}: {ex.Message}");
        }

        PlayerPrefs.DeleteKey(SAVE_PREFIX + saveTime);
        if (sanitized != saveTime)
        {
            PlayerPrefs.DeleteKey(SAVE_PREFIX + sanitized);
        }
    }

    private IEnumerable<string> EnumerateSaveTimesFromFiles()
    {
        string dir = GetSaveDirectoryPath();
        if (!Directory.Exists(dir)) yield break;

        string[] files;
        try
        {
            files = Directory.GetFiles(dir, $"{SAVE_PREFIX}*.json");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to enumerate save files: {ex.Message}");
            yield break;
        }

        foreach (var file in files)
        {
            string json;
            try
            {
                json = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to read save metadata from '{file}': {ex.Message}");
                continue;
            }

            if (string.IsNullOrEmpty(json)) continue;

            GameSaveData data = null;
            try
            {
                data = JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse save metadata from '{file}': {ex.Message}");
            }

            if (!string.IsNullOrEmpty(data?.saveTime))
            {
                yield return data.saveTime;
            }
        }
    }

    private static string SanitizeSaveTime(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace(':', '-').Replace('\n', ' ').Replace('\r', ' ');
    }
    
    public void DeleteSave(string saveTime)
    {
        RemoveSaveStorage(saveTime);

        var saveTimes = GetTrackedSaveTimes();
        if (saveTimes.Remove(saveTime))
        {
            PlayerPrefs.SetString(SAVE_TIMES_KEY, string.Join(",", saveTimes));
            PlayerPrefs.Save();
        }
    }
    
    /// <summary>
    /// Bir bitki toplandığında bu metodu çağırın
    /// </summary>
    public void OnPlantCollected(Plant plant)
    {
        if (plant == null || plant.item == null) return;
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string plantId = $"{plant.item.itemName}_{plant.transform.position.x:F2}_{plant.transform.position.y:F2}_{plant.transform.position.z:F2}";
        
        // Current save data'yı kontrol et
        if (currentSaveData == null)
        {
            // Eğer save data yoksa yeni bir tane oluştur
            currentSaveData = new GameSaveData();
        }
        
        // Bu bitkiyi collected olarak işaretle veya save listesinden çıkar
        PlantSaveData existingPlant = currentSaveData.plants.Find(p => p.plantId == plantId && p.sceneName == currentScene);
        if (existingPlant != null)
        {
            existingPlant.isCollected = true;
            Debug.Log($"Plant marked as collected: {plantId}");
        }
        else
        {
            // Eğer listede yoksa collected olarak ekle
            PlantSaveData newPlantData = new PlantSaveData();
            newPlantData.itemName = plant.item.itemName;
            newPlantData.position = plant.transform.position;
            newPlantData.rotation = plant.transform.eulerAngles;
            newPlantData.scale = plant.transform.localScale;
            newPlantData.sceneName = currentScene;
            newPlantData.plantId = plantId;
            newPlantData.isCollected = true;
            
            currentSaveData.plants.Add(newPlantData);
            Debug.Log($"Plant added as collected: {plantId}");
        }

        // As an extra safety, remove any other Plant clones at same spot to avoid duplicate pickups on reload
        var plantsAtSamePos = GameObject.FindObjectsOfType<Plant>()
            .Where(p => p != null && p.item != null &&
                        Mathf.Approximately(p.transform.position.x, plant.transform.position.x) &&
                        Mathf.Approximately(p.transform.position.y, plant.transform.position.y) &&
                        Mathf.Approximately(p.transform.position.z, plant.transform.position.z));
        foreach (var p in plantsAtSamePos)
        {
            if (p != plant)
            {
                Destroy(p.gameObject);
            }
        }
    }

    // Called when a world item is picked up so it won't respawn on return
    public void OnWorldItemPickedUp(WorldItem picked)
    {
        if (picked == null || picked.item == null) return;
        if (currentSaveData == null) currentSaveData = new GameSaveData();
        if (currentSaveData.worldItems == null) currentSaveData.worldItems = new List<WorldItemSaveData>();

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string persistentId = picked.PersistentId;
        bool willDestroy = picked.quantity <= 1;
        int removed = 0;
        if (!string.IsNullOrEmpty(persistentId))
        {
            if (willDestroy)
            {
                MarkWorldItemPendingRemoval(persistentId);
            }
            removed = currentSaveData.worldItems.RemoveAll(w =>
                w != null && w.sceneName == currentScene && !string.IsNullOrEmpty(w.persistentId) && w.persistentId == persistentId);
        }
        if (removed == 0)
        {
            var pos = picked.transform.position;
            removed = currentSaveData.worldItems.RemoveAll(w =>
                w != null && w.sceneName == currentScene && w.itemName == picked.item.itemName &&
                (w.position - pos).sqrMagnitude <= 0.5f * 0.5f);
        }
        if (removed > 0)
        {
            Debug.Log($"[GameSaveManager] Removed {removed} saved world item entries for picked {picked.item.itemName}");
        }
        if (!willDestroy && !string.IsNullOrEmpty(persistentId))
        {
            CaptureWorldItemSnapshot(picked);
        }
        // Destroy işlemi tamamlandıktan sonra snapshot'ı güncelle
        RequestWorldItemSnapshotRefresh();
    }

    // Called when an inventory item is dropped into the world
    public void OnWorldItemDropped(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.item == null) return;
        if (currentSaveData == null) currentSaveData = new GameSaveData();
        if (currentSaveData.worldItems == null) currentSaveData.worldItems = new List<WorldItemSaveData>();

        worldItem.MarkRuntime(WorldItem.WorldItemOrigin.ManualDrop);
        Debug.Log($"[GameSaveManager] Registered dropped world item {worldItem.item.itemName} with id {worldItem.PersistentId}");

        // Spawn edilen objenin nihai transform'unu almak için snapshot'ı bir sonraki frame'e ertele
        RequestWorldItemSnapshotRefresh();
    }

    public void CaptureWorldItemSnapshot(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.item == null) return;
        if (_isRestoringScene) return; // restore sırasında yok edilen objeleri yeniden kaydetme
        if (currentSaveData == null) currentSaveData = new GameSaveData();
        if (currentSaveData.worldItems == null) currentSaveData.worldItems = new List<WorldItemSaveData>();

        string sceneName = worldItem.gameObject.scene.IsValid()
            ? worldItem.gameObject.scene.name
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        var position = worldItem.transform.position;
        string persistentId = worldItem.PersistentId;
        if (!string.IsNullOrEmpty(persistentId) && _worldItemsPendingRemoval.Contains(persistentId))
        {
            Debug.Log($"[GameSaveManager] Skipping snapshot for world item {worldItem.item.itemName} ({persistentId}) pending removal");
            return;
        }
        int removed = 0;
        if (!string.IsNullOrEmpty(persistentId))
        {
            removed = currentSaveData.worldItems.RemoveAll(w =>
                w != null && w.sceneName == sceneName && !string.IsNullOrEmpty(w.persistentId) && w.persistentId == persistentId);
        }
        if (removed == 0)
        {
            currentSaveData.worldItems.RemoveAll(w =>
                w != null && w.sceneName == sceneName && w.itemName == worldItem.item.itemName &&
                (w.position - position).sqrMagnitude <= 0.5f * 0.5f);
        }

        currentSaveData.worldItems.Add(new WorldItemSaveData
        {
            itemName = worldItem.item.itemName,
            position = position,
            rotation = worldItem.transform.eulerAngles,
            scale = worldItem.transform.localScale,
            quantity = Mathf.Max(1, worldItem.quantity),
            sceneName = sceneName,
            persistentId = persistentId,
            origin = worldItem.Origin.ToString()
        });

        _pendingWorldItemSnapshots.Add(sceneName);
        Debug.Log($"[GameSaveManager] Captured pending snapshot for world item {worldItem.item.itemName} in scene {sceneName}");
        RequestWorldItemSnapshotRefresh();
    }

    // Public helper for components to update snapshot of ISaveable objects without flushing to disk
    public void CaptureSceneObjectsSnapshotNow()
    {
        if (currentSaveData == null) currentSaveData = new GameSaveData();
        SaveSceneObjects(false); // tam yenileme
    }

    // Incremental (merging) snapshot - objeler disable/destroy sürecindeyken veri kaybını önler
    public void CaptureSceneObjectsIncremental()
    {
        if (_isSceneUnloading)
        {
            Debug.Log("[GameSaveManager] Skipping incremental scene snapshot while scene unload is in progress");
            return;
        }
        if (currentSaveData == null) currentSaveData = new GameSaveData();
        SaveSceneObjects(true);
    }

    // Incremental snapshot + immediate disk flush (yüksek riskli anlar için)
    public void CaptureIncrementalAndFlush()
    {
        CaptureSceneObjectsIncremental();
        // Küçük bir debounce olmadan direkt SaveGame çağırmak disk IO'yu artırabilir
        // Bu yüzden sadece kritik anlarda kullanın
        SaveGame();
    }
    
    // Auto-save on application focus/pause events
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("Application paused, auto-saving...");
            AutoSave();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            Debug.Log("Application lost focus, auto-saving...");
            AutoSave();
        }
    }
    
    // Auto-save method
    private void AutoSave()
    {
        try
        {
            SaveGame();
            Debug.Log("Auto-save completed successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Auto-save failed: {e.Message}");
        }
    }
    
    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
}
