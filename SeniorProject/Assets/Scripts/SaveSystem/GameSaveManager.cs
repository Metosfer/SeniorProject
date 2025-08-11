using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameSaveData
{
    public string saveTime;
    public string sceneName;
    public PlayerSaveData playerData;
    public List<WorldItemSaveData> worldItems;
    public List<PlantSaveData> plants;
    public InventorySaveData inventoryData;
    // Yeni inventory save format
    public List<PlayerInventoryItemData> playerInventoryData;
    // Dictionary yerine List kullanarak JSON serialization problemini çözüyoruz
    public List<SceneObjectSaveData> sceneObjects;
    
    public GameSaveData()
    {
        worldItems = new List<WorldItemSaveData>();
        plants = new List<PlantSaveData>();
        sceneObjects = new List<SceneObjectSaveData>();
        playerInventoryData = new List<PlayerInventoryItemData>();
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
public class SceneObjectSaveData
{
    public string objectId;
    public bool isActive;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
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
    
    private GameSaveData currentSaveData;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Sahne yüklendiğinde biraz bekle, sonra restore et
        Invoke(nameof(RestoreSceneData), 0.1f);
    }
    
    public void SaveGame()
    {
        string saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        currentSaveData = new GameSaveData();
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
        
        // Scene objects'leri kaydet
        SaveSceneObjects();
        
        // Save data'yı JSON'a çevir ve PlayerPrefs'e kaydet
        string jsonData = JsonUtility.ToJson(currentSaveData, true);
        PlayerPrefs.SetString(SAVE_PREFIX + saveTime, jsonData);
        
        // Save times listesini güncelle
        UpdateSaveTimesList(saveTime);
        
        PlayerPrefs.Save();
        Debug.Log($"Game saved successfully at {saveTime}");
    }
    
    public void LoadGame(string saveTime)
    {
        string saveKey = SAVE_PREFIX + saveTime;
        if (!PlayerPrefs.HasKey(saveKey))
        {
            Debug.LogError($"Save data not found for time: {saveTime}");
            return;
        }
        
        string jsonData = PlayerPrefs.GetString(saveKey);
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
            currentSaveData.playerData = new PlayerSaveData();
            currentSaveData.playerData.position = player.transform.position;
            currentSaveData.playerData.rotation = player.transform.eulerAngles;
            currentSaveData.playerData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
    }
    
    private void SaveWorldItems()
    {
        currentSaveData.worldItems.Clear();
        
        // WorldItem component'ine sahip tüm objeleri bul
        WorldItem[] worldItems = FindObjectsOfType<WorldItem>();
        foreach (WorldItem worldItem in worldItems)
        {
            if (worldItem.item != null)
            {
                WorldItemSaveData itemData = new WorldItemSaveData();
                itemData.itemName = worldItem.item.itemName;
                itemData.position = worldItem.transform.position;
                itemData.rotation = worldItem.transform.eulerAngles;
                itemData.scale = worldItem.transform.localScale;
                itemData.quantity = worldItem.quantity;
                itemData.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                
                currentSaveData.worldItems.Add(itemData);
            }
        }
        
        // DroppedItem component'ine sahip objeleri de kaydet
        DroppedItem[] droppedItems = FindObjectsOfType<DroppedItem>();
        foreach (DroppedItem droppedItem in droppedItems)
        {
            if (droppedItem.itemData != null)
            {
                WorldItemSaveData itemData = new WorldItemSaveData();
                itemData.itemName = droppedItem.itemData.itemName;
                itemData.position = droppedItem.transform.position;
                itemData.rotation = droppedItem.transform.eulerAngles;
                itemData.scale = droppedItem.transform.localScale;
                itemData.quantity = 1;
                itemData.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                
                currentSaveData.worldItems.Add(itemData);
            }
        }
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
                // Compute plantId and skip if this plant was already collected earlier
                string thisPlantId = $"{plant.item.itemName}_{plant.transform.position.x:F2}_{plant.transform.position.y:F2}_{plant.transform.position.z:F2}";
                var alreadyCollected = currentSaveData.plants.Any(p => p.sceneName == currentScene && p.plantId == thisPlantId && p.isCollected);
                if (alreadyCollected)
                {
                    // Don’t re-add this plant to save list
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
                Debug.Log($"Saved plant: {plantData.itemName} at {plantData.position} with ID: {plantData.plantId}");
            }
        }
        
        Debug.Log($"Total plants in current scene: {plants.Length}, Total plants saved across all scenes: {currentSaveData.plants.Count}");
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
    
    private void SaveSceneObjects()
    {
        currentSaveData.sceneObjects.Clear();
        
        // Collect all ISaveable components in the scene and group by GameObject
        var saveableComponents = FindObjectsOfType<MonoBehaviour>().OfType<ISaveable>().ToArray();
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

        foreach (var kvp in byObject)
        {
            var go = kvp.Key;
            var comps = kvp.Value;

            var sod = new SceneObjectSaveData();
            sod.objectId = TryGetStableObjectId(go, comps);
            sod.position = go.transform.position;
            sod.rotation = go.transform.eulerAngles;
            sod.scale = go.transform.localScale;

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
                foreach (var entry in data)
                {
                    string key = $"{typeName}.{entry.Key}";
                    string value = SerializeValue(entry.Value);
                    sod.AddComponentData(key, value);
                }
            }

            currentSaveData.sceneObjects.Add(sod);
        }

        Debug.Log($"Scene objects saved via ISaveable: {currentSaveData.sceneObjects.Count}");
    }
    
    private void RestoreSceneData()
    {
        if (currentSaveData == null) return;
        
        Debug.Log("Starting scene data restoration...");
        
        // Player pozisyonunu restore et
        RestorePlayerData();
        
        // Mevcut world item'ları temizle
        ClearExistingWorldItems();
        
        // World item'ları restore et
        RestoreWorldItems();
        
        // Mevcut plant'ları temizle
        ClearExistingPlants();
        
        // Plant'ları restore et
        RestorePlants();
        
        // Inventory'yi restore et (biraz gecikme ile)
        Invoke(nameof(DelayedInventoryRestore), 0.2f);
        
        // Scene object'leri restore et
        RestoreSceneObjects();
        
        Debug.Log("Scene data restoration completed");
    }
    
    private void DelayedInventoryRestore()
    {
        RestoreInventoryData();
    }
    
    private void RestorePlayerData()
    {
        if (currentSaveData.playerData != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                    player.transform.position = currentSaveData.playerData.position;
                    player.transform.eulerAngles = currentSaveData.playerData.rotation;
                    controller.enabled = true;
                }
                else
                {
                    player.transform.position = currentSaveData.playerData.position;
                    player.transform.eulerAngles = currentSaveData.playerData.rotation;
                }
            }
        }
    }
    
    private void ClearExistingWorldItems()
    {
        // Mevcut world item'ları sil
        WorldItem[] existingWorldItems = FindObjectsOfType<WorldItem>();
        foreach (WorldItem item in existingWorldItems)
        {
            DestroyImmediate(item.gameObject);
        }
        
        DroppedItem[] existingDroppedItems = FindObjectsOfType<DroppedItem>();
        foreach (DroppedItem item in existingDroppedItems)
        {
            DestroyImmediate(item.gameObject);
        }
    }
    
    private void RestoreWorldItems()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        foreach (WorldItemSaveData itemData in currentSaveData.worldItems)
        {
            if (itemData.sceneName == currentScene)
            {
                // Item'ı SCResources'dan bul
                SCItem item = FindItemByName(itemData.itemName);
                if (item != null)
                {
                    // WorldItemSpawner kullanarak spawn et
                    WorldItemSpawner.SpawnItem(item, itemData.position, itemData.quantity);
                    
                    // Son spawn edilen item'ın transform'unu ayarla
                    WorldItem[] allWorldItems = FindObjectsOfType<WorldItem>();
                    if (allWorldItems.Length > 0)
                    {
                        WorldItem lastSpawned = allWorldItems[allWorldItems.Length - 1];
                        lastSpawned.transform.eulerAngles = itemData.rotation;
                        lastSpawned.transform.localScale = itemData.scale;
                    }
                }
            }
        }
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
                string plantId = $"{plant.item.itemName}_{plant.transform.position.x:F2}_{plant.transform.position.y:F2}_{plant.transform.position.z:F2}";
                existingPlantIds.Add(plantId);
            }
        }
        
        // Save edilen bitkileri kontrol et
        foreach (PlantSaveData plantData in currentSaveData.plants)
        {
            if (plantData.sceneName == currentScene && !plantData.isCollected)
            {
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
    
    private void RestoreSceneObjects()
    {
        // Index saved scene-objects by objectId for quick lookup
        var map = new Dictionary<string, SceneObjectSaveData>();
        foreach (var sod in currentSaveData.sceneObjects)
        {
            if (string.IsNullOrEmpty(sod.objectId)) continue;
            map[sod.objectId] = sod;
        }

        var saveableComponents = FindObjectsOfType<MonoBehaviour>().OfType<ISaveable>().ToArray();
        foreach (var comp in saveableComponents)
        {
            var mb = comp as MonoBehaviour;
            if (mb == null) continue;
            var go = mb.gameObject;

            // Compute stable id same way as in save
            string objectId = TryGetStableObjectId(go, new List<ISaveable> { comp });
            if (string.IsNullOrEmpty(objectId) || !map.TryGetValue(objectId, out var sod))
            {
                continue; // Nothing saved for this object
            }

            // Apply saved transform
            ApplyTransform(go.transform, sod.position, sod.rotation, sod.scale);

            // Extract this component's namespaced data and pass it back
            var typeName = comp.GetType().Name;
            var dict = new Dictionary<string, object>();
            for (int i = 0; i < sod.componentDataKeys.Count; i++)
            {
                string key = sod.componentDataKeys[i];
                if (!key.StartsWith(typeName + ".", StringComparison.Ordinal)) continue;
                string shortKey = key.Substring(typeName.Length + 1);
                dict[shortKey] = sod.componentDataValues[i];
            }

            try { comp.LoadSaveData(dict); }
            catch (Exception ex)
            {
                Debug.LogError($"Load error in {typeName} on {go.name}: {ex.Message}");
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
        // If any component exposes a public 'saveId' field/property, use that for stable identity
        foreach (var c in comps)
        {
            var mb = c as MonoBehaviour;
            if (mb == null) continue;
            var type = mb.GetType();
            var fi = type.GetField("saveId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                var v = fi.GetValue(mb) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            var pi = type.GetProperty("saveId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (pi != null && pi.PropertyType == typeof(string))
            {
                var v = pi.GetValue(mb) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
        }
        // Fallback: hierarchy path (stable if scene hierarchy is stable)
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
        List<string> saveTimes = GetSaveTimes();
        saveTimes.Add(saveTime);
        
        // Maksimum save slot sayısını aş
        if (saveTimes.Count > maxSaveSlots)
        {
            // En eski save'i sil
            string oldestSave = saveTimes[0];
            PlayerPrefs.DeleteKey(SAVE_PREFIX + oldestSave);
            saveTimes.RemoveAt(0);
        }
        
        PlayerPrefs.SetString(SAVE_TIMES_KEY, string.Join(",", saveTimes.ToArray()));
    }
    
    public List<string> GetSaveTimes()
    {
        string times = PlayerPrefs.GetString(SAVE_TIMES_KEY, "");
        return new List<string>(times.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
    }
    
    public bool HasSaveData(string saveTime)
    {
        return PlayerPrefs.HasKey(SAVE_PREFIX + saveTime);
    }
    
    public void DeleteSave(string saveTime)
    {
        PlayerPrefs.DeleteKey(SAVE_PREFIX + saveTime);
        
        List<string> saveTimes = GetSaveTimes();
        saveTimes.Remove(saveTime);
        PlayerPrefs.SetString(SAVE_TIMES_KEY, string.Join(",", saveTimes.ToArray()));
        PlayerPrefs.Save();
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
    
    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
