using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DragAndDropHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 originalPosition;
    private Transform originalParent;
    private InventorySlotUI inventorySlot;
    private GameObject dragIcon;
    
    public int slotIndex { get; set; }
    public SCInventory inventory { get; set; }
    public InventoryUIManager uiManager { get; set; }

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        rectTransform = GetComponent<RectTransform>();
        inventorySlot = GetComponent<InventorySlotUI>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // CanvasGroup interactable kontrolü
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            return;
        }
        
        // Eğer slot boşsa sürükleme başlatma
        if (inventory == null || slotIndex >= inventory.inventorySlots.Count || 
            inventory.inventorySlots[slotIndex].item == null)
        {
            return;
        }

        // Orijinal pozisyon ve parent'ı kaydet
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;

        // Sürükleme sırasında slotu gizleme, alpha'yı değiştirme. Sadece raycast'i kapat.
        canvasGroup.blocksRaycasts = false;

        // Drag icon oluştur (sadece item ikonu kopyası)
        if (dragIcon == null && inventorySlot != null && inventorySlot.itemIcon != null && inventorySlot.itemIcon.sprite != null)
        {
            dragIcon = new GameObject("DragIcon");
            dragIcon.transform.SetParent(canvas.transform, false);
            var iconImage = dragIcon.AddComponent<Image>();
            iconImage.sprite = inventorySlot.itemIcon.sprite;
            iconImage.raycastTarget = false;
            var iconRect = dragIcon.GetComponent<RectTransform>();
            iconRect.sizeDelta = inventorySlot.itemIcon.rectTransform.sizeDelta;
            // Mouse pozisyonuna göre başlangıç konumunu ayarla
            Vector2 localPointerPosition;
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                cam,
                out localPointerPosition);
            iconRect.localPosition = localPointerPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // CanvasGroup interactable kontrolü
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            return;
        }
        
        // Eğer slot boşsa sürüklemeyi durdurmadan önce kontrol et
        if (inventory == null || slotIndex >= inventory.inventorySlots.Count || 
            inventory.inventorySlots[slotIndex].item == null)
        {
            return;
        }

        // Drag icon'u mouse ile takip ettir
        if (dragIcon != null)
        {
            Vector2 localPointerPosition;
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                cam,
                out localPointerPosition);
            dragIcon.GetComponent<RectTransform>().localPosition = localPointerPosition;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // CanvasGroup interactable kontrolü
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            return;
        }
        
        // Eğer slot boşsa işlemi sonlandır
        if (inventory == null || slotIndex >= inventory.inventorySlots.Count || 
            inventory.inventorySlots[slotIndex].item == null)
        {
            ResetPosition();
            return;
        }

        // Drag icon'u yok et
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
        canvasGroup.blocksRaycasts = true;

        // Mouse altındaki objeyi kontrol et
        GameObject hitObject = eventData.pointerCurrentRaycast.gameObject;
        DropZone dropZone = null;

        // DropZone bul (hit objesinin kendisinde veya parent'larında)
        if (hitObject != null)
        {
            dropZone = hitObject.GetComponent<DropZone>();
            if (dropZone == null)
            {
                dropZone = hitObject.GetComponentInParent<DropZone>();
            }
        }

        // Eğer geçerli bir drop zone bulunursa
        if (dropZone != null && dropZone.CanAcceptDrop())
        {
            // Sadece 1 tane eşyayı dünyada spawn et
            Slot currentSlot = inventory.inventorySlots[slotIndex];
            Vector3 worldPosition = GetWorldDropPosition(eventData);
            
            WorldItemSpawner.SpawnItem(currentSlot.item, worldPosition, 1); // Sadece 1 tane
            
            // Inventory'den 1 tane azalt
            RemoveSingleItemFromInventory();
        }
        // Eğer UI canvas'ın dışında bırakılırsa (geçerli bir UI objesi değilse)
        else if (hitObject == null || (!IsUIElement(hitObject)))
        {
            // Canvas dışında bırakıldı, dünyaya sadece 1 tane spawn et
            Slot currentSlot = inventory.inventorySlots[slotIndex];
            Vector3 worldPosition = GetWorldDropPosition(eventData);
            
            WorldItemSpawner.SpawnItem(currentSlot.item, worldPosition, 1); // Sadece 1 tane
            
            // Inventory'den 1 tane azalt
            RemoveSingleItemFromInventory();
        }
        else
        {
            // Geçerli bir drop zone yoksa eski pozisyona geri dön
            ResetPosition();
        }
    }

    private bool IsUIElement(GameObject obj)
    {
        // Objenin UI elementi olup olmadığını kontrol et
        return obj.GetComponent<RectTransform>() != null || 
               obj.GetComponentInParent<Canvas>() != null;
    }

    private Vector3 GetWorldDropPosition(PointerEventData eventData)
    {
        // Camera'dan ray çek
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera != null)
        {
            // Mouse pozisyonundan ray çek
            Ray ray = mainCamera.ScreenPointToRay(eventData.position);
            RaycastHit hit;

            // Zemin ile çarpışma kontrolü (sadece zemin layer'ında)
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                return hit.point + Vector3.up * 0.5f; // Yere değmemesi için biraz yukarı
            }
            else
            {
                // Eğer çarpışma yoksa varsayılan pozisyon
                Vector3 worldPos = ray.GetPoint(10f);
                worldPos.y = 0.5f;
                return worldPos;
            }
        }

        // Fallback pozisyon
        return Vector3.zero + Vector3.up * 0.5f;
    }

    private void RemoveSingleItemFromInventory()
    {
        if (inventory != null && slotIndex < inventory.inventorySlots.Count)
        {
            Slot slot = inventory.inventorySlots[slotIndex];
            if (slot.itemCount > 1)
            {
                // Eğer 1'den fazla item varsa, sadece 1 azalt
                slot.itemCount--;
                if (slot.isFull)
                {
                    slot.isFull = false;
                }
            }
            else
            {
                // Son item ise, sadece içeriği temizle, slotu asla yok etme
                slot.item = null;
                slot.itemCount = 0;
                slot.isFull = false;
            }
            // Inventory değişikliğini tetikle
            inventory.TriggerInventoryChanged();
        }

        ResetPosition();
    }

    private void RemoveItemFromInventory()
    {
        if (inventory != null && slotIndex < inventory.inventorySlots.Count)
        {
            Slot slot = inventory.inventorySlots[slotIndex];
            slot.item = null;
            slot.itemCount = 0;
            slot.isFull = false;
            
            // Inventory değişikliğini tetikle
            inventory.TriggerInventoryChanged();
        }

        ResetPosition();
    }

    private void ResetPosition()
    {
        // Drag icon'u yok et
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
        // Orijinal parent ve pozisyona geri dön
        transform.SetParent(originalParent, false);
        rectTransform.anchoredPosition = originalPosition;
        // CanvasGroup'u her durumda görünür ve raycast yapılabilir yap
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }
}
