using UnityEngine;

public class DropZone : MonoBehaviour
{
    [Header("Drop Zone Settings")]
    public bool acceptAllItems = true;
    public string[] acceptedItemTypes; // Sadece belirli item türlerini kabul etmek için
    
    [Header("Visual Feedback")]
    public GameObject dropIndicator; // Drop zone'un görsel göstergesi
    public Color normalColor = Color.white;
    public Color highlightColor = Color.green;
    public Color invalidColor = Color.red;

    private Renderer dropZoneRenderer;
    private bool isHighlighted = false;

    private void Start()
    {
        dropZoneRenderer = GetComponent<Renderer>();
        if (dropZoneRenderer != null)
        {
            dropZoneRenderer.material.color = normalColor;
        }
        
        // Drop indicator başlangıçta gizli
        if (dropIndicator != null)
        {
            dropIndicator.SetActive(false);
        }
    }    public virtual bool CanAcceptDrop()
    {
        return true; // Şimdilik her drop'u kabul et
    }

    public bool CanAcceptItem(SCItem item)
    {
        if (acceptAllItems)
            return true;

        // Belirli item türlerini kontrol et
        foreach (string itemType in acceptedItemTypes)
        {
            if (item.itemName.Contains(itemType))
                return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Player drop zone'a yaklaştığında görsel feedback
        if (other.CompareTag("Player"))
        {
            HighlightDropZone(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HighlightDropZone(false);
        }
    }

    private void HighlightDropZone(bool highlight)
    {
        isHighlighted = highlight;
        
        if (dropZoneRenderer != null)
        {
            dropZoneRenderer.material.color = highlight ? highlightColor : normalColor;
        }
        
        if (dropIndicator != null)
        {
            dropIndicator.SetActive(highlight);
        }
    }

    public void ShowInvalidDrop()
    {
        if (dropZoneRenderer != null)
        {
            dropZoneRenderer.material.color = invalidColor;
            // 0.5 saniye sonra normal renge geri dön
            Invoke(nameof(ResetColor), 0.5f);
        }
    }

    private void ResetColor()
    {
        if (dropZoneRenderer != null)
        {
            dropZoneRenderer.material.color = isHighlighted ? highlightColor : normalColor;
        }
    }
}
