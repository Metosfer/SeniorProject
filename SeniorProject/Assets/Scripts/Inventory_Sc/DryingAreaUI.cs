using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DryingAreaUI : MonoBehaviour, IDropHandler
{
    [Header("UI References")]
    public GameObject dryingPanel; // Kurutma paneli
    public DryingAreaManager dryingManager; // DryingAreaManager referansı
    
    [Header("Slot UI References")]
    public DryingSlotUI[] slotUIs = new DryingSlotUI[3]; // 3 slot için UI referansları
    
    private bool isPanelOpen = false;

    private void Start()
    {
        // Panel başlangıçta kapalı
        if (dryingPanel != null)
        {
            dryingPanel.SetActive(false);
        }
        
        // Her slot UI'sını ayarla
        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] != null)
            {
                slotUIs[i].slotIndex = i;
                slotUIs[i].dryingAreaUI = this;
                
                // Collect butonunu ayarla
                if (slotUIs[i].collectButton != null)
                {
                    int index = i; // Closure için
                    slotUIs[i].collectButton.onClick.AddListener(() => CollectFromSlot(index));
                }
            }
        }
    }

    private void Update()
    {
        // Panel açıksa slot UI'larını güncelle
        if (isPanelOpen && dryingManager != null)
        {
            UpdateSlotUIs();
        }
    }

    public void OpenPanel()
    {
        isPanelOpen = true;
        if (dryingPanel != null)
        {
            dryingPanel.SetActive(true);
        }
        UpdateSlotUIs();
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
        if (dryingPanel != null)
        {
            dryingPanel.SetActive(false);
        }
    }

    public void TogglePanel()
    {
        if (isPanelOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    private void UpdateSlotUIs()
    {
        if (dryingManager == null) return;
        
        for (int i = 0; i < slotUIs.Length && i < dryingManager.dryingSlots.Length; i++)
        {
            DryingSlot slot = dryingManager.dryingSlots[i];
            DryingSlotUI slotUI = slotUIs[i];
            
            if (slotUI != null)
            {
                slotUI.UpdateSlotUI(slot);
            }
        }
    }

    public bool TryAddItemToSlot(int slotIndex, SCItem item)
    {
        if (dryingManager != null)
        {
            return dryingManager.TryAddItemToSlot(slotIndex, item);
        }
        return false;
    }

    public void CollectFromSlot(int slotIndex)
    {
        if (dryingManager != null)
        {
            dryingManager.CollectSlot(slotIndex);
        }
    }

    // IDropHandler implementasyonu - UI paneli üzerine drop için
    public void OnDrop(PointerEventData eventData)
    {
        // Bu method slotlara drop için kullanılacak
        // Detayları DryingSlotUI'da implement edilecek
    }
}
