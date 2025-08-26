using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class FlaskSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
	[Header("UI Components")] 
	public Image slotBackground;           // Parent Image (raycast target)
	public Image itemIcon;                 // Child Image
	public TextMeshProUGUI itemNameText;   // Child TMP text

	[Header("Visuals")] 
	public Color emptyColor = Color.white;
	public Color occupiedColor = new Color(0.9f, 0.9f, 0.9f, 1f);

	[HideInInspector] public int slotIndex;
	[HideInInspector] public FlaskManager flaskManager;

	private CanvasGroup _cg;

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
	public void SetSlotIndex(int index) { slotIndex = index; }
	public void InitFlaskSlot(FlaskManager mgr) { flaskManager = mgr; }
	public void UpdateFlaskSlotUI(FlaskManager.StoredSlot slot) { UpdateUI(slot); }

	public void UpdateUI(FlaskManager.StoredSlot slot)
	{
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
				itemIcon.enabled = true;
				itemIcon.sprite = slot.item.itemIcon;
			}
			else
			{
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
			slotBackground.color = hasItem ? occupiedColor : emptyColor;
		}
	}

	private void SetEmptyVisuals()
	{
		if (itemIcon != null)
		{
			itemIcon.enabled = false;
			itemIcon.sprite = null;
		}
		if (itemNameText != null)
		{
			itemNameText.text = string.Empty;
		}
		if (slotBackground != null)
		{
			slotBackground.color = emptyColor;
		}
	}

    // Accept drops from Inventory UI
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"FlaskSlotUI OnDrop called! slot={slotIndex}");
        Debug.Log($"eventData.pointerDrag: {eventData.pointerDrag?.name}");
        
        DragAndDropHandler dragHandler = eventData.pointerDrag?.GetComponent<DragAndDropHandler>();
        Debug.Log($"dragHandler found: {dragHandler != null}");
        
        if (dragHandler == null)
        {
            Debug.LogWarning($"OnDrop failed: dragHandler is null");
            return;
        }
        
        if (dragHandler.inventory == null)
        {
            Debug.LogWarning($"OnDrop failed: dragHandler.inventory is null");
            return;
        }
        else
        {
            Debug.Log($"dragHandler.inventory found: {dragHandler.inventory.name}, slots count: {dragHandler.inventory.inventorySlots.Count}");
        }
        
        if (flaskManager == null)
        {
            Debug.LogWarning($"OnDrop failed: flaskManager is null - trying to find it");
            // FlaskManager'ı sahnede bul
            flaskManager = FindObjectOfType<FlaskManager>();
            if (flaskManager == null)
            {
                Debug.LogError("No FlaskManager found in scene!");
                return;
            }
            else
            {
                Debug.Log($"Found FlaskManager: {flaskManager.name}");
            }
        }
        else
        {
            Debug.Log($"flaskManager found: {flaskManager.name}");
        }

		int srcIndex = dragHandler.slotIndex;
		var inv = dragHandler.inventory;
		if (srcIndex < 0 || srcIndex >= inv.inventorySlots.Count)
		{
			Debug.LogWarning($"OnDrop failed: invalid srcIndex={srcIndex}, inventory slots={inv.inventorySlots.Count}");
			return;
		}
		var srcSlot = inv.inventorySlots[srcIndex];
		if (srcSlot == null || srcSlot.item == null)
		{
			Debug.LogWarning($"OnDrop failed: srcSlot is null or item is null");
			return;
		}

		Debug.Log($"Attempting to add {srcSlot.item.itemName} to Flask slot {slotIndex}");
		bool ok = flaskManager.TryAddItemToSlot(slotIndex, srcSlot.item);
		if (!ok)
		{
			Debug.LogWarning($"TryAddItemToSlot failed for {srcSlot.item.itemName} to slot {slotIndex}");
			return; // Different item in slot without stacking, or other rule
		}

		// Remove one from inventory source
		Debug.Log($"Successfully added item to Flask slot {slotIndex}, removing from inventory");
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
		if (flaskManager == null) return;
		var inventory = SCInventory.GetPersistentInventory();
		bool ok = flaskManager.TryTakeOneFromSlotToInventory(slotIndex, inventory);
		if (ok)
		{
			// Inventory AddItem already triggers UI event; refresh slot visuals too
			flaskManager.RefreshUI();
		}
	}
}

