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

        InitSlots();
        BindSlotUIs();
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
        storedSlots.Clear();
        for (int i = 0; i < Mathf.Max(1, slotCount); i++)
        {
            storedSlots.Add(new StoredSlot());
        }
    }

    private void BindSlotUIs()
    {
        if (slotUIs == null) return;
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                t.SendMessage("SetSlotIndex", i, SendMessageOptions.DontRequireReceiver);
                t.SendMessage("InitFlaskSlot", this, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    public bool TryAddItemToSlot(int slotIndex, SCItem item)
    {
        if (item == null) return false;
        if (slotIndex < 0 || slotIndex >= storedSlots.Count) return false;

        var slot = storedSlots[slotIndex];
        if (slot.item == null)
        {
            slot.item = item;
            slot.count = 1;
            RefreshUI();
            return true;
        }
        else if (slot.item == item && item.canStackable)
        {
            slot.count++;
            RefreshUI();
            return true;
        }
        return false;
    }

    public bool TryTakeOneFromSlotToInventory(int slotIndex, SCInventory targetInventory)
    {
        if (slotIndex < 0 || slotIndex >= storedSlots.Count) return false;
        if (targetInventory == null) return false;

        var slot = storedSlots[slotIndex];
        if (slot.item == null || slot.count <= 0) return false;

        // Deneme: önce envantere ekleyebilir miyiz?
        bool added = targetInventory.AddItem(slot.item);
        if (!added)
        {
            Debug.LogWarning("Envantere eklenemedi: boş slot yok veya stack limiti dolu.");
            return false;
        }

        // Başarılı ise Flask slotundan 1 azalt
        slot.count--;
        if (slot.count <= 0)
        {
            slot.item = null;
            slot.count = 0;
        }
        RefreshUI();
        return true;
    }

    public void RefreshUI()
    {
        if (slotUIs == null) return;
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                t.SendMessage("UpdateFlaskSlotUI", storedSlots[i], SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}