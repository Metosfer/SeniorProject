using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class DryingSlot
{
    [Header("3D World References")]
    public Transform slotTransform; // Objenin yerleştirileceği konum (dünyada)
    public GameObject currentObject;
    
    [Header("Item Data")]
    public SCItem currentItemData;
    public float timer;
    public bool isOccupied;
    public bool isReadyToCollect;
    
    [Header("UI References")]
    public Image slotIcon; // UI'daki slot ikonları
    public TextMeshProUGUI timerText; // Geri sayım text'i
    public Button collectButton; // Toplama butonu
    public CanvasGroup slotCanvasGroup; // Drag and drop için
    
    public void ResetSlot()
    {
        isOccupied = false;
        isReadyToCollect = false;
        currentObject = null;
        currentItemData = null;
        timer = 0f;
        
        if (slotIcon != null) 
        {
            slotIcon.sprite = null;
            slotIcon.enabled = false;
        }
        if (timerText != null) timerText.text = "";
        if (collectButton != null) collectButton.gameObject.SetActive(false);
        if (slotCanvasGroup != null) slotCanvasGroup.interactable = true;
    }
    
    public void UpdateUI()
    {
        if (currentItemData != null && slotIcon != null)
        {
            slotIcon.sprite = currentItemData.itemIcon;
            slotIcon.enabled = true;
        }
        
        if (timerText != null)
        {
            if (isOccupied && !isReadyToCollect)
            {
                timerText.text = Mathf.Ceil(timer).ToString() + "s";
            }
            else if (isReadyToCollect)
            {
                timerText.text = "Ready!";
            }
            else
            {
                timerText.text = "";
            }
        }
        
        if (collectButton != null)
        {
            collectButton.gameObject.SetActive(isReadyToCollect);
        }
        
        if (slotCanvasGroup != null)
        {
            slotCanvasGroup.interactable = !isOccupied || isReadyToCollect;
        }
    }
}

