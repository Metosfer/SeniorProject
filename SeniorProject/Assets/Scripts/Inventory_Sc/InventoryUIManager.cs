using UnityEngine;
using System.Collections.Generic;

public class InventoryUIManager : MonoBehaviour
{
    public SCInventory inventory;
    private List<InventorySlotUI> slotUIs;
    private bool isInventoryVisible = false;

    private void Start()
    {
        slotUIs = new List<InventorySlotUI>();
        foreach (Transform child in transform)
        {
            InventorySlotUI slotUI = child.GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUIs.Add(slotUI);
            }
        }
        UpdateUI();
        gameObject.SetActive(true); // Oyun başladığında envanter UI'sını gizle
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            isInventoryVisible = !isInventoryVisible;
            gameObject.SetActive(isInventoryVisible);
            if (isInventoryVisible)
            {
                UpdateUI(); // Envanter açıldığında UI'yı güncelle
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
    }

    private void UpdateUI()
    {
        for (int i = 0; i < inventory.inventorySlots.Count; i++)
        {
            if (i < slotUIs.Count)
            {
                Slot slot = inventory.inventorySlots[i];
                InventorySlotUI slotUI = slotUIs[i];
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
}