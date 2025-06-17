using UnityEngine;
using System.Collections.Generic;

public class InventoryUIManager : MonoBehaviour
{
    public SCInventory inventory;
    public GameObject inventoryPanel; // Inventory paneline referans
    private List<InventorySlotUI> slotUIs;
    public bool isInventoryVisible = false;    private void Start()
    {
        slotUIs = new List<InventorySlotUI>();
        
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
        
        // Sadece inventory ve slotUIs mevcut ise UpdateUI çağır
        if (inventory != null && slotUIs.Count > 0)
        {
            UpdateUI();
        }
        
        // Oyun başladığında inventory panelini gizle
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            isInventoryVisible = !isInventoryVisible;
            
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(isInventoryVisible);
            }
              if (isInventoryVisible)
            {
                // Sadece inventory ve slotUIs mevcut ise UpdateUI çağır
                if (inventory != null && slotUIs.Count > 0)
                {
                    UpdateUI(); // Envanter açıldığında UI'yı güncelle
                }
            }
        }
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += UpdateUI;
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= UpdateUI;
        }
    }    private void UpdateUI()
    {
        // Null kontrollerini ekle
        if (inventory == null || inventory.inventorySlots == null || slotUIs == null)
        {
            Debug.LogWarning("InventoryUIManager: inventory, inventorySlots veya slotUIs null!");
            return;
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
                }
                else
                {
                    slotUI.itemIcon.enabled = false;
                    slotUI.itemCountText.enabled = false;
                }
            }
        }
    }
    
    public void RefreshUI()
    {
        if (inventory != null && slotUIs != null && slotUIs.Count > 0)
        {
            UpdateUI();
        }
    }
}