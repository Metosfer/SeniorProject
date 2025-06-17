using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Inventory", menuName = "Inventory/New Inventory")]
public class SCInventory : ScriptableObject
{
    public List<Slot> inventorySlots = new List<Slot>();
    int stackLimit = 4;
    public event System.Action OnInventoryChanged;    public bool AddItem(SCItem item)
    {
        // Önce aynı türden item'ı stack'lemeye çalış
        foreach (Slot slot in inventorySlots)
        {
            if (slot.item == item && slot.item.canStackable)
            {
                if (slot.itemCount < stackLimit)
                {
                    slot.itemCount++;
                    if (slot.itemCount == stackLimit)
                    {
                        slot.isFull = true;
                    }
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        // Eğer stack'lenemiyorsa, boş slot ara
        foreach (Slot slot in inventorySlots)
        {
            if (slot.itemCount == 0)
            {
                slot.AddItemToSlot(item);
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        // Hiç boş slot yoksa
        return false;
    }public void ResetInventory()
    {
        if (inventorySlots == null)
        {
            Debug.LogWarning("SCInventory: inventorySlots null!");
            return;
        }
        
        foreach (Slot slot in inventorySlots)
        {
            if (slot != null)
            {
                slot.item = null;
                slot.itemCount = 0;
                slot.isFull = false;
            }
        }
        OnInventoryChanged?.Invoke();
    }

    public void TriggerInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}

[System.Serializable]
public class Slot
{
    public bool isFull;
    public int itemCount;
    public SCItem item;

    public void AddItemToSlot(SCItem item)
    {
        this.item = item;
        if (item.canStackable == false)
        {
            isFull = true;
        }
        itemCount++;
    }
}