using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image itemIcon;
    public TextMeshProUGUI itemCountText;    private void Awake()
    {
        // Transform.Find ile bileşenleri bul ve null kontrolü yap
        Transform iconTransform = transform.Find("Icon");
        Transform countTransform = transform.Find("Count");
        
        if (iconTransform != null)
        {
            itemIcon = iconTransform.GetComponent<Image>();
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Icon' child bulunamadı! GameObject: {gameObject.name}");
        }
        
        if (countTransform != null)
        {
            itemCountText = countTransform.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Count' child bulunamadı! GameObject: {gameObject.name}");
        }
    }
}