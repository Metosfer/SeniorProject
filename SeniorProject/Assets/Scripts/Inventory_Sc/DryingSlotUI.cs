using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class DryingSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    public Image itemIcon;
    public TextMeshProUGUI timerText;
    public Button collectButton;
    public Image slotBackground;
    [Tooltip("Sürükleme sırasında slot üzerinde gösterilecek ghost ikon")]
    public Image hoverGhostIcon;
    [Tooltip("Ghost ikon opaklığı")] public float ghostIconAlpha = 0.6f;
    
    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color occupiedColor = Color.gray;
    public Color readyColor = Color.green;

    [Header("Hover Feedback Settings")] 
    [Tooltip("Slot üzerine gelindiğinde kullanılacak özel cursor (boş bırakılırsa sistem cursor'u değiştirilmez)")] [SerializeField] private Texture2D hoverCursor; 
    [Tooltip("Cursor hotspot ofseti (texture merkezinde istiyorsanız width/2,height/2 kullanın)")] [SerializeField] private Vector2 cursorHotspot = Vector2.zero;
    [Tooltip("Hover sırasında arka plan rengi")] [SerializeField] private Color hoverBackgroundColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("Slot boşsa hover rengini farklı göster")] [SerializeField] private bool differentiateEmptyHover = true;
    [Tooltip("Boş slot hover rengi")] [SerializeField] private Color emptyHoverColor = new Color(0.9f, 0.9f, 1f, 0.85f);
    [Tooltip("Slot dolu hover rengi")] [SerializeField] private Color occupiedHoverColor = new Color(1f, 0.95f, 0.75f, 0.9f);
    [Tooltip("Slot ready durumunda hover rengi")] [SerializeField] private Color readyHoverColor = new Color(0.75f, 1f, 0.75f, 0.95f);
    [Tooltip("Hover çıktığında cursor'u eski haline döndür")] [SerializeField] private bool restoreCursorOnExit = true;
    [Tooltip("Hover state debug logları")] [SerializeField] private bool debugHover = false;

    private Color _originalBGColor;
    private bool _storedOriginalColor = false;
    private bool _isPointerInside = false;
    private static Texture2D s_defaultCursorTexture; // (opsiyonel future use)

    [HideInInspector]
    public int slotIndex;
    [HideInInspector]
    public DryingAreaUI dryingAreaUI;

    private void Awake()
    {
        if (slotBackground != null && !_storedOriginalColor)
        {
            _originalBGColor = slotBackground.color;
            _storedOriginalColor = true;
        }
    }

    public void UpdateSlotUI(DryingSlot slot)
    {
        // Icon güncelleme
        if (itemIcon != null)
        {
            if (slot.currentItemData != null)
            {
                itemIcon.sprite = slot.currentItemData.itemIcon;
                itemIcon.enabled = true;
            }
            else
            {
                itemIcon.enabled = false;
            }
        }
        
        // Timer text güncelleme
        if (timerText != null)
        {
            if (slot.isOccupied && !slot.isReadyToCollect)
            {
                timerText.text = Mathf.Ceil(slot.timer).ToString() + "s";
            }
            else if (slot.isReadyToCollect)
            {
                timerText.text = "Ready!";
            }
            else
            {
                timerText.text = "Empty";
            }
        }
        
        // Collect button güncelleme
        if (collectButton != null)
        {
            collectButton.gameObject.SetActive(slot.isReadyToCollect);
        }
        
        // Background color güncelleme
        if (slotBackground != null)
        {
            if (slot.isReadyToCollect)
            {
                slotBackground.color = readyColor;
            }
            else if (slot.isOccupied)
            {
                slotBackground.color = occupiedColor;
            }
            else
            {
                slotBackground.color = normalColor;
            }
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Inventory'den sürüklenen item'ı al
        DragAndDropHandler dragHandler = eventData.pointerDrag?.GetComponent<DragAndDropHandler>();
        
        if (dragHandler != null && dragHandler.inventory != null)
        {
            int sourceSlotIndex = dragHandler.slotIndex;
            SCItem draggedItem = dragHandler.inventory.inventorySlots[sourceSlotIndex].item;
            
            // Item drying için uygun mu kontrol et
            if (draggedItem != null && draggedItem.canBeDried)
            {
                // Slot'a eklemeyi dene
                bool success = dryingAreaUI.TryAddItemToSlot(slotIndex, draggedItem);
                
                if (success)
                {
                    // Inventory'den item'ı kaldır (sadece 1 tane)
                    if (dragHandler.inventory.inventorySlots[sourceSlotIndex].itemCount > 1)
                    {
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].itemCount--;
                    }
                    else
                    {
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].item = null;
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].itemCount = 0;
                        dragHandler.inventory.inventorySlots[sourceSlotIndex].isFull = false;
                    }
                    
                    // Inventory değişikliğini tetikle
                    dragHandler.inventory.TriggerInventoryChanged();
                }
            }
            else
            {
                Debug.Log("Bu item kurutma için uygun değil!");
            }
        }
    }

    // Hover ghost preview for dragged item
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverGhostIcon == null) { /* ignore ghost if null */ } else {
            var dd = DragAndDropHandler.CurrentDragHandler;
            if (dd == null || dd.inventory == null) { hoverGhostIcon.gameObject.SetActive(false); }
            else {
                var slot = dd.inventory.inventorySlots[dd.slotIndex];
                var draggedItem = slot != null ? slot.item : null;
                if (draggedItem != null)
                {
                    var spr = draggedItem.itemIcon;
                    if (spr != null)
                    {
                        hoverGhostIcon.sprite = spr;
                        var c = hoverGhostIcon.color; c.a = Mathf.Clamp01(ghostIconAlpha); hoverGhostIcon.color = c;
                        hoverGhostIcon.gameObject.SetActive(true);
                    }
                    else hoverGhostIcon.gameObject.SetActive(false);
                }
                else hoverGhostIcon.gameObject.SetActive(false);
            }
        }
        _isPointerInside = true;
        ApplyHoverVisual();
        if (hoverCursor != null)
        {
            Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
            if (debugHover) Debug.Log($"[DryingSlotUI] Cursor changed on hover (slotIndex={slotIndex})");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverGhostIcon != null) hoverGhostIcon.gameObject.SetActive(false);
        _isPointerInside = false;
        RestoreNormalVisual();
        if (restoreCursorOnExit)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (debugHover) Debug.Log($"[DryingSlotUI] Cursor restored on exit (slotIndex={slotIndex})");
        }
    }

    private void ApplyHoverVisual()
    {
        if (slotBackground == null) return;
        if (!_storedOriginalColor)
        {
            _originalBGColor = slotBackground.color; _storedOriginalColor = true;
        }
        // Determine dynamic color based on slot state via DryingArea
        Color target = hoverBackgroundColor;
        if (differentiateEmptyHover && dryingAreaUI != null && slotIndex >= 0 && slotIndex < dryingAreaUI.dryingManager.dryingSlots.Length)
        {
            var slot = dryingAreaUI.dryingManager.dryingSlots[slotIndex];
            if (slot != null)
            {
                if (slot.isReadyToCollect) target = readyHoverColor;
                else if (slot.isOccupied) target = occupiedHoverColor;
                else target = emptyHoverColor;
            }
        }
        slotBackground.color = target;
    }

    private void RestoreNormalVisual()
    {
        if (slotBackground == null) return;
        if (_storedOriginalColor)
        {
            slotBackground.color = _originalBGColor;
        }
    }

    private void OnDisable()
    {
        if (_isPointerInside)
        {
            if (restoreCursorOnExit)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            RestoreNormalVisual();
            _isPointerInside = false;
        }
    }

    // Reintroduced API for DragAndDropHandler compatibility
    public void ShowHoverGhost(Sprite sprite)
    {
        if (hoverGhostIcon == null) return;
        if (sprite == null)
        {
            hoverGhostIcon.gameObject.SetActive(false);
            return;
        }
        hoverGhostIcon.sprite = sprite;
        var c = hoverGhostIcon.color; c.a = Mathf.Clamp01(ghostIconAlpha); hoverGhostIcon.color = c;
        hoverGhostIcon.gameObject.SetActive(true);
    }

    public void HideHoverGhost()
    {
        if (hoverGhostIcon != null)
        {
            hoverGhostIcon.gameObject.SetActive(false);
        }
    }
}
