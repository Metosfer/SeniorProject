using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Inventory", menuName = "Inventory/New Inventory")]
public class SCInventory : ScriptableObject
{
    public List<Slot> inventorySlots = new List<Slot>();
    int stackLimit = 4;

    public bool AddItem(SCItem item)
    {
        // Önce tüm slotların dolu olup olmadığını kontrol et
        bool allSlotsFull = true;
        foreach (Slot slot in inventorySlots)
        {
            if (!slot.isFull)
            {
                allSlotsFull = false;
                break;
            }
        }
        if (allSlotsFull)
        {
            return false; // Tüm slotlar doluysa eşya eklenemez
        }

        // Normal eşya ekleme mantığı
        foreach (Slot slot in inventorySlots)
        {
            if (slot.item == item)
            {
                if (slot.item.canStackable)
                {
                    if (slot.itemCount < stackLimit)
                    {
                        slot.itemCount++;
                        if (slot.itemCount == stackLimit)
                        {
                            slot.isFull = true;
                            return false;
                        }
                        return true;
                    }
                }
            }
            else if (slot.itemCount == 0)
            {
                slot.AddItemToSlot(item);
                return true;
            }
        }
        return false;
    }

    public void ResetInventory()
    {
        foreach (Slot slot in inventorySlots)
        {
            slot.item = null;
            slot.itemCount = 0;
            slot.isFull = false;
        }
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