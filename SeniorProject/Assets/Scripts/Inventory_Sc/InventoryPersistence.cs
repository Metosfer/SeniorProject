using UnityEngine;
using System.Collections.Generic;

public static class InventoryPersistence
{
    private const string INVENTORY_KEY = "PlayerInventory";
    
    [System.Serializable]
    public class SerializableSlot
    {
        public string itemName;
        public int itemCount;
        public bool isFull;
        
        public SerializableSlot(string name, int count, bool full)
        {
            itemName = name;
            itemCount = count;
            isFull = full;
        }
    }
    
    [System.Serializable]
    public class SerializableInventory
    {
        public List<SerializableSlot> slots = new List<SerializableSlot>();
    }
    
    public static void SaveInventory(SCInventory inventory)
    {
        if (inventory == null) return;
        
        SerializableInventory serializableInv = new SerializableInventory();
        
        foreach (Slot slot in inventory.inventorySlots)
        {
            string itemName = slot.item != null ? slot.item.itemName : "";
            SerializableSlot serializableSlot = new SerializableSlot(itemName, slot.itemCount, slot.isFull);
            serializableInv.slots.Add(serializableSlot);
        }
        
        string json = JsonUtility.ToJson(serializableInv);
        PlayerPrefs.SetString(INVENTORY_KEY, json);
        PlayerPrefs.Save();
        
        Debug.Log("Inventory saved to PlayerPrefs");
    }
    
    public static void LoadInventory(SCInventory inventory, SCItem[] allItems)
    {
        if (inventory == null || allItems == null) return;
        
        string json = PlayerPrefs.GetString(INVENTORY_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log("No saved inventory found");
            return;
        }
        
        try
        {
            SerializableInventory serializableInv = JsonUtility.FromJson<SerializableInventory>(json);
            
            for (int i = 0; i < serializableInv.slots.Count && i < inventory.inventorySlots.Count; i++)
            {
                SerializableSlot savedSlot = serializableInv.slots[i];
                Slot inventorySlot = inventory.inventorySlots[i];
                
                // Item'ı bul
                SCItem foundItem = null;
                if (!string.IsNullOrEmpty(savedSlot.itemName))
                {
                    foreach (SCItem item in allItems)
                    {
                        if (item.itemName == savedSlot.itemName)
                        {
                            foundItem = item;
                            break;
                        }
                    }
                }
                
                // Slot'u doldur
                inventorySlot.item = foundItem;
                inventorySlot.itemCount = savedSlot.itemCount;
                inventorySlot.isFull = savedSlot.isFull;
            }
            
            inventory.TriggerInventoryChanged();
            Debug.Log("Inventory loaded from PlayerPrefs");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load inventory: {e.Message}");
        }
    }
    
    public static void ClearSavedInventory()
    {
        PlayerPrefs.DeleteKey(INVENTORY_KEY);
        PlayerPrefs.Save();
        Debug.Log("Saved inventory cleared");
    }
    
    public static bool HasSavedInventory()
    {
        return PlayerPrefs.HasKey(INVENTORY_KEY);
    }
}
