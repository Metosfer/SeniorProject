using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class FlaskManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // Panel referansı
    [SerializeField] private GameObject panel;
    
    // Panelin aktif olup olmadığını kontrol eden değişken
    private bool isPanelActive = false;

    [Header("Hover Feedback")]
    [Tooltip("Mouse ile üstüne gelince uygulanacak opaklık (0-1)")]
    [Range(0f,1f)] public float hoverOpacity = 0.9f;
    [Tooltip("Alt objelerdeki renderer'lara da uygula")] public bool includeChildren = true;
    [Tooltip("Renk özelliği için denenecek property adları")] public string[] colorPropertyNames = new[] { "_Color", "_BaseColor" };

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Dictionary<Renderer, float> _originalAlpha = new Dictionary<Renderer, float>();
    private bool _hoverApplied;
    
    [Header("Hover Scale (UI/Image)")]
    [Tooltip("Hover'da büyütme efektini etkinleştir")] public bool enableHoverScale = true;
    [Tooltip("Hedef ölçek çarpanı (1 = orijinal)")] public float hoverScale = 1.08f;
    [Tooltip("Hover'a geçiş süresi (sn)")] public float scaleInDuration = 0.12f;
    [Tooltip("Hover'dan çıkış süresi (sn)")] public float scaleOutDuration = 0.12f;
    private Vector3 _initialScale;
    private Coroutine _scaleCo;
    private bool _isPointerOver;

    [Header("Hover Cursor")]
    [Tooltip("Objenin üstüne gelince değişecek cursor görseli")] public Texture2D hoverCursor;
    [Tooltip("Cursor hotspot (piksel)")] public Vector2 cursorHotspot = Vector2.zero;
    [Tooltip("Cursor boyutu (px). (0,0) ise orijinal boyut kullanılır")] public Vector2 cursorSize = Vector2.zero;
    
    [Header("Storage Settings")]
    [Tooltip("Flask deposundaki slot sayısı")] public int slotCount = 3;
    [Tooltip("Slot UI referansları (FlaskPanel altındaki slot objeleri)")]
    public Transform[] slotUIs = new Transform[3];
    [Tooltip("Her slot'ta item stack limiti")]
    public int stackLimit = 4;

    // Depo verisi
    [System.Serializable]
    public class StoredSlot
    {
        public SCItem item;
        public int count;
    }
    public List<StoredSlot> storedSlots = new List<StoredSlot>();
    
    void Start()
    {
        // Başlangıçta paneli kapalı duruma getir
        if (panel != null)
        {
            panel.SetActive(false);
        }

    // Hover görsel geri bildirim için renderer'ları hazırla
    _renderers = includeChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
    _mpb = new MaterialPropertyBlock();
    CacheOriginalAlphas();
        _initialScale = transform.localScale;

        // Force clear all stored data first
        Debug.Log("=== FLASK MANAGER START - CLEARING DATA ===");
        InitSlots();
        
        // Debug: Show SlotUI assignments BEFORE binding
        Debug.Log("=== SLOTUI ARRAY ASSIGNMENTS ===");
        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] != null)
            {
                Debug.Log($"slotUIs[{i}] = GameObject '{slotUIs[i].name}'");
            }
            else
            {
                Debug.Log($"slotUIs[{i}] = NULL");
            }
        }
        Debug.Log("=================================");
        
        BindSlotUIs();
        
        // Debug: Verify slot assignments AFTER binding
        Debug.Log("=== VERIFYING SLOT ASSIGNMENTS ===");
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                var flaskSlotUI = t.GetComponent<FlaskSlotUI>();
                if (flaskSlotUI != null)
                {
                    Debug.Log($"GameObject '{t.name}' has slotIndex: {flaskSlotUI.slotIndex}");
                }
                else
                {
                    Debug.LogError($"GameObject '{t.name}' missing FlaskSlotUI component!");
                }
            }
        }
        Debug.Log("===================================");
    }

    void Update()
    {
        // ESC tuşuna basıldığında ve panel açıksa, paneli kapat
        if (Input.GetKeyDown(KeyCode.Escape) && isPanelActive)
        {
            ClosePanel();
        }
    }
    
    private void OnMouseEnter()
    {
        ApplyAlphaToRenderers(hoverOpacity);
        _hoverApplied = true;
    StartHoverScale();
    StartHoverCursor();
    _isPointerOver = true;
    }

    private void OnMouseExit()
    {
        RestoreAlphaOnRenderers();
        _hoverApplied = false;
    EndHoverScale();
    EndHoverCursor();
    _isPointerOver = false;
    }
    
    // Mouse tıklaması algılandığında çağrılır
    private void OnMouseDown()
    {
    StartClickScale();
        // Block world clicks while Pause menu is open
        if (PauseMenuController.IsPausedGlobally)
        {
            return;
        }
        // If Market or another modal UI is open, ignore world clicks
        if (MarketManager.IsAnyOpen)
        {
            return;
        }
        OpenPanel();
    }

    private void OnMouseUp()
    {
        ReleaseClickScale();
    }
    
    // Paneli açan metod
    private void OpenPanel()
    {
        if (panel != null)
        {
            panel.SetActive(true);
            isPanelActive = true;
            // Panel arka planı varsa raycast'ı kapat ki slotlar drop alabilsin
            var panelImg = panel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.raycastTarget = false;
            }
            // Çocuklardaki yaygın arka plan img'lerini de kapat (Slotların dışında kalanlar)
            foreach (var img in panel.GetComponentsInChildren<Image>(includeInactive: true))
            {
                if (img.gameObject == panel) continue;
                // Slot arka planlarını etkileme: slotların bir FlaskSlotUI bileşeni vardır
                if (img.GetComponentInParent<FlaskSlotUI>() != null)
                {
                    // Slot içindeki itemIcon gibi elemanlarda raycast target kapalı kalabilir
                    continue;
                }
                // Tam ekran/katman arka planı gibi Image’ların raycast'ını kapat
                img.raycastTarget = false;
            }
            RefreshUI();
        }
        else
        {
            Debug.LogError("Panel atanmamış! Inspector'da panel referansını ayarlayın.");
        }
    }
    
    // Paneli kapatan metod
    private void ClosePanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            isPanelActive = false;
        }
    }

    // Panel durumunu kontrol etmek için public metod
    public bool IsPanelActive()
    {
        return isPanelActive;
    }

    // ----- Storage Logic -----
    private void InitSlots()
    {
        Debug.Log($"InitSlots called: creating {slotCount} empty slots");
        storedSlots.Clear();
        for (int i = 0; i < Mathf.Max(1, slotCount); i++)
        {
            storedSlots.Add(new StoredSlot());
            Debug.Log($"Created empty slot {i}");
        }
    }

    private void BindSlotUIs()
    {
        Debug.Log("BindSlotUIs called");
        if (slotUIs == null) return;
        
        Debug.Log("=== SLOT UI BINDING ===");
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                Debug.Log($"Binding slot index {i} to GameObject '{t.name}' at array position {i}");
                
                // Use direct component access instead of SendMessage
                var flaskSlotUI = t.GetComponent<FlaskSlotUI>();
                if (flaskSlotUI != null)
                {
                    flaskSlotUI.slotIndex = i;
                    flaskSlotUI.flaskManager = this;
                    Debug.Log($"DIRECT: Set {t.name} slotIndex to {i}");
                }
                else
                {
                    Debug.LogError($"GameObject '{t.name}' is missing FlaskSlotUI component!");
                }
                
                // Also try SendMessage as backup
                t.SendMessage("SetSlotIndex", i, SendMessageOptions.DontRequireReceiver);
                t.SendMessage("InitFlaskSlot", this, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning($"SlotUI at array position {i} is null!");
            }
        }
        Debug.Log("=====================");
    }

    public bool TryAddItemToSlot(int slotIndex, SCItem item)
    {
        Debug.Log($"TryAddItemToSlot called: slotIndex={slotIndex}, item={item?.itemName}");
        
        // Debug: Show all slots status
        Debug.Log("=== ALL SLOTS STATUS ===");
        for (int i = 0; i < storedSlots.Count; i++)
        {
            var s = storedSlots[i];
            Debug.Log($"Slot {i}: item={s.item?.itemName}, count={s.count}");
        }
        Debug.Log("========================");
        
        if (item == null) return false;
        if (slotIndex < 0 || slotIndex >= storedSlots.Count) 
        {
            Debug.LogError($"Invalid slotIndex: {slotIndex}, storedSlots.Count: {storedSlots.Count}");
            return false;
        }

        // NEW FEATURE: Check if same item exists in any other slot and can stack
        if (item.canStackable)
        {
            for (int i = 0; i < storedSlots.Count; i++)
            {
                var existingSlot = storedSlots[i];
                if (existingSlot.item == item && existingSlot.count > 0)
                {
                    // Found same item in slot i, check if it can stack more
                    if (existingSlot.count < stackLimit)
                    {
                        Debug.Log($"Found same item {item.itemName} in slot {i}, stacking there instead of slot {slotIndex}");
                        existingSlot.count++;
                        RefreshUI();
                        return true;
                    }
                    else
                    {
                        Debug.Log($"Same item {item.itemName} in slot {i} is at max stack ({stackLimit})");
                    }
                }
            }
        }

        var slot = storedSlots[slotIndex];
        Debug.Log($"Target slot {slotIndex}: item={slot.item?.itemName}, count={slot.count}");
        
        if (slot.item == null)
        {
            // Boş slot: item'i yerleştir
            Debug.Log($"Placing item {item.itemName} in empty slot {slotIndex}");
            slot.item = item;
            slot.count = 1;
            RefreshUI();
            return true;
        }
        else if (slot.item == item && item.canStackable)
        {
            // Aynı item ve stacklenebilir: sayıyı artır
            if (slot.count < stackLimit)
            {
                Debug.Log($"Stacking item {item.itemName} in slot {slotIndex}, new count: {slot.count + 1}");
                slot.count++;
                RefreshUI();
                return true;
            }
            else
            {
                Debug.Log($"Slot {slotIndex} is at max stack limit ({stackLimit}) for {item.itemName}");
                return false;
            }
        }
        else
        {
            // Slot dolu ve farklı item: drop işlemini reddet
            Debug.LogWarning($"REJECTED: Slot {slotIndex} is occupied with {slot.item.itemName}, cannot place {item.itemName}");
            return false;
        }
    }

    // Public method for auto-transfer from inventory
    public bool TryAddToAnySlot(SCItem item)
    {
        Debug.Log($"TryAddToAnySlot called for item: {item?.itemName}");
        
        if (item == null) return false;

        // First, try to stack with existing same items (smart stacking)
        if (item.canStackable)
        {
            for (int i = 0; i < storedSlots.Count; i++)
            {
                var slot = storedSlots[i];
                if (slot.item == item && slot.count > 0 && slot.count < stackLimit)
                {
                    Debug.Log($"Auto-stacking {item.itemName} in existing slot {i}");
                    slot.count++;
                    RefreshUI();
                    return true;
                }
            }
        }

        // If no existing stack available, find first empty slot
        for (int i = 0; i < storedSlots.Count; i++)
        {
            var slot = storedSlots[i];
            if (slot.item == null)
            {
                Debug.Log($"Auto-placing {item.itemName} in empty slot {i}");
                slot.item = item;
                slot.count = 1;
                RefreshUI();
                return true;
            }
        }

        Debug.Log($"No available slots for {item.itemName}");
        return false; // No available slots
    }

    // Public method for save system to reset Flask data
    public void ResetFlaskData()
    {
        Debug.Log("Resetting Flask data for save restoration");
        for (int i = 0; i < storedSlots.Count; i++)
        {
            storedSlots[i].item = null;
            storedSlots[i].count = 0;
        }
        RefreshUI();
    }

    public bool TryTakeOneFromSlotToInventory(int slotIndex, SCInventory targetInventory)
    {
        Debug.Log($"TryTakeOneFromSlotToInventory called: slotIndex={slotIndex}");
        
        if (slotIndex < 0 || slotIndex >= storedSlots.Count) 
        {
            Debug.LogError($"Invalid slotIndex: {slotIndex}, storedSlots.Count: {storedSlots.Count}");
            return false;
        }
        if (targetInventory == null) 
        {
            Debug.LogError("targetInventory is null");
            return false;
        }

        var slot = storedSlots[slotIndex];
        Debug.Log($"Slot {slotIndex}: item={slot.item?.itemName}, count={slot.count}");
        
        if (slot.item == null || slot.count <= 0) 
        {
            Debug.Log($"Slot {slotIndex} is empty or has no count");
            return false;
        }

        // Deneme: önce envantere ekleyebilir miyiz?
        bool added = targetInventory.AddItem(slot.item);
        Debug.Log($"Adding {slot.item.itemName} to inventory: {added}");
        if (!added)
        {
            Debug.LogWarning("Envantere eklenemedi: boş slot yok veya stack limiti dolu.");
            return false;
        }

        // Başarılı ise Flask slotundan 1 azalt
        slot.count--;
        Debug.Log($"Decremented slot {slotIndex} count to {slot.count}");
        if (slot.count <= 0)
        {
            Debug.Log($"Clearing slot {slotIndex}");
            slot.item = null;
            slot.count = 0;
        }
        RefreshUI();
        return true;
    }

    public void RefreshUI()
    {
        Debug.Log("RefreshUI called");
        if (slotUIs == null) return;
        
        Debug.Log("=== REFRESH UI DEBUG ===");
        for (int i = 0; i < slotUIs.Length && i < storedSlots.Count; i++)
        {
            var t = slotUIs[i];
            if (t != null)
            {
                var slot = storedSlots[i];
                Debug.Log($"Refreshing UI for slot {i}: GameObject='{t.name}', item={slot.item?.itemName}, count={slot.count}");
                t.SendMessage("UpdateFlaskSlotUI", storedSlots[i], SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning($"SlotUI {i} is null during refresh!");
            }
        }
        Debug.Log("========================");
    }

    private void OnDisable()
    {
        if (_hoverApplied)
        {
            RestoreAlphaOnRenderers();
            _hoverApplied = false;
        }
        if (enableHoverScale)
        {
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            transform.localScale = _initialScale;
        }
        // Scene değişiminde veya disable olurken mevcut durumu kaydetmek için GameSaveManager'a push et
        var gsm = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>(true);
        if (gsm != null)
        {
            gsm.CaptureFlaskState(this);
        }
    }

    // UI EventSystem handlers (for Image-based objects)
    public void OnPointerEnter(PointerEventData eventData)
    {
    StartHoverScale();
    StartHoverCursor();
    _isPointerOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        EndHoverScale();
    EndHoverCursor();
        _isPointerOver = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StartClickScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleaseClickScale();
    }

    private void StartHoverScale()
    {
        if (!enableHoverScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        _scaleCo = StartCoroutine(ScaleTo(_initialScale * Mathf.Max(0.01f, hoverScale), Mathf.Max(0.01f, scaleInDuration)));
    }

    private void EndHoverScale()
    {
        if (!enableHoverScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        _scaleCo = StartCoroutine(ScaleTo(_initialScale, Mathf.Max(0.01f, scaleOutDuration)));
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        if (duration <= 0.0001f)
        {
            transform.localScale = target; yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.Lerp(start, target, u);
            yield return null;
        }
        transform.localScale = target;
        _scaleCo = null;
    }

    [Header("Click Scale (Press)")]
    [Tooltip("Tıklanırken küçülme efekti")] public bool enableClickScale = true;
    [Tooltip("Tıklama sırasında çarpan (1'den küçük önerilir, örn: 0.96)")] public float clickScale = 0.96f;
    [Tooltip("Basılıya geçiş süresi (sn)")] public float clickScaleInDuration = 0.06f;
    [Tooltip("Basılıdan çıkış süresi (sn)")] public float clickScaleOutDuration = 0.08f;

    private void StartClickScale()
    {
        if (!enableClickScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        float mult = Mathf.Clamp(clickScale, 0.5f, 1.5f);
        Vector3 target = transform.localScale * mult;
        _scaleCo = StartCoroutine(ScaleTo(target, Mathf.Max(0.01f, clickScaleInDuration)));
    }

    private void ReleaseClickScale()
    {
        if (!enableClickScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        Vector3 target = _isPointerOver && enableHoverScale ? _initialScale * Mathf.Max(0.01f, hoverScale) : _initialScale;
        _scaleCo = StartCoroutine(ScaleTo(target, Mathf.Max(0.01f, clickScaleOutDuration)));
    }

    private void StartHoverCursor()
    {
        if (hoverCursor == null) return;
        if (cursorSize != Vector2.zero && (hoverCursor.width != (int)cursorSize.x || hoverCursor.height != (int)cursorSize.y))
        {
            var scaled = ScaleCursor(hoverCursor, (int)cursorSize.x, (int)cursorSize.y);
            Cursor.SetCursor(scaled, cursorHotspot, CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
        }
    }

    private void EndHoverCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private Texture2D ScaleCursor(Texture2D originalCursor, int newWidth, int newHeight)
    {
        if (originalCursor == null) return null;
        Texture2D readable = MakeTextureReadable(originalCursor);
        if (readable == null) return originalCursor;
        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                float u = (float)x / newWidth;
                float v = (float)y / newHeight;
                Color pixel = readable.GetPixelBilinear(u, v);
                scaled.SetPixel(x, y, pixel);
            }
        }
        scaled.Apply();
        if (readable != originalCursor)
        {
            DestroyImmediate(readable);
        }
        return scaled;
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
            Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }
    }

    private void CacheOriginalAlphas()
    {
        _originalAlpha.Clear();
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            float a = GetRendererAlpha(r);
            _originalAlpha[r] = a;
        }
    }

    private float GetRendererAlpha(Renderer r)
    {
        if (r == null) return 1f;
        var mats = r.sharedMaterials;
        if (mats != null && mats.Length > 0)
        {
            var m = mats[0];
            if (m != null)
            {
                for (int i = 0; i < colorPropertyNames.Length; i++)
                {
                    string prop = colorPropertyNames[i];
                    if (m.HasProperty(prop))
                    {
                        Color c = m.GetColor(prop);
                        return c.a;
                    }
                }
            }
        }
        var sr = r.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.color.a;
        return 1f;
    }

    private void ApplyAlphaToRenderers(float alpha)
    {
        if (_renderers == null) return;
        alpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            bool applied = false;
            var mats = r.sharedMaterials;
            if (mats != null && mats.Length > 0)
            {
                for (int pi = 0; pi < colorPropertyNames.Length; pi++)
                {
                    string prop = colorPropertyNames[pi];
                    if (mats[0] != null && mats[0].HasProperty(prop))
                    {
                        r.GetPropertyBlock(_mpb);
                        Color c = mats[0].GetColor(prop);
                        c.a = alpha;
                        _mpb.SetColor(prop, c);
                        r.SetPropertyBlock(_mpb);
                        applied = true;
                        break;
                    }
                }
            }
            if (!applied)
            {
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color; c.a = alpha; sr.color = c; applied = true;
                }
            }
        }
    }

    private void RestoreAlphaOnRenderers()
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            float alpha = 1f;
            if (_originalAlpha.TryGetValue(r, out var a)) alpha = a;

            bool restored = false;
            var mats = r.sharedMaterials;
            if (mats != null && mats.Length > 0)
            {
                for (int pi = 0; pi < colorPropertyNames.Length; pi++)
                {
                    string prop = colorPropertyNames[pi];
                    if (mats[0] != null && mats[0].HasProperty(prop))
                    {
                        r.GetPropertyBlock(_mpb);
                        Color c = mats[0].GetColor(prop);
                        c.a = alpha;
                        _mpb.SetColor(prop, c);
                        r.SetPropertyBlock(_mpb);
                        restored = true;
                        break;
                    }
                }
            }
            if (!restored)
            {
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color; c.a = alpha; sr.color = c; restored = true;
                }
            }
        }
    }
}