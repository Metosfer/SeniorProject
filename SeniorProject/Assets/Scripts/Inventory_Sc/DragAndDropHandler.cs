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
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            return;
        }
        
        if (inventory == null || slotIndex >= inventory.inventorySlots.Count || 
            inventory.inventorySlots[slotIndex].item == null)
        {
            return;
        }

        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;

        canvasGroup.blocksRaycasts = false;

        if (dragIcon == null && inventorySlot != null && inventorySlot.itemIcon != null && inventorySlot.itemIcon.sprite != null && canvas != null)
        {
            Debug.Log("Creating dragIcon...");
            dragIcon = new GameObject("DragIcon");
            dragIcon.SetActive(true);
            dragIcon.transform.SetParent(canvas.transform, false);
            var iconImage = dragIcon.AddComponent<Image>();
            iconImage.sprite = inventorySlot.itemIcon.sprite;
            iconImage.raycastTarget = false;
            iconImage.color = Color.red; // Test için kırmızı
            iconImage.enabled = true;
            var iconRect = dragIcon.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(50, 50); // Sabit bir boyut testi
            dragIcon.transform.SetAsLastSibling();

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            Vector2 localPointerPosition;
            Camera cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                cam = canvas.worldCamera;
                if (cam == null)
                {
                    cam = Camera.main;
                }
            }
            Debug.Log($"Kullanılan kamera: {cam?.name}");
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                cam,
                out localPointerPosition);
            iconRect.anchoredPosition = localPointerPosition;
            Debug.Log($"DragIcon pozisyonu: {localPointerPosition}, Boyutu: {iconRect.sizeDelta}");
        }
        else
        {
            Debug.LogWarning("DragIcon could not be created!");
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            return;
        }
        
        if (inventory == null || slotIndex >= inventory.inventorySlots.Count || 
            inventory.inventorySlots[slotIndex].item == null)
        {
            return;
        }

        if (dragIcon != null)
        {
            Vector2 localPointerPosition;
            Camera cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                cam = canvas.worldCamera;
                if (cam == null)
                {
                    cam = Camera.main;
                }
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                cam,
                out localPointerPosition);
            dragIcon.GetComponent<RectTransform>().anchoredPosition = localPointerPosition;
            
        }
        else
        {
            Debug.LogWarning("OnDrag: dragIcon is null!");
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            // DragIcon'u kesin destroy et
            if (dragIcon != null)
            {
                Destroy(dragIcon);
                dragIcon = null;
            }
            return;
        }
        
        if (inventory == null || slotIndex >= inventory.inventorySlots.Count || 
            inventory.inventorySlots[slotIndex].item == null)
        {
            ResetPosition();
            return;
        }

        // DragIcon'u kesin destroy et - her durumda
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
        canvasGroup.blocksRaycasts = true;

        // Aktif slot
        Slot currentSlotRef = inventory.inventorySlots[slotIndex];

        // Önce: Dünya (3D) FarmingArea hedefini ara
        FarmingAreaManager farmingArea = null;
        Camera cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out RaycastHit worldHit, Mathf.Infinity))
            {
                farmingArea = worldHit.collider.GetComponent<FarmingAreaManager>();
                if (farmingArea == null)
                {
                    farmingArea = worldHit.collider.GetComponentInParent<FarmingAreaManager>();
                }
            }
        }

        if (farmingArea != null && currentSlotRef.item != null && currentSlotRef.item.isSeed)
        {
            // FarmingAreaManager seed ekimini üstlenecek
            farmingArea.OnDrop(eventData);
            return;
        }

        // UI Drop hedefi kontrolü (genel): IDropHandler var mı?
        GameObject hitObject = eventData.pointerCurrentRaycast.gameObject;
        if (hitObject != null)
        {
            var genericDrop = hitObject.GetComponentInParent<IDropHandler>();
            // Eğer bir UI drop hedefi varsa (ör. FlaskSlotUI, DryingSlotUI), o hedef OnDrop ile işlemi yapacaktır.
            // Bu durumda dünyaya bırakma fallbacklerini ÇALIŞTIRMAYIN.
            if (genericDrop != null)
            {
                ResetPosition();
                return;
            }
        }

        // Ek güvence: Bazı durumlarda pointerCurrentRaycast boş olabilir; tüm GraphicRaycaster'lar üzerinden explicit raycast yap
        var raycasters = GameObject.FindObjectsOfType<GraphicRaycaster>();
        if (raycasters != null && raycasters.Length > 0)
        {
            PointerEventData ped = new PointerEventData(EventSystem.current) { position = eventData.position, pointerDrag = eventData.pointerDrag };
            var allResults = new System.Collections.Generic.List<RaycastResult>();
            foreach (var gr in raycasters)
            {
                if (gr == null || !gr.isActiveAndEnabled) continue;
                var results = new System.Collections.Generic.List<RaycastResult>();
                gr.Raycast(ped, results);
                if (results != null && results.Count > 0)
                {
                    allResults.AddRange(results);
                }
            }
            // En üstteki sonuçlara öncelik vermek için sortingOrder'a göre zaten GraphicRaycaster sıralar; biz ilk uygun hedefi alalım
            foreach (var rr in allResults)
            {
                var go = rr.gameObject;
                if (go == null) continue;
                var drop = go.GetComponentInParent<IDropHandler>();
                if (drop != null)
                {
                    try
                    {
                        drop.OnDrop(eventData);
                    }
                    catch { }
                    ResetPosition();
                    return;
                }
            }
        }

        // Özel UI DropZone kontrolü (varsa eski mantık)
        DropZone dropZone = null;
        if (hitObject != null)
        {
            dropZone = hitObject.GetComponent<DropZone>();
            if (dropZone == null)
            {
                dropZone = hitObject.GetComponentInParent<DropZone>();
            }
        }

        if (dropZone != null && dropZone.CanAcceptDrop())
        {
            Vector3 worldPosition = GetWorldDropPosition(eventData);
            WorldItemSpawner.SpawnItem(currentSlotRef.item, worldPosition, 1);
            RemoveSingleItemFromInventory();
        }
    else if (hitObject == null || (!IsUIElement(hitObject)))
        {
            Vector3 worldPosition = GetWorldDropPosition(eventData);
            WorldItemSpawner.SpawnItem(currentSlotRef.item, worldPosition, 1);
            RemoveSingleItemFromInventory();
        }
        else
        {
            ResetPosition();
        }
    }

    private bool IsUIElement(GameObject obj)
    {
        return obj.GetComponent<RectTransform>() != null || 
               obj.GetComponentInParent<Canvas>() != null;
    }

    private Vector3 GetWorldDropPosition(PointerEventData eventData)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(eventData.position);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                return hit.point + Vector3.up * 0.5f;
            }
            else
            {
                Vector3 worldPos = ray.GetPoint(10f);
                worldPos.y = 0.5f;
                return worldPos;
            }
        }

        return Vector3.zero + Vector3.up * 0.5f;
    }

    private void RemoveSingleItemFromInventory()
    {
        if (inventory != null && slotIndex < inventory.inventorySlots.Count)
        {
            Slot slot = inventory.inventorySlots[slotIndex];
            if (slot.itemCount > 1)
            {
                slot.itemCount--;
                if (slot.isFull)
                {
                    slot.isFull = false;
                }
            }
            else
            {
                slot.item = null;
                slot.itemCount = 0;
                slot.isFull = false;
            }
            inventory.TriggerInventoryChanged();
        }

        ResetPosition();
    }

    private void ResetPosition()
    {
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
        transform.SetParent(originalParent, false);
        rectTransform.anchoredPosition = originalPosition;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }
}