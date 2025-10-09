using UnityEngine;
using System.Collections.Generic;

public class InventoryUIManager : MonoBehaviour
{
    public SCInventory inventory;
    public GameObject inventoryPanel; // Inventory paneline referans
    private List<InventorySlotUI> slotUIs;
    public bool isInventoryVisible = false;
    
    [Header("Auto-Open Settings")]
    [Tooltip("Item alındığında paneli otomatik aç")]
    public bool autoOpenOnItemAdded = true;
    [Tooltip("Otomatik açma için minimum delay (saniye)")]
    public float autoOpenDelay = 0.1f;
    
    // Private variables for auto-open functionality
    private int previousItemCount = 0;
    private bool hasInitialized = false;

    private void Awake()
    {
        // Basit referans kontrolü yap - henüz inventory merge etme
        slotUIs = new List<InventorySlotUI>();
    }
    
    private void Start()
    {
        // İlk inventory setup'ını yap
        SetupInventory();
        
        // UI setup'ını yap  
        SetupUI();
        
        // Final update
        FinalizeSetup();
    }
    
    private void SetupInventory()
    {
        // Eğer inspector'da atanmış bir inventory varsa, onu persistent inventory ile merge et
        SCInventory assignedInventory = inventory;
        
        // Persistent inventory kullan
        inventory = SCInventory.GetPersistentInventory();
        
        // Eğer inspector'da atanmış inventory var ve persistent inventory boşsa, kopyala
        if (assignedInventory != null && inventory.inventorySlots.Count > 0)
        {
            bool persistentIsEmpty = true;
            foreach (var slot in inventory.inventorySlots)
            {
                if (slot.item != null)
                {
                    persistentIsEmpty = false;
                    break;
                }
            }
            
            if (persistentIsEmpty && assignedInventory.inventorySlots.Count > 0)
            {
                Debug.Log("Merging inspector inventory to persistent inventory");
                for (int i = 0; i < assignedInventory.inventorySlots.Count && i < inventory.inventorySlots.Count; i++)
                {
                    if (assignedInventory.inventorySlots[i].item != null)
                    {
                        inventory.inventorySlots[i] = new Slot
                        {
                            item = assignedInventory.inventorySlots[i].item,
                            itemCount = assignedInventory.inventorySlots[i].itemCount,
                            isFull = assignedInventory.inventorySlots[i].isFull
                        };
                        Debug.Log($"Copied slot {i}: {assignedInventory.inventorySlots[i].item.itemName}");
                    }
                }
                inventory.TriggerInventoryChanged();
            }
        }
        
        Debug.Log($"Using inventory with {inventory.inventorySlots.Count} slots");
        
        // Inventory içeriğini debug et
        for (int i = 0; i < Mathf.Min(3, inventory.inventorySlots.Count); i++)
        {
            var slot = inventory.inventorySlots[i];
            if (slot.item != null)
            {
                Debug.Log($"Final Inventory Slot {i}: {slot.item.itemName} x{slot.itemCount}");
            }
        }
    }
    
    private void SetupUI()
    {
        // Inventory panelinin child'larını kontrol et
        if (inventoryPanel != null)
        {
            int slotIndex = 0;
            foreach (Transform child in inventoryPanel.transform)
            {
                InventorySlotUI slotUI = child.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUIs.Add(slotUI);
                    
                    // DragAndDropHandler ekle
                    DragAndDropHandler dragHandler = child.GetComponent<DragAndDropHandler>();
                    if (dragHandler == null)
                    {
                        dragHandler = child.gameObject.AddComponent<DragAndDropHandler>();
                    }
                    
                    // Drag handler'ı konfigure et
                    dragHandler.slotIndex = slotIndex;
                    dragHandler.inventory = inventory;
                    dragHandler.uiManager = this;
                    
                    slotIndex++;
                }
            }
        }
        
