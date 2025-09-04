using UnityEngine;
using UnityEngine.UI;
using EazyCamera;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class DragAndDropHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // Static tracking for current drag
    public static DragAndDropHandler CurrentDragHandler { get; private set; }
    public static bool IsDragging => CurrentDragHandler != null;
    
    // ESC consumption flag for this frame to prevent PauseMenu from reacting
    private static int s_lastEscapeConsumedFrame = -1;
    public static bool DidConsumeEscapeThisFrame() => s_lastEscapeConsumedFrame == Time.frameCount;

    // Public helper to ensure any ESC press while dragging cancels the drag first
    public static bool TryCancelCurrentDragAndConsumeEsc()
    {
        if (CurrentDragHandler != null)
        {
            s_lastEscapeConsumedFrame = Time.frameCount;
            // Call instance cancel; OnEndDrag will be safely skipped by _dragCancelled
            try { CurrentDragHandler.CancelDragOperation(); } catch {}
            return true;
        }
        return false;
    }
    
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 originalPosition;
    private Transform originalParent;
    private InventorySlotUI inventorySlot;
    private GameObject dragIcon;
    private GameObject dragGhost;
    // Track hovered drying slot for ghost previews
    private static DryingSlotUI s_currentHoveredDryingSlot;
    
    // Camera orbit control during drag
    private EazyCam _cam;
    private bool _prevOrbitEnabled;
    private bool _camStateCaptured;
    private bool _dragCancelled = false;
    
    public int slotIndex { get; set; }
    public SCInventory inventory { get; set; }
    public InventoryUIManager uiManager { get; set; }
    
    public Slot CurrentDraggedItem 
    {
        get 
        {
            if (inventory != null && slotIndex < inventory.inventorySlots.Count)
                return inventory.inventorySlots[slotIndex];
            return null;
        }
    }

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
    
    private void Update()
    {
        // If this handler is currently dragging, cancel on ESC even without pointer movement
        if (CurrentDragHandler == this && Input.GetKeyDown(KeyCode.Escape))
        {
            s_lastEscapeConsumedFrame = Time.frameCount;
            CancelDragOperation();
        }
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

        // Reset cancellation flag for new drag
        _dragCancelled = false;

        // Set static drag tracking
        CurrentDragHandler = this;

        // Disable camera orbit while dragging (preserve previous state)
        if (_cam == null)
        {
            _cam = GameObject.FindObjectOfType<EazyCam>();
        }
        if (_cam != null && !_camStateCaptured)
        {
            _prevOrbitEnabled = _cam.CameraSettings.OrbitEnabled;
            _camStateCaptured = true;
            _cam.SetOrbitEnabled(EnabledState.Disabled);
        }

        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;

        canvasGroup.blocksRaycasts = false;

        // Check current scene to determine ghost type
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isShopScene = currentScene == "ShopScene";
        bool isFarmScene = currentScene == "FarmScene";

        // Prefer 3D ghost of the actual world item prefab if available (FarmScene)
        // Use 2D icon for ShopScene
        var slot = inventory.inventorySlots[slotIndex];
        var item = slot != null ? slot.item : null;
        
        if (isShopScene)
        {
            // ShopScene: Always use 2D icon
            CreateOrMoveDragIcon(eventData);
        }
        else if (isFarmScene && item != null)
        {
            // FarmScene: Try 3D ghost first
            GameObject prefabForGhost = item.dropPrefab != null ? item.dropPrefab : item.itemPrefab;
            if (prefabForGhost != null)
            {
                try
                {
                    dragGhost = Instantiate(prefabForGhost);
                    dragGhost.name = $"DragGhost_{item.itemName}";
                    PrepareGhostAppearance(dragGhost, 0.5f);
                    SetLayerRecursively(dragGhost, 2); // Ignore Raycast
                    PositionGhostAtPointer(eventData);
                }
                catch
                {
                    // Fallback to icon if instantiation fails
                    CreateOrMoveDragIcon(eventData);
                }
            }
            else
            {
                // No prefab defined: fallback to 2D icon drag
                CreateOrMoveDragIcon(eventData);
            }
        }
        else
        {
            // Other scenes: Use 2D icon as default
            CreateOrMoveDragIcon(eventData);
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

        // Check for ESC key to cancel drag operation
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Mark ESC as consumed to prevent PauseMenu from opening
            s_lastEscapeConsumedFrame = Time.frameCount;
            CancelDragOperation();
            return;
        }

        if (dragGhost != null)
        {
            PositionGhostAtPointer(eventData);
        }
        else if (dragIcon != null)
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
            // nothing to move (no ghost or icon)
        }

    // Update DryingSlot hover ghost preview (UI side)
    UpdateDryingSlotHoverGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // If drag was cancelled by ESC, skip normal end drag logic
        if (_dragCancelled)
        {
            _dragCancelled = false; // Reset for next drag
            return;
        }

        // Clear static drag tracking
        CurrentDragHandler = null;
        // Hide any drying slot hover ghost
        if (s_currentHoveredDryingSlot != null)
        {
            try { s_currentHoveredDryingSlot.HideHoverGhost(); } catch {}
            s_currentHoveredDryingSlot = null;
        }

        // Restore camera orbit if we disabled it
        if (_cam != null && _camStateCaptured)
        {
            _cam.SetOrbitEnabled(_prevOrbitEnabled ? EnabledState.Enabled : EnabledState.Disabled);
            _camStateCaptured = false;
        }
        
        // Ensure drag previews are always destroyed
        try
        {
            if (dragGhost != null) { Destroy(dragGhost); dragGhost = null; }
            if (dragIcon != null) { Destroy(dragIcon); dragIcon = null; }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error destroying drag previews: {e.Message}");
        }
        
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            // Canvas is not interactable, just return
            return;
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
                // Ensure dragGhost is destroyed when dropping to UI
                if (dragGhost != null) { Destroy(dragGhost); dragGhost = null; }
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
                    // Ensure dragGhost is destroyed when dropping to UI
                    if (dragGhost != null) { Destroy(dragGhost); dragGhost = null; }
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
            // Notify save system
            if (GameSaveManager.Instance != null)
            {
                try { GameSaveManager.Instance.OnWorldItemDropped(currentSlotRef.item, worldPosition, 1); } catch {}
            }
            RemoveSingleItemFromInventory();
        }
        else if (hitObject == null || (!IsUIElement(hitObject)))
        {
            Vector3 worldPosition = GetWorldDropPosition(eventData);
            WorldItemSpawner.SpawnItem(currentSlotRef.item, worldPosition, 1);
            if (GameSaveManager.Instance != null)
            {
                try { GameSaveManager.Instance.OnWorldItemDropped(currentSlotRef.item, worldPosition, 1); } catch {}
            }
            RemoveSingleItemFromInventory();
        }
        else
        {
            ResetPosition();
        }
    }

    private void UpdateDryingSlotHoverGhost(PointerEventData eventData)
    {
        // Determine DryingSlotUI under pointer, if any
        DryingSlotUI target = null;
        var hitObject = eventData.pointerCurrentRaycast.gameObject;
        if (hitObject != null)
        {
            target = hitObject.GetComponentInParent<DryingSlotUI>();
        }
        if (target == null)
        {
            // Fallback raycast across all UI canvases
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
                    foreach (var rr in results)
                    {
                        if (rr.gameObject == null) continue;
                        var ui = rr.gameObject.GetComponentInParent<DryingSlotUI>();
                        if (ui != null) { target = ui; break; }
                    }
                    if (target != null) break;
                }
            }
        }

        if (target == s_currentHoveredDryingSlot) return; // no change

        // Hide previous
        if (s_currentHoveredDryingSlot != null)
        {
            try { s_currentHoveredDryingSlot.HideHoverGhost(); } catch {}
            s_currentHoveredDryingSlot = null;
        }

        // Show new if any
        if (target != null)
        {
            var slot = CurrentDraggedItem;
            var sprite = (slot != null && slot.item != null) ? slot.item.itemIcon : null;
            try { target.ShowHoverGhost(sprite); } catch {}
            s_currentHoveredDryingSlot = target;
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

    private void CancelDragOperation()
    {
        // Mark as cancelled to prevent OnEndDrag from running
        _dragCancelled = true;

        // Clear static drag tracking
        CurrentDragHandler = null;
        if (s_currentHoveredDryingSlot != null)
        {
            try { s_currentHoveredDryingSlot.HideHoverGhost(); } catch {}
            s_currentHoveredDryingSlot = null;
        }

        // Restore camera orbit if we disabled it
        if (_cam != null && _camStateCaptured)
        {
            _cam.SetOrbitEnabled(_prevOrbitEnabled ? EnabledState.Enabled : EnabledState.Disabled);
            _camStateCaptured = false;
        }

        // Destroy drag previews
        if (dragGhost != null) { Destroy(dragGhost); dragGhost = null; }
        if (dragIcon != null) { Destroy(dragIcon); dragIcon = null; }

        // Reset position and raycast blocking
        transform.SetParent(originalParent, false);
        rectTransform.anchoredPosition = originalPosition;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        Debug.Log("Drag operation cancelled by ESC key");
    }

    private void ResetPosition()
    {
        if (dragGhost != null) { Destroy(dragGhost); dragGhost = null; }
        if (dragIcon != null) { Destroy(dragIcon); dragIcon = null; }
        transform.SetParent(originalParent, false);
        rectTransform.anchoredPosition = originalPosition;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void CreateOrMoveDragIcon(PointerEventData eventData)
    {
        if (dragIcon == null && inventorySlot != null && inventorySlot.itemIcon != null && inventorySlot.itemIcon.sprite != null && canvas != null)
        {
            dragIcon = new GameObject("DragIcon");
            dragIcon.SetActive(true);
            dragIcon.transform.SetParent(canvas.transform, false);
            var iconImage = dragIcon.AddComponent<Image>();
            iconImage.sprite = inventorySlot.itemIcon.sprite;
            iconImage.raycastTarget = false;
            iconImage.color = new Color(1f, 1f, 1f, 0.8f);
            iconImage.enabled = true;
            var iconRect = dragIcon.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(50, 50);
            dragIcon.transform.SetAsLastSibling();

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
        }
        if (dragIcon != null)
        {
            Vector2 localPointerPosition;
            Camera cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                cam,
                out localPointerPosition);
            dragIcon.GetComponent<RectTransform>().anchoredPosition = localPointerPosition;
        }
    }

    private void PositionGhostAtPointer(PointerEventData eventData)
    {
        if (dragGhost == null) return;
        Camera mainCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (mainCamera == null) return;
        Ray ray = mainCamera.ScreenPointToRay(eventData.position);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            dragGhost.transform.position = hit.point + Vector3.up * 0.02f; // slight lift
            // Optional: align with surface normal yaw only
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, hit.normal).normalized;
            if (forward.sqrMagnitude > 0.01f)
                dragGhost.transform.rotation = Quaternion.LookRotation(forward, hit.normal);
        }
        else
        {
            Vector3 pos = ray.GetPoint(6f);
            pos.y = 0.02f;
            dragGhost.transform.position = pos;
        }
    }

    private void PrepareGhostAppearance(GameObject go, float opacity)
    {
        // Disable physics/collisions
        var rbs = go.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs) { rb.isKinematic = true; rb.useGravity = false; }
        var cols = go.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols) { c.enabled = false; }

        // Make materials semi-transparent
        var rends = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var rend = rends[i];
            if (rend == null) continue;
            // ensure we get unique instances to avoid modifying shared assets
            var mats = rend.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;
                TrySetMaterialTransparent(mat);
                if (mat.HasProperty("_Color"))
                {
                    var c = mat.color; c.a = Mathf.Clamp01(opacity); mat.color = c;
                }
                if (mat.HasProperty("_BaseColor"))
                {
                    var c2 = mat.GetColor("_BaseColor"); c2.a = Mathf.Clamp01(opacity); mat.SetColor("_BaseColor", c2);
                }
                mat.renderQueue = 3000;
            }
            rend.materials = mats;
        }
    }

    private void OnDestroy()
    {
        // Ensure drag previews are destroyed when this component is destroyed
        if (dragGhost != null)
        {
            Destroy(dragGhost);
            dragGhost = null;
        }
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
        
        // Clear static reference if this was the current drag handler
        if (CurrentDragHandler == this)
        {
            CurrentDragHandler = null;
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void TrySetMaterialTransparent(Material mat)
    {
        if (mat == null) return;
        var shaderName = mat.shader != null ? mat.shader.name : string.Empty;
        // Legacy Standard
        if (shaderName == "Standard")
        {
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return;
        }
        // URP/Lit
        if (shaderName.Contains("Universal Render Pipeline/Lit") || shaderName.Contains("URP/Lit"))
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            return;
        }
        // Fallback: rely on alpha channel if respected
    }
}