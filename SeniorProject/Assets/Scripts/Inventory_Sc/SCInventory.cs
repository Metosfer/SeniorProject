using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Inventory", menuName = "Inventory/New Inventory")]
public class SCInventory : ScriptableObject
{
    [System.NonSerialized] // Bu field serialize edilmeyecek, böylece Unity Inspector'da değişmez
    private static SCInventory _persistentInventory;
    
    [System.NonSerialized]
    private static bool _hasBeenResetThisSession = false; // Play mode başlangıcında bir kez sıfırla
    
    public List<Slot> inventorySlots = new List<Slot>();
    int stackLimit = 4;
    public event System.Action OnInventoryChanged;
    
    // Public property olarak slots'a erişim
    public List<Slot> slots => inventorySlots;
    
    // Persistent inventory instance'ı al/oluştur
    public static SCInventory GetPersistentInventory()
    {
        if (_persistentInventory == null)
        {
            _persistentInventory = CreateInstance<SCInventory>();
            _persistentInventory.name = "PersistentInventory";
            _persistentInventory.InitializeSlots();
            
            // Play mode başlangıcında bir kez sıfırla
            if (!_hasBeenResetThisSession)
            {
                _persistentInventory.ResetInventory();
                _hasBeenResetThisSession = true;
                Debug.Log("Persistent inventory reset for new play session");
            }
        }
        return _persistentInventory;
    }
    
    // Slot'ları başlat
    private void InitializeSlots()
    {
        if (inventorySlots.Count == 0)
        {
            // Varsayılan olarak 12 slot oluştur
            for (int i = 0; i < 12; i++)
            {
                inventorySlots.Add(new Slot());
            }
        }
    }    public bool AddItem(SCItem item)
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
    }
    
    // Quantity parametreli AddItem metodu
    public bool AddItem(SCItem item, int quantity)
    {
        for (int i = 0; i < quantity; i++)
        {
            bool added = AddItem(item);
            if (!added)
            {
                Debug.LogWarning($"Could not add all items. Added {i} out of {quantity} {item.itemName}");
                return i > 0; // En az bir item eklendiyse true döndür
            }
        }
        return true;
    }

    // Item'ı envanterden çıkar
    public bool RemoveItem(SCItem item, int quantity = 1)
    {
        int remainingToRemove = quantity;
        
        // Önce stack'lenmiş item'ları çıkar
        foreach (Slot slot in inventorySlots)
        {
            if (slot.item == item && slot.itemCount > 0)
            {
                int canRemove = Mathf.Min(remainingToRemove, slot.itemCount);
                slot.itemCount -= canRemove;
                remainingToRemove -= canRemove;
                
                if (slot.itemCount == 0)
                {
                    slot.item = null;
                    slot.isFull = false;
                }
                else if (slot.item.canStackable && slot.itemCount < stackLimit)
                {
                    slot.isFull = false;
                }
                
                if (remainingToRemove <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }
        
        // Yeterli item bulunamadıysa
        if (remainingToRemove > 0)
        {
            Debug.LogWarning($"Could not remove all items. Removed {quantity - remainingToRemove} out of {quantity} {item.itemName}");
            return quantity != remainingToRemove; // En az bir item çıkarıldıysa true
        }
        
        OnInventoryChanged?.Invoke();
        return true;
    }

    // Belirli bir item'ın toplam miktarını döndür
    public int GetItemCount(SCItem item)
    {
        int totalCount = 0;
        foreach (Slot slot in inventorySlots)
        {
            if (slot.item == item)
            {
                totalCount += slot.itemCount;
            }
        }
        return totalCount;
    }

    // Belirli bir item'ın envanterde olup olmadığını kontrol et
    public bool HasItem(SCItem item, int minQuantity = 1)
    {
        return GetItemCount(item) >= minQuantity;
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