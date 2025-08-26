using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlaskManager : MonoBehaviour
{
    // Panel referansı
    [SerializeField] private GameObject panel;
    
    // Panelin aktif olup olmadığını kontrol eden değişken
    private bool isPanelActive = false;
    
    [Header("Storage Settings")]
    [Tooltip("Flask deposundaki slot sayısı")] public int slotCount = 3;
    [Tooltip("Slot UI referansları (FlaskPanel altındaki slot objeleri)")]
    public Transform[] slotUIs = new Transform[3];

    // Depo verisi
    [System.Serializable]
    public class StoredSlot
    {
        public SCItem item;
        public int count;
    }
    public List<StoredSlot> storedSlots = new List<StoredSlot>();
    
    void Start()
    {
        // Başlangıçta paneli kapalı duruma getir
        if (panel != null)
        {
            panel.SetActive(false);
        }

        // Force clear all stored data first
        Debug.Log("=== FLASK MANAGER START - CLEARING DATA ===");
        InitSlots();
        
        // Debug: Show SlotUI assignments BEFORE binding
        Debug.Log("=== SLOTUI ARRAY ASSIGNMENTS ===");
        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] != null)
            {
                Debug.Log($"slotUIs[{i}] = GameObject '{slotUIs[i].name}'");
            }
            else
            {
                Debug.Log($"slotUIs[{i}] = NULL");
            }
        }
        Debug.Log("=================================");
        
        BindSlotUIs();
        
        // Debug: Verify slot assignments AFTER binding
        Debug.Log("=== VERIFYING SLOT ASSIGNMENTS ===");
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                var flaskSlotUI = t.GetComponent<FlaskSlotUI>();
                if (flaskSlotUI != null)
                {
                    Debug.Log($"GameObject '{t.name}' has slotIndex: {flaskSlotUI.slotIndex}");
                }
                else
                {
                    Debug.LogError($"GameObject '{t.name}' missing FlaskSlotUI component!");
                }
            }
        }
        Debug.Log("===================================");
    }

    void Update()
    {
        // ESC tuşuna basıldığında ve panel açıksa, paneli kapat
        if (Input.GetKeyDown(KeyCode.Escape) && isPanelActive)
        {
            ClosePanel();
        }
    }
    
    // Mouse tıklaması algılandığında çağrılır
    private void OnMouseDown()
    {
        OpenPanel();
    }
    
    // Paneli açan metod
    private void OpenPanel()
    {
        if (panel != null)
        {
            panel.SetActive(true);
            isPanelActive = true;
            // Panel arka planı varsa raycast'ı kapat ki slotlar drop alabilsin
            var panelImg = panel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.raycastTarget = false;
            }
            // Çocuklardaki yaygın arka plan img'lerini de kapat (Slotların dışında kalanlar)
            foreach (var img in panel.GetComponentsInChildren<Image>(includeInactive: true))
            {
                if (img.gameObject == panel) continue;
                // Slot arka planlarını etkileme: slotların bir FlaskSlotUI bileşeni vardır
                if (img.GetComponentInParent<FlaskSlotUI>() != null)
                {
                    // Slot içindeki itemIcon gibi elemanlarda raycast target kapalı kalabilir
                    continue;
                }
                // Tam ekran/katman arka planı gibi Image’ların raycast'ını kapat
                img.raycastTarget = false;
            }
            RefreshUI();
        }
        else
        {
            Debug.LogError("Panel atanmamış! Inspector'da panel referansını ayarlayın.");
        }
    }
    
    // Paneli kapatan metod
    private void ClosePanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            isPanelActive = false;
        }
    }

    // Panel durumunu kontrol etmek için public metod
    public bool IsPanelActive()
    {
        return isPanelActive;
    }

    // ----- Storage Logic -----
    private void InitSlots()
    {
        Debug.Log($"InitSlots called: creating {slotCount} empty slots");
        storedSlots.Clear();
        for (int i = 0; i < Mathf.Max(1, slotCount); i++)
        {
            storedSlots.Add(new StoredSlot());
            Debug.Log($"Created empty slot {i}");
        }
    }

    private void BindSlotUIs()
    {
        Debug.Log("BindSlotUIs called");
        if (slotUIs == null) return;
        
        Debug.Log("=== SLOT UI BINDING ===");
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                Debug.Log($"Binding slot index {i} to GameObject '{t.name}' at array position {i}");
                
                // Use direct component access instead of SendMessage
                var flaskSlotUI = t.GetComponent<FlaskSlotUI>();
                if (flaskSlotUI != null)
                {
                    flaskSlotUI.slotIndex = i;
                    flaskSlotUI.flaskManager = this;
                    Debug.Log($"DIRECT: Set {t.name} slotIndex to {i}");
                }
                else
                {
                    Debug.LogError($"GameObject '{t.name}' is missing FlaskSlotUI component!");
                }
                
                // Also try SendMessage as backup
                t.SendMessage("SetSlotIndex", i, SendMessageOptions.DontRequireReceiver);
                t.SendMessage("InitFlaskSlot", this, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning($"SlotUI at array position {i} is null!");
            }
        }
        Debug.Log("=====================");
    }

    public bool TryAddItemToSlot(int slotIndex, SCItem item)
    {
        Debug.Log($"TryAddItemToSlot called: slotIndex={slotIndex}, item={item?.itemName}");
        
        // Debug: Show all slots status
        Debug.Log("=== ALL SLOTS STATUS ===");
        for (int i = 0; i < storedSlots.Count; i++)
        {
            var s = storedSlots[i];
            Debug.Log($"Slot {i}: item={s.item?.itemName}, count={s.count}");
        }
        Debug.Log("========================");
        
        if (item == null) return false;
        if (slotIndex < 0 || slotIndex >= storedSlots.Count) 
        {
            Debug.LogError($"Invalid slotIndex: {slotIndex}, storedSlots.Count: {storedSlots.Count}");
            return false;
        }

        var slot = storedSlots[slotIndex];
        Debug.Log($"Target slot {slotIndex}: item={slot.item?.itemName}, count={slot.count}");
        
        if (slot.item == null)
        {
            // Boş slot: item'i yerleştir
            Debug.Log($"Placing item {item.itemName} in empty slot {slotIndex}");
            slot.item = item;
            slot.count = 1;
            RefreshUI();
            return true;
        }
        else if (slot.item == item && item.canStackable)
        {
            // Aynı item ve stacklenebilir: sayıyı artır
            Debug.Log($"Stacking item {item.itemName} in slot {slotIndex}, new count: {slot.count + 1}");
            slot.count++;
            RefreshUI();
            return true;
        }
        else
        {
            // Slot dolu ve farklı item: drop işlemini reddet
            Debug.LogWarning($"REJECTED: Slot {slotIndex} is occupied with {slot.item.itemName}, cannot place {item.itemName}");
            return false;
        }
    }

    public bool TryTakeOneFromSlotToInventory(int slotIndex, SCInventory targetInventory)
    {
        Debug.Log($"TryTakeOneFromSlotToInventory called: slotIndex={slotIndex}");
        
        if (slotIndex < 0 || slotIndex >= storedSlots.Count) 
        {
            Debug.LogError($"Invalid slotIndex: {slotIndex}, storedSlots.Count: {storedSlots.Count}");
            return false;
        }
        if (targetInventory == null) 
        {
            Debug.LogError("targetInventory is null");
            return false;
        }

        var slot = storedSlots[slotIndex];
        Debug.Log($"Slot {slotIndex}: item={slot.item?.itemName}, count={slot.count}");
        
        if (slot.item == null || slot.count <= 0) 
        {
            Debug.Log($"Slot {slotIndex} is empty or has no count");
            return false;
        }

        // Deneme: önce envantere ekleyebilir miyiz?
        bool added = targetInventory.AddItem(slot.item);
        Debug.Log($"Adding {slot.item.itemName} to inventory: {added}");
        if (!added)
        {
            Debug.LogWarning("Envantere eklenemedi: boş slot yok veya stack limiti dolu.");
            return false;
        }

        // Başarılı ise Flask slotundan 1 azalt
        slot.count--;
        Debug.Log($"Decremented slot {slotIndex} count to {slot.count}");
        if (slot.count <= 0)
        {
            Debug.Log($"Clearing slot {slotIndex}");
            slot.item = null;
            slot.count = 0;
        }
        RefreshUI();
        return true;
    }

    public void RefreshUI()
    {
        Debug.Log("RefreshUI called");
        if (slotUIs == null) return;
        
        Debug.Log("=== REFRESH UI DEBUG ===");
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                var slot = storedSlots[i];
                Debug.Log($"Refreshing UI for slot {i}: GameObject='{t.name}', item={slot.item?.itemName}, count={slot.count}");
                t.SendMessage("UpdateFlaskSlotUI", storedSlots[i], SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning($"SlotUI {i} is null during refresh!");
            }
        }
        Debug.Log("========================");
    }
}