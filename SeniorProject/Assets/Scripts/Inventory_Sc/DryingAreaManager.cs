using UnityEngine;

public class DryingAreaManager : MonoBehaviour
{
    [Header("Drying Settings")]
    public DryingSlot[] dryingSlots = new DryingSlot[3];
    
    [Header("Player Interaction")]
    public float interactionRange = 3f;
    public KeyCode interactionKey = KeyCode.T   ;
    public GameObject interactionUI; // "Press T to open drying area" UI

    [Header("UI References")]
    public DryingAreaUI dryingUI;
    
    private bool playerInRange = false;
    private Transform playerTransform;
    private Inventory playerInventory;

    private void Start()
    {
        // Player referanslarını bul
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerInventory = FindObjectOfType<Inventory>();
        
        // Interaction UI'yı başlangıçta gizle
        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
        }
        
        // Slot'ları sıfırla
        for (int i = 0; i < dryingSlots.Length; i++)
        {
            if (dryingSlots[i] != null)
            {
                dryingSlots[i].ResetSlot();
            }
        }
    }

    private void Update()
    {
        // Player mesafe kontrolü
        CheckPlayerDistance();

        // T tuşu kontrolü (input işlemleri Update'te olmalı)
        if (playerInRange && Input.GetKeyDown(interactionKey))
        {
            if (dryingUI != null)
            {
                dryingUI.TogglePanel();
            }
        }

        // Kurutma timer'ları güncelle
        foreach (DryingSlot slot in dryingSlots)
        {
            if (slot != null && slot.isOccupied && !slot.isReadyToCollect)
            {
                slot.timer -= Time.deltaTime;
                if (slot.timer <= 0f)
                {
                    CompleteDrying(slot);
                }
            }
        }
    }

    private void CheckPlayerDistance()
    {
        if (playerTransform == null) return;
        
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool wasInRange = playerInRange;
        playerInRange = distance <= interactionRange;
        
        // Player range'e girdi/çıktı
        if (playerInRange != wasInRange)
        {
            if (interactionUI != null)
            {
                interactionUI.SetActive(playerInRange);
            }
        }
    }

    public bool TryAddItemToSlot(int slotIndex, SCItem item)
    {
        if (slotIndex < 0 || slotIndex >= dryingSlots.Length)
            return false;
            
        DryingSlot slot = dryingSlots[slotIndex];
        if (slot == null) return false;
        
        // Slot boş mu ve item kurutulabilir mi kontrol et
        if (!slot.isOccupied && item.canBeDried && item.dryingTime > 0)
        {
            slot.currentItemData = item;
            slot.timer = item.dryingTime;
            slot.isOccupied = true;
            slot.isReadyToCollect = false;
            
            Debug.Log($"Item '{item.itemName}' kurutma slot {slotIndex}'a eklendi. Süre: {item.dryingTime}s");
            return true;
        }
        
        return false;
    }

    private void CompleteDrying(DryingSlot slot)
    {
        if (slot.currentItemData != null)
        {
            slot.isReadyToCollect = true;
            Debug.Log($"Kurutma tamamlandı: {slot.currentItemData.itemName}");
        }
    }

    public void CollectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= dryingSlots.Length)
            return;
            
        DryingSlot slot = dryingSlots[slotIndex];
        if (slot == null) return;
        
        if (slot.isReadyToCollect && slot.currentItemData != null)
        {
            Debug.Log($"CollectSlot: slotIndex={slotIndex}, item={slot.currentItemData.itemName}, driedVersion={(slot.currentItemData.driedVersion != null ? slot.currentItemData.driedVersion.itemName : "null")}");
            // Kurutulmuş item'ı inventory'e ekle
            if (playerInventory != null && slot.currentItemData.driedVersion != null)
            {
                bool added = playerInventory.playerInventory.AddItem(slot.currentItemData.driedVersion);
                Debug.Log($"AddItem(driedVersion): {slot.currentItemData.driedVersion.itemName}, result={added}");
                if (added)
                {
                    Debug.Log($"Kurutulmuş item toplandı: {slot.currentItemData.driedVersion.itemName}");
                    slot.ResetSlot();
                }
                else
                {
                    Debug.LogWarning("Inventory dolu! Kurutulmuş item toplanamadı.");
                }
            }
            else if (slot.currentItemData.driedVersion == null)
            {
                // Eğer dried version yoksa orijinal item'ı geri ver
                bool added = playerInventory.playerInventory.AddItem(slot.currentItemData);
                Debug.Log($"AddItem(original): {slot.currentItemData.itemName}, result={added}");
                if (added)
                {
                    Debug.Log($"Item toplandı (dried version yok): {slot.currentItemData.itemName}");
                    slot.ResetSlot();
                }
                else
                {
                    Debug.LogWarning("Inventory dolu! Item toplanamadı.");
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Interaction range'i göster
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
