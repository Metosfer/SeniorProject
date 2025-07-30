using UnityEngine;
using UnityEngine.SceneManagement;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }
    
    [Header("Inventory Settings")]
    public SCInventory playerInventory; // Inspector'da atanan inventory (opsiyonel)
    
    private SCInventory persistentInventory;
    
    private void Awake()
    {
        // Singleton pattern - sadece bir tane InventoryManager olmalı
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("InventoryManager created and set to DontDestroyOnLoad");
            
            // Persistent inventory'yi al veya oluştur
            persistentInventory = SCInventory.GetPersistentInventory();
            
            // Scene değişikliklerini dinle - Unity'nin built-in event sistemi
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Debug.Log("Duplicate InventoryManager found, destroying...");
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // Inspector'da atanan inventory ile persistent inventory'yi sync et
        if (playerInventory != null && persistentInventory.inventorySlots.Count == 0)
        {
            // Inspector inventory'sini persistent'a kopyala
            for (int i = 0; i < playerInventory.inventorySlots.Count; i++)
            {
                if (i < persistentInventory.inventorySlots.Count)
                {
                    persistentInventory.inventorySlots[i] = playerInventory.inventorySlots[i];
                }
            }
            Debug.Log("Synced inspector inventory to persistent inventory");
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Yeni sahne yüklendiğinde inventory UI'ları güncelle
        Invoke(nameof(RefreshInventoryUI), 0.1f);
    }
    
    public SCInventory GetPlayerInventory()
    {
        if (persistentInventory == null)
        {
            persistentInventory = SCInventory.GetPersistentInventory();
            Debug.Log("Created new persistent inventory");
        }
        return persistentInventory;
    }
    
    public void SetPlayerInventory(SCInventory inventory)
    {
        persistentInventory = inventory;
    }
    
    // Her sahne yüklendiğinde çağrılabilir
    public void RefreshInventoryUI()
    {
        InventoryUIManager[] uiManagers = FindObjectsOfType<InventoryUIManager>();
        Debug.Log($"Found {uiManagers.Length} InventoryUIManager(s) in scene");
        
        foreach(InventoryUIManager uiManager in uiManagers)
        {
            if (uiManager != null && persistentInventory != null)
            {
                // UI Manager'da inventory referansı yok mu kontrol et
                if (uiManager.inventory == null)
                {
                    uiManager.SetInventoryReference(persistentInventory);
                    Debug.Log("Connected null inventory reference to persistent inventory");
                }
                else if (uiManager.inventory != persistentInventory)
                {
                    // Farklı bir inventory referansı varsa, persistent ile değiştir
                    uiManager.SetInventoryReference(persistentInventory);
                    Debug.Log("Updated inventory reference to persistent inventory");
                }
                
                // Her durumda UI'ı yenile
                uiManager.RefreshUI();
                Debug.Log("Updated InventoryUIManager with persistent inventory data");
            }
        }
        
        // Sahne içindeki tüm Inventory component'lerini de sync et
        Inventory[] inventoryComponents = FindObjectsOfType<Inventory>();
        foreach (Inventory inv in inventoryComponents)
        {
            if (inv.playerInventory != persistentInventory)
            {
                inv.playerInventory = persistentInventory;
                Debug.Log($"Synced {inv.name} inventory component");
            }
        }
        
        // Event trigger'la
        if (persistentInventory != null)
        {
            persistentInventory.TriggerInventoryChanged();
            Debug.Log("Triggered inventory changed event");
        }
    }
    
    private void OnDestroy()
    {
        // Event'lerden unsubscribe ol
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
