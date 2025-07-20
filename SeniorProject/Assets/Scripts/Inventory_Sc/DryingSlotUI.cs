using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class DryingSlotUI : MonoBehaviour, IDropHandler
{
    [Header("UI Components")]
    public Image itemIcon;
    public TextMeshProUGUI timerText;
    public Button collectButton;
    public Image slotBackground;
    
    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color occupiedColor = Color.gray;
    public Color readyColor = Color.green;
    
    [HideInInspector]
    public int slotIndex;
    [HideInInspector]
    public DryingAreaUI dryingAreaUI;

    public void UpdateSlotUI(DryingSlot slot)
    {
        // Icon güncelleme
        if (itemIcon != null)
        {
            if (slot.currentItemData != null)
            {
                itemIcon.sprite = slot.currentItemData.itemIcon;
                itemIcon.enabled = true;
            }
            else
            {
                itemIcon.enabled = false;
            }
        }
        
        // Timer text güncelleme
        if (timerText != null)
        {
            if (slot.isOccupied && !slot.isReadyToCollect)
            {
                timerText.text = Mathf.Ceil(slot.timer).ToString() + "s";
            }
            else if (slot.isReadyToCollect)
            {
                timerText.text = "Ready!";
            }
            else
            {
                timerText.text = "Empty";
            }
        }
        
        // Collect button güncelleme
        if (collectButton != null)
        {
            collectButton.gameObject.SetActive(slot.isReadyToCollect);
        }
        
        // Background color güncelleme
        if (slotBackground != null)
        {
            if (slot.isReadyToCollect)
            {
                slotBackground.color = readyColor;
            }
            else if (slot.isOccupied)
            {
                slotBackground.color = occupiedColor;
            }
            else
            {
                slotBackground.color = normalColor;
            }
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Inventory'den sürüklenen item'ı al
        DragAndDropHandler dragHandler = eventData.pointerDrag?.GetComponent<DragAndDropHandler>();
        
        if (dragHandler != null && dragHandler.inventory != null)
        {
            int sourceSlotIndex = dragHandler.slotIndex;
            SCItem draggedItem = dragHandler.inventory.inventorySlots[sourceSlotIndex].item;
            
            // Item drying için uygun mu kontrol et
            if (draggedItem != null && draggedItem.canBeDried)
            {
                // Slot'a eklemeyi dene
                bool success = dryingAreaUI.TryAddItemToSlot(slotIndex, draggedItem);
                
                if (success)
                {
                    // Inventory'den item'ı kaldır (sadece 1 tane)
                    if (dragHandler.inventory.inventorySlots[sourceSlotIndex].itemCount > 1)
                    {
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].itemCount--;
                    }
                    else
                    {
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].item = null;
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].itemCount = 0;
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].isFull = false;
                    }
                    
                    // Inventory değişikliğini tetikle
                    dragHandler.inventory.TriggerInventoryChanged();
                }
            }
            else
            {
                Debug.Log("Bu item kurutma için uygun değil!");
            }
        }
    }
}
