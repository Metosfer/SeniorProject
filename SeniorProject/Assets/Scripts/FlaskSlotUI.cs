using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class FlaskSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")] 
    public Image slotBackground;           // Parent Image (raycast target)
    public Image itemIcon;                 // Child Image
    public TextMeshProUGUI itemNameText;   // Child TMP text

    [Header("Ghost Image")]
    [Tooltip("Ghost image için opacity değeri (0-1 arası)")]
    public float ghostOpacity = 0.5f;

    [Header("Visuals")] 
    public Color emptyColor = Color.white;
    public Color occupiedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color hoverColor = new Color(1f, 1f, 0.8f, 1f);

    [Header("Cursor")]
    [Tooltip("İmleç slot üzerine geldiğinde değişecek texture")]
    public Texture2D hoverCursor;
    [Tooltip("İmleç hotspot (genelde texture'ın sol üst köşesi)")]
    public Vector2 cursorHotspot = Vector2.zero;
    [Tooltip("İmleç boyutu (piksel cinsinden). (0,0) ise texture'ın orijinal boyutu kullanılır")]
    public Vector2 cursorSize = Vector2.zero;

    [HideInInspector] public int slotIndex = -1; // Initialize with -1 to detect unassigned slots
    [HideInInspector] public FlaskManager flaskManager;

    private CanvasGroup _cg;
    private bool _isHovered = false;
    private bool _showingGhost = false;
    private Sprite _originalItemSprite;
    private Color _originalItemColor;

    private void Awake()
    {
        // Ensure this slot receives raycasts even if parent CanvasGroup blocks
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.interactable = true;
        _cg.blocksRaycasts = true;
#if UNITY_2020_1_OR_NEWER
        _cg.ignoreParentGroups = true;
#endif
        // Guarantee one graphic is raycastable
        if (slotBackground != null)
        {
            slotBackground.raycastTarget = true;
        }
        else
        {
            // Ensure the slot has an Image to receive raycasts even when empty
            var img = GetComponent<Image>();
            if (img == null)
            {
                img = gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0); // fully transparent
            }
            img.raycastTarget = true;
            slotBackground = img;
            if (itemIcon != null) itemIcon.raycastTarget = false;
        }
    }

    // --- Message-based integration ---
    public void SetSlotIndex(int index) 
    { 
        Debug.Log($"SetSlotIndex called on {gameObject.name}: old={slotIndex}, new={index}");
        slotIndex = index; 
        Debug.Log($"FlaskSlotUI {gameObject.name} assigned slotIndex: {index}");
        // Clear any existing visuals when reassigning
        if (itemIcon != null)
        {
            itemIcon.enabled = false;
            itemIcon.sprite = null;
        }
        if (itemNameText != null)
        {
            itemNameText.text = string.Empty;
        }
        _showingGhost = false;
    }
    public void InitFlaskSlot(FlaskManager mgr) 
    { 
        flaskManager = mgr; 
        Debug.Log($"FlaskSlotUI {gameObject.name} initialized with FlaskManager (slotIndex now: {slotIndex})");
        // Force refresh to ensure correct display
        SetEmptyVisuals();
    }
    public void UpdateFlaskSlotUI(FlaskManager.StoredSlot slot) { UpdateUI(slot); }

    public void UpdateUI(FlaskManager.StoredSlot slot)
    {
        Debug.Log($"UpdateUI called on {gameObject.name} (slotIndex={slotIndex}): item={slot?.item?.itemName}, count={slot?.count}");
        
        // Hide ghost image if showing when real item comes
        if (_showingGhost)
        {
            HideGhostImage();
        }
        
        if (slot == null)
        {
            SetEmptyVisuals();
            return;
        }

        bool hasItem = slot.item != null && slot.count > 0;

        if (itemIcon != null)
        {
            if (hasItem && slot.item.itemIcon != null)
            {
                Debug.Log($"Setting icon for {gameObject.name}: {slot.item.itemName}");
                itemIcon.enabled = true;
                itemIcon.sprite = slot.item.itemIcon;
                itemIcon.color = Color.white; // Ensure full opacity for real items
            }
            else
            {
                Debug.Log($"Clearing icon for {gameObject.name}");
                itemIcon.enabled = false;
                itemIcon.sprite = null;
            }
        }

        if (itemNameText != null)
        {
            itemNameText.text = hasItem ? $"{slot.item.itemName} x{slot.count}" : string.Empty;
        }

        if (slotBackground != null)
        {
            Color targetColor = hasItem ? occupiedColor : emptyColor;
            if (_isHovered) targetColor = hoverColor;
            slotBackground.color = targetColor;
        }
    }

    private void SetEmptyVisuals()
    {
        // Hide ghost image if showing
        if (_showingGhost)
        {
            HideGhostImage();
        }
        
        if (itemIcon != null)
        {
            itemIcon.enabled = false;
            itemIcon.sprite = null;
            itemIcon.color = Color.white; // Reset to default color
        }
        if (itemNameText != null)
        {
            itemNameText.text = string.Empty;
        }
        if (slotBackground != null)
        {
            Color targetColor = emptyColor;
            if (_isHovered) targetColor = hoverColor;
            slotBackground.color = targetColor;
        }
    }

    // Accept drops from Inventory UI
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"OnDrop called on FlaskSlot {slotIndex} (GameObject: {gameObject.name})");
        
        // Hide ghost image since we're doing the actual drop
        HideGhostImage();
        
        DragAndDropHandler dragHandler = eventData.pointerDrag?.GetComponent<DragAndDropHandler>();
        if (dragHandler == null || dragHandler.inventory == null) return;
        
        if (flaskManager == null)
        {
            flaskManager = FindObjectOfType<FlaskManager>();
            if (flaskManager == null) return;
        }

        int srcIndex = dragHandler.slotIndex;
        var inv = dragHandler.inventory;
        if (srcIndex < 0 || srcIndex >= inv.inventorySlots.Count) return;
        var srcSlot = inv.inventorySlots[srcIndex];
        if (srcSlot == null || srcSlot.item == null) return;

        Debug.Log($"Trying to add {srcSlot.item.itemName} to Flask slot {slotIndex} (GameObject: {gameObject.name})");
        
        // Double check our slot index is what we think it is
        Debug.Log($"Before drop - this FlaskSlotUI thinks it's slot {slotIndex}, gameObject name: {gameObject.name}");
        
        bool ok = flaskManager.TryAddItemToSlot(slotIndex, srcSlot.item);
        if (!ok) 
        {
            Debug.LogError($"Failed to add {srcSlot.item.itemName} to slot {slotIndex}");
            return;
        }

        // Remove one from inventory source
        if (srcSlot.itemCount > 1)
        {
            srcSlot.itemCount--;
            if (srcSlot.isFull) srcSlot.isFull = false;
        }
        else
        {
            srcSlot.item = null;
            srcSlot.itemCount = 0;
            srcSlot.isFull = false;
        }
        inv.TriggerInventoryChanged();
        flaskManager.RefreshUI();
    }

    // Click to take one back to inventory
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"OnPointerClick called on FlaskSlot {slotIndex}");
        
        if (flaskManager == null) return;
        
        // Check if this slot actually has an item
        if (slotIndex >= 0 && slotIndex < flaskManager.storedSlots.Count)
        {
            var slot = flaskManager.storedSlots[slotIndex];
            Debug.Log($"Slot {slotIndex} has item: {slot.item?.itemName}, count: {slot.count}");
            
            if (slot.item == null || slot.count <= 0)
            {
                Debug.Log($"Slot {slotIndex} is empty, cannot take item");
                return;
            }
        }
        
        var inventory = SCInventory.GetPersistentInventory();
        bool ok = flaskManager.TryTakeOneFromSlotToInventory(slotIndex, inventory);
        Debug.Log($"TryTakeOneFromSlotToInventory result: {ok}");
        if (ok)
        {
            flaskManager.RefreshUI();
        }
    }

    // Cursor management
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        Debug.Log($"OnPointerEnter called on slot {slotIndex}");
        
        // Check if we should show ghost image
        if (DragAndDropHandler.IsDragging && DragAndDropHandler.CurrentDragHandler != null)
        {
            var draggedItem = DragAndDropHandler.CurrentDragHandler.CurrentDraggedItem;
            if (draggedItem != null && draggedItem.item != null)
            {
                Debug.Log($"Dragging {draggedItem.item.itemName}, checking slot {slotIndex} for ghost image");
                ShowGhostImage(draggedItem.item);
            }
        }
        
    if (hoverCursor != null)
        {
            // Boyut ayarı varsa ölçeklenmiş cursor kullan
            if (cursorSize != Vector2.zero && (cursorSize.x != hoverCursor.width || cursorSize.y != hoverCursor.height))
            {
                var scaledCursor = ScaleCursor(hoverCursor, (int)cursorSize.x, (int)cursorSize.y);
                Cursor.SetCursor(scaledCursor, cursorHotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
            }
        }
        UpdateVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        HideGhostImage();
        var cm = CursorManager.Instance;
        if (cm != null)
        {
            cm.UseDefaultNow();
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (slotBackground == null) return;
        
        bool hasItem = false;
        if (flaskManager != null && slotIndex < flaskManager.storedSlots.Count)
        {
            var slot = flaskManager.storedSlots[slotIndex];
            hasItem = slot.item != null && slot.count > 0;
        }
        
        Color targetColor = hasItem ? occupiedColor : emptyColor;
        if (_isHovered) targetColor = hoverColor;
        slotBackground.color = targetColor;
    }

    private Texture2D ScaleCursor(Texture2D originalCursor, int newWidth, int newHeight)
    {
        if (originalCursor == null) return null;
        
        Texture2D readableTexture = MakeTextureReadable(originalCursor);
        if (readableTexture == null) return originalCursor;
        
        Texture2D scaledTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        
        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                float u = (float)x / newWidth;
                float v = (float)y / newHeight;
                Color pixel = readableTexture.GetPixelBilinear(u, v);
                scaledTexture.SetPixel(x, y, pixel);
            }
        }
        
        scaledTexture.Apply();
        
        if (readableTexture != originalCursor)
        {
            DestroyImmediate(readableTexture);
        }
        
        return scaledTexture;
    }

    private Texture2D MakeTextureReadable(Texture2D texture)
    {
        if (texture == null) return null;
        
        try
        {
            texture.GetPixel(0, 0);
            return texture;
        }
        catch
        {
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
            Graphics.Blit(texture, rt);
            
            RenderTexture.active = rt;
            Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readableTexture.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return readableTexture;
        }
    }
    
    private void ShowGhostImage(SCItem item)
    {
        if (itemIcon == null || item == null || item.itemIcon == null) return;
        
        // Slot already has an item, don't show ghost
        if (flaskManager != null && slotIndex < flaskManager.storedSlots.Count)
        {
            var slot = flaskManager.storedSlots[slotIndex];
            if (slot.item != null && slot.count > 0) 
            {
                Debug.Log($"Slot {slotIndex} already occupied, not showing ghost image");
                return;
            }
        }
        
        // Store original values if not already showing ghost
        if (!_showingGhost)
        {
            _originalItemSprite = itemIcon.sprite;
            _originalItemColor = itemIcon.color;
        }
        
        // Show ghost image
        Debug.Log($"Showing ghost image of {item.itemName} in slot {slotIndex}");
        itemIcon.enabled = true;
        itemIcon.sprite = item.itemIcon;
        itemIcon.color = new Color(1f, 1f, 1f, ghostOpacity);
        _showingGhost = true;
    }
    
    private void HideGhostImage()
    {
        if (!_showingGhost || itemIcon == null) return;
        
        Debug.Log($"Hiding ghost image in slot {slotIndex}");
        
        // Check if slot actually has an item
        bool hasRealItem = false;
        if (flaskManager != null && slotIndex < flaskManager.storedSlots.Count)
        {
            var slot = flaskManager.storedSlots[slotIndex];
            hasRealItem = slot.item != null && slot.count > 0;
        }
        
        if (hasRealItem)
        {
            // Restore real item visuals
            var slot = flaskManager.storedSlots[slotIndex];
            itemIcon.sprite = slot.item.itemIcon;
            itemIcon.color = Color.white;
            Debug.Log($"Restored real item {slot.item.itemName} in slot {slotIndex}");
        }
        else
        {
            // Restore original empty state
            itemIcon.enabled = false;
            itemIcon.sprite = _originalItemSprite;
            itemIcon.color = _originalItemColor;
            Debug.Log($"Restored empty state in slot {slotIndex}");
        }
        
        _showingGhost = false;
    }
}