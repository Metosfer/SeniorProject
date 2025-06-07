using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Inventory", menuName = "Inventory/New Inventory")]
public class SCInventory : ScriptableObject
{
    public List<Slot> inventorySlots = new List<Slot>();
    int stackLimit = 4;

    public bool AddItem(SCItem item) // Corrected from Item to SCItem
    {
        foreach (Slot slot in inventorySlots)
        {
            if (slot.item == item)
            {
                if (slot.item.canStackable)
                {
                    if (slot.itemCount < stackLimit)
                    {
                        slot.itemCount++;
                        if (slot.itemCount == stackLimit) // Corrected from > to ==
                        {
                            slot.isFull = true;
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