        Debug.Log($"UI Setup complete - SlotUIs: {slotUIs.Count}, Inventory slots: {inventory.inventorySlots.Count}");
    }
    
    private void FinalizeSetup()
    {
        // Event'i bağla
        if (inventory != null)
        {
            inventory.OnInventoryChanged += OnInventoryChanged;
            
            // İlk item count'ı kaydet
            previousItemCount = GetCurrentItemCount();
            hasInitialized = true;
        }
        
        // UI'ı ilk kez güncelle
        if (inventory != null && slotUIs.Count > 0)
        {
            Debug.Log("Calling initial UpdateUI");
            UpdateUI();
        }
        else
        {
            Debug.LogWarning($"Cannot update UI - inventory is null: {inventory == null}, slotUIs count: {slotUIs.Count}");
        }
        
        // Oyun başladığında inventory panelini gizle
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }
    
    private void Update()
    {
    if (InputHelper.GetKeyDown(KeyCode.I))
        {
            isInventoryVisible = !isInventoryVisible;
            
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(isInventoryVisible);
            }
            
            if (isInventoryVisible)
            {
                Debug.Log("Inventory panel opened - forcing UI synchronization");
                
                // Panel açıldığında inventory referansını kontrol et ve güncelle
                if (inventory == null)
                {
                    inventory = SCInventory.GetPersistentInventory();
                    inventory.OnInventoryChanged += UpdateUI;
                    Debug.Log("Re-connected to persistent inventory");
                }
                
                // Drag handler referanslarını güncelle
                UpdateDragHandlerReferences();
                
                // UI'ı zorla güncelle
                if (inventory != null && slotUIs.Count > 0)
                {
                    UpdateUI();
                    Debug.Log("Forced UI update on panel open");
                }
                else
                {
                    Debug.LogWarning($"Cannot refresh UI on open - inventory null: {inventory == null}, slotUIs: {slotUIs?.Count}");
                }
            }
        }
    }

    private void OnEnable()
    {
        // Inventory değiştiğinde UI'ı güncelle
        if (inventory != null)
        {
            inventory.OnInventoryChanged += OnInventoryChanged;
            Debug.Log("InventoryUIManager: Event connected on OnEnable");
        }
        else
        {
            Debug.LogWarning("InventoryUIManager: inventory is null in OnEnable");
        }
    }

    private void OnDisable()
    {
        // Event'i disconnect et
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= OnInventoryChanged;
            Debug.Log("InventoryUIManager: Event disconnected on OnDisable");
        }
    }    private void UpdateUI()
    {
        // Null kontrollerini ekle
        if (inventory == null || inventory.inventorySlots == null || slotUIs == null)
        {
            Debug.LogWarning("InventoryUIManager: inventory, inventorySlots veya slotUIs null!");
            return;
        }

        Debug.Log($"UpdateUI called - Inventory has {inventory.inventorySlots.Count} slots, UI has {slotUIs.Count} slot UIs");
        
        // İlk birkaç slot'un içeriğini logla
        for (int j = 0; j < Mathf.Min(3, inventory.inventorySlots.Count); j++)
        {
            var slot = inventory.inventorySlots[j];
            if (slot.item != null)
            {
                Debug.Log($"Slot {j}: {slot.item.itemName} x{slot.itemCount}");
            }
            else
            {
                Debug.Log($"Slot {j}: Empty");
            }
        }

        if (slotUIs.Count != inventory.inventorySlots.Count)
        {
            Debug.LogWarning($"InventoryUIManager: slotUIs.Count ({slotUIs.Count}) != inventorySlots.Count ({inventory.inventorySlots.Count})!");
        }
        for (int i = 0; i < inventory.inventorySlots.Count; i++)
        {
            if (i < slotUIs.Count && slotUIs[i] != null)
            {
                Slot slot = inventory.inventorySlots[i];
                InventorySlotUI slotUI = slotUIs[i];
                // SlotUI bileşenlerinin null olmadığını kontrol et
                if (slotUI.itemIcon == null || slotUI.itemCountText == null)
                {
                    Debug.LogWarning($"InventoryUIManager: SlotUI {i} bileşenleri null!");
                    continue;
                }
                // DragAndDropHandler için CanvasGroup referansını al
                DragAndDropHandler dragHandler = slotUI.GetComponent<DragAndDropHandler>();
                CanvasGroup slotCanvasGroup = slotUI.GetComponent<CanvasGroup>();
                if (slot.item != null)
                {
                    slotUI.itemIcon.sprite = slot.item.itemIcon;
                    slotUI.itemIcon.enabled = true;
                    if (slot.itemCount > 1)
                    {
                        slotUI.itemCountText.text = slot.itemCount.ToString();
                        slotUI.itemCountText.enabled = true;
                    }
                    else
                    {
                        slotUI.itemCountText.enabled = false;
                    }
                    // Slot dolu ise sürüklemeyi etkinleştir
                    if (slotCanvasGroup != null)
                    {
                        slotCanvasGroup.interactable = true;
                        slotCanvasGroup.blocksRaycasts = true;
                    }
                    // Slot arka planını occupiedColor yap
                    if (slotUI.slotBackground != null)
                    {
                        slotUI.slotBackground.color = slotUI.occupiedColor;
                        slotUI.slotBackground.enabled = true;
                    }
                }
                else
                {
                    // Slot boş ise ikon ve text'i gizle, slot GameObject'i aktif kalsın
                    slotUI.itemIcon.enabled = false;
                    slotUI.itemCountText.enabled = false;
                    if (slotCanvasGroup != null)
                    {
                        slotCanvasGroup.interactable = false;
                        slotCanvasGroup.blocksRaycasts = true;
                    }
                    // Slot arka planını normalColor yap ve aktif bırak
                    if (slotUI.slotBackground != null)
                    {
                        slotUI.slotBackground.color = slotUI.normalColor;
                        slotUI.slotBackground.enabled = true;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Inventory değiştiğinde çağrılan event handler
    /// </summary>
    private void OnInventoryChanged()
    {
        // Önce UI'ı güncelle
        UpdateUI();
        
        // Auto-open kontrolü yap
        if (autoOpenOnItemAdded && hasInitialized)
        {
            CheckForAutoOpen();
        }
    }
    
    /// <summary>
    /// Item eklendiğinde otomatik panel açma kontrolü
    /// </summary>
    private void CheckForAutoOpen()
    {
        int currentItemCount = GetCurrentItemCount();
        
        // Eğer item sayısı artmışsa ve panel kapalıysa
        if (currentItemCount > previousItemCount && !isInventoryVisible)
        {
            // Delay ile paneli aç
            Invoke(nameof(AutoOpenPanel), autoOpenDelay);
            Debug.Log($"📦 Item added! Auto-opening inventory panel. Previous: {previousItemCount}, Current: {currentItemCount}");
        }
        
        // Mevcut item count'ı kaydet
        previousItemCount = currentItemCount;
    }
    
    /// <summary>
    /// Otomatik panel açma
    /// </summary>
    private void AutoOpenPanel()
    {
        if (!isInventoryVisible)
        {
            // Paneli aç
            isInventoryVisible = true;
            
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(isInventoryVisible);
            }
            
            // Panel açılınca UI synchronization
            if (isInventoryVisible)
            {
                Debug.Log("Inventory panel auto-opened - forcing UI synchronization");
                
                // İlk açılışta event'i yeniden bağla
                if (inventory != null)
                {
                    inventory.OnInventoryChanged += OnInventoryChanged;
                }
                
                // Drag handler referanslarını güncelle
                UpdateDragHandlerReferences();
                
                // Force UI update
                if (inventory != null && slotUIs.Count > 0)
                {
                    UpdateUI();
                    Debug.Log("Forced UI update on auto panel open");
                }
            }
            
            Debug.Log("📦 Inventory panel auto-opened due to item addition!");
        }
    }
    
    /// <summary>
    /// Mevcut toplam item sayısını hesapla
    /// </summary>
    private int GetCurrentItemCount()
    {
        if (inventory == null || inventory.inventorySlots == null)
            return 0;
            
        int totalCount = 0;
        foreach (var slot in inventory.inventorySlots)
        {
            if (slot.item != null)
            {
                totalCount += slot.itemCount;
            }
        }
        return totalCount;
    }
    
    public void RefreshUI()
    {
        if (inventory != null && slotUIs != null && slotUIs.Count > 0)
        {
            UpdateUI();
        }
    }
    
    public void SetInventoryReference(SCInventory newInventory)
    {
        // Eski inventory'den event'ı kaldır
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= UpdateUI;
        }
        
        // Yeni inventory'yi ata
        inventory = newInventory;
        
        // Yeni inventory'ye event'ı ekle
        if (inventory != null)
        {
            inventory.OnInventoryChanged += UpdateUI;
        }
        
        // Drag handler'ların inventory referansını güncelle
        UpdateDragHandlerReferences();
    }
    
    public void ForceInventoryMergeAndUpdate()
    {
        // Inspector'da atanmış inventory varsa onu merge et
        SCInventory originalInventory = null;
        
        // Eğer bir inventory field atanmışsa, onu sakla
        if (inventory != null && inventory.name != "PersistentInventory")
        {
            originalInventory = inventory;
        }
        
        // Persistent inventory'yi al
        inventory = SCInventory.GetPersistentInventory();
        
        // Inspector inventory'yi merge et
        if (originalInventory != null && originalInventory.inventorySlots.Count > 0)
        {
            bool persistentIsEmpty = true;
            foreach (var slot in inventory.inventorySlots)
            {
                if (slot.item != null)
                {
                    persistentIsEmpty = false;
                    break;
                }
            }
            
            if (persistentIsEmpty)
            {
                Debug.Log("Force merging inspector inventory to persistent inventory");
                for (int i = 0; i < originalInventory.inventorySlots.Count && i < inventory.inventorySlots.Count; i++)
                {
                    if (originalInventory.inventorySlots[i].item != null)
                    {
                        inventory.inventorySlots[i] = new Slot
                        {
                            item = originalInventory.inventorySlots[i].item,
                            itemCount = originalInventory.inventorySlots[i].itemCount,
                            isFull = originalInventory.inventorySlots[i].isFull
                        };
                    }
                }
            }
        }
        
        // Event'ı yeniden bağla
        inventory.OnInventoryChanged += UpdateUI;
        
        // Drag handler'ları güncelle
        UpdateDragHandlerReferences();
        
        // UI'ı güncelle
        if (slotUIs != null && slotUIs.Count > 0)
        {
            UpdateUI();
            Debug.Log("Forced UI update completed");
        }
    }
    
    private void UpdateDragHandlerReferences()
    {
        if (inventoryPanel != null && inventory != null)
        {
            int slotIndex = 0;
            foreach (Transform child in inventoryPanel.transform)
            {
                DragAndDropHandler dragHandler = child.GetComponent<DragAndDropHandler>();
                if (dragHandler != null)
                {
                    dragHandler.inventory = inventory;
                    dragHandler.slotIndex = slotIndex;
                    dragHandler.uiManager = this;
                }
                slotIndex++;
            }
        }
    }
}