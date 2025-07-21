using UnityEngine;

public class SceneInventorySetup : MonoBehaviour
{
    [Header("Scene Setup")]
    [Tooltip("Bu sahne için inventory UI'sini otomatik setup yap")]
    public bool autoSetupInventoryUI = true;
    
    [Tooltip("Bu sahnede inventory'yi sıfırla (test için)")]
    public bool resetInventoryOnLoad = false;
    
    [Tooltip("UI senkronizasyonunu zorla (UI sorunları için)")]
    public bool forceUISynchronization = true;

    private void Start()
    {
        SetupSceneInventory();
    }

    private void SetupSceneInventory()
    {
        // Persistent inventory'yi al
        SCInventory persistentInventory = SCInventory.GetPersistentInventory();
        
        // Test için inventory sıfırlama
        if (resetInventoryOnLoad)
        {
            persistentInventory.ResetInventory();
            Debug.Log("Inventory reset for testing");
        }

        // Sahne içindeki tüm InventoryUIManager'ları bul ve persistent inventory'yi ata
        if (autoSetupInventoryUI)
        {
            InventoryUIManager[] uiManagers = FindObjectsOfType<InventoryUIManager>();
            foreach (InventoryUIManager uiManager in uiManagers)
            {
                // Her InventoryUIManager için merge işlemini tetikle
                uiManager.ForceInventoryMergeAndUpdate();
                Debug.Log($"Triggered inventory merge for {uiManager.name}");
                
                // Ek senkronizasyon kontrolü
                if (forceUISynchronization)
                {
                    // Kısa bir bekleme sonrası tekrar güncelle
                    StartCoroutine(DelayedUIUpdate(uiManager));
                }
            }
        }

        // Sahne içindeki tüm Inventory component'lerini persistent inventory ile sync et
        Inventory[] inventoryComponents = FindObjectsOfType<Inventory>();
        foreach (Inventory inv in inventoryComponents)
        {
            if (inv.playerInventory == null || inv.playerInventory.inventorySlots.Count == 0)
            {
                inv.playerInventory = persistentInventory;
                Debug.Log($"Synced {inv.name} with persistent inventory");
            }
        }

        Debug.Log("Scene inventory setup completed");
    }

    // Inspector'da test butonu için
    [ContextMenu("Setup Inventory")]
    public void ForceSetupInventory()
    {
        SetupSceneInventory();
    }

    [ContextMenu("Reset Persistent Inventory")]
    public void ResetPersistentInventory()
    {
        SCInventory persistentInventory = SCInventory.GetPersistentInventory();
        persistentInventory.ResetInventory();
        Debug.Log("Persistent inventory has been reset");
    }
    
    // UI senkronizasyon sorunları için gecikmeli güncelleme
    private System.Collections.IEnumerator DelayedUIUpdate(InventoryUIManager uiManager)
    {
        yield return new WaitForSeconds(0.1f);
        
        if (uiManager != null)
        {
            uiManager.RefreshUI();
            Debug.Log($"Delayed UI update completed for {uiManager.name}");
        }
    }
}
