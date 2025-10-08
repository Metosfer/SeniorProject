using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Proximity-based fish market system (similar to MarketManager but opened by approaching and pressing E).
/// - Player approaches the object and presses E to open
/// - ESC closes the market (Pause is not triggered, PauseMenu handles this)
/// - Market open blocks other UI interactions with transparent full-screen overlay
/// - Adds items to inventory via InventoryManager and refreshes UI
/// - Supports save/restore for offers
/// </summary>
public class FishMarketManager : MonoBehaviour
{
    [Header("Slots & Offers")]
    public int slotCount = 6;
    public List<ProductEntry> productPool = new List<ProductEntry>();
    public bool allowDuplicates = false;
    public bool refreshOffersOnOpen = true;

    [Header("UI")]
    public GameObject marketPanel;
    public GameObject slotPrefab;
    public Transform slotsRoot;
    public TextMeshProUGUI moneyText;
    [Tooltip("Market açıkken diğer tıklamaları engellemek için tam ekran şeffaf overlay. Boşsa otomatik oluşturulur.")]
    public GameObject inputBlockerOverlay;

    [Header("Player Detection")]
    [Tooltip("Oyuncunun markete yaklaşma mesafesi (E tuşu aktif olur)")]
    public float interactionRange = 3f;
    [Tooltip("Oyuncunun uzaklaşma mesafesi (prompt gizlenir, flicker önlenir)")]
    public float exitRange = 4f; // Range'den çıkış mesafesi (flickering önlemek için)
    [Tooltip("Oyuncu karakterinin Transform component'i")]
    public Transform playerTransform;
    
    [Header("UI Elements")]
    [Tooltip("Etkileşim metni gösterecek GameObject (Press E to Open Fish Market)")]
    public GameObject promptText;
    [Tooltip("UI Text component referansı (Canvas üzerindeki text için)")]
    public Text promptTextComponent; // UI Text için
    [Tooltip("3D dünyada gösterilecek TextMeshPro component referansı")]
    public TMPro.TextMeshPro promptTextMeshPro; // 3D TextMeshPro için
    
    private int _lastMoneyShown = int.MinValue;
    // Track canvas groups we disable to block outside clicks while market open
    private struct CanvasGroupState { public CanvasGroup cg; public bool interactable; public bool blocksRaycasts; }
    private readonly List<CanvasGroupState> _disabledCanvasGroups = new List<CanvasGroupState>();

    [Header("Economy")]
    // Removed playerMoney - now uses MoneyManager.Instance.Balance exclusively
    // Global flag so world interactables can respect market-open state
    private static bool s_isAnyOpen = false;
    public static bool IsAnyOpen => s_isAnyOpen;

    [Serializable]
    public class ProductEntry
    {
        public SCItem item;
        [Min(1)] public int price = 10;
        public string displayNameOverride;
        public Sprite icon;
        [Min(0)] public int initialStock = 5;
    }

    private class Offer
    {
        public SCItem item;
        public int price;
        public string displayName;
        public Sprite icon;
        public int stock;
    }

    private readonly List<Offer> _activeOffers = new List<Offer>();
    private Inventory _inventory;
    private bool _offersRestoredFromSave = false;
    private static readonly Color OverlayTransparent = new Color(0f, 0f, 0f, 0f);
    private bool playerInRange = false;
    private float lastDistanceCheckTime = 0f; // To reduce flicker, check distance less frequently
    private const float DISTANCE_CHECK_INTERVAL = 0.1f; // Check every 0.1 seconds

    // ESC consumption flag for this frame to prevent PauseMenu from reacting
    public static int s_lastEscapeConsumedFrame = -1;
    public static bool DidConsumeEscapeThisFrame() => s_lastEscapeConsumedFrame == Time.frameCount;    private void Start()
    {
        _inventory = FindObjectOfType<Inventory>();
        if (marketPanel != null) marketPanel.SetActive(false);
        if (promptText != null)
            promptText.SetActive(false);
        // Money is now handled exclusively by MoneyManager.Instance.Balance
        RefreshMoneyText();
    }

    private void Update()
    {
        // Check distance less frequently to prevent flicker
        if (Time.time - lastDistanceCheckTime >= DISTANCE_CHECK_INTERVAL)
        {
            lastDistanceCheckTime = Time.time;
            CheckPlayerDistance();
        }
        
        HandleInput();
        
        // Esc: sadece marketi kapat, PauseMenu bu durumda açılmamalı
    if (marketPanel != null && marketPanel.activeSelf && InputHelper.GetKeyDown(KeyCode.Escape))
        {
            s_lastEscapeConsumedFrame = Time.frameCount;
            CloseMarket();
            return;
        }
        if (_lastMoneyShown != GetCurrentMoney())
        {
            RefreshMoneyText();
        }
    }    void CheckPlayerDistance()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // Hysteresis sistemi: Giriş ve çıkış için farklı mesafeler
        if (!playerInRange && distance <= interactionRange)
        {
            playerInRange = true;
            ShowPrompt();
        }
        else if (playerInRange && distance > exitRange)
        {
            playerInRange = false;
            HidePrompt();
        }
    }

    void HandleInput()
    {
    if (playerInRange && InputHelper.GetKeyDown(KeyCode.E) && !marketPanel.activeSelf)
        {
            OpenMarket();
        }
    }

    void ShowPrompt()
    {
        if (promptText != null)
        {
            promptText.SetActive(true);

            // UI Text için
            if (promptTextComponent != null)
                promptTextComponent.text = "Press E to Open Fish Market";

            // 3D TextMeshPro için
            if (promptTextMeshPro != null)
                promptTextMeshPro.text = "Press E to Open Fish Market";
        }
    }

    void HidePrompt()
    {
        if (promptText != null)
            promptText.SetActive(false);
    }

    public void ToggleMarket()
    {
        if (marketPanel == null) return;
        bool next = !marketPanel.activeSelf;
        if (next) OpenMarket(); else CloseMarket();
    }

    public void OpenFromUIButton() => OpenMarket();
    public void CloseFromUIButton() => CloseMarket();

    public void OpenMarket()
    {
        if (marketPanel == null) return;
        HidePrompt();
        marketPanel.SetActive(true);
        // ensure panel and blocker sit at the very top of their canvas
        marketPanel.transform.SetAsLastSibling();
        if (((refreshOffersOnOpen && !_offersRestoredFromSave)) || _activeOffers.Count == 0) GenerateOffers();
        BuildSlotUI();
        RefreshMoneyText();

        var cam = FindObjectOfType<EazyCamera.EazyCam>();
        if (cam != null) cam.SetZoomEnabled(EazyCamera.EnabledState.Disabled);

        EnsureInputBlocker();
        if (inputBlockerOverlay != null)
        {
            // Put overlay on top, then bring marketPanel above it so overlay blocks everything else
            inputBlockerOverlay.SetActive(true);
            inputBlockerOverlay.transform.SetAsLastSibling();
            marketPanel.transform.SetAsLastSibling();
        }

        // Additionally, disable other CanvasGroups (in all canvases) to block clicks outside market
        DisableOtherCanvasGroups();
        // Mark globally open so world-click interactables (Flask/Book etc.) can ignore input
        s_isAnyOpen = true;
    }

    public void CloseMarket()
    {
        if (marketPanel == null) return;
        marketPanel.SetActive(false);
        var cam = FindObjectOfType<EazyCamera.EazyCam>();
        if (cam != null) cam.SetZoomEnabled(EazyCamera.EnabledState.Enabled);
        if (inputBlockerOverlay != null) inputBlockerOverlay.SetActive(false);
        RestoreOtherCanvasGroups();
        s_isAnyOpen = false;

        if (playerInRange)
            ShowPrompt();
    }

    private void GenerateOffers()
    {
        _activeOffers.Clear();
        if (productPool == null || productPool.Count == 0) return;
        List<int> indices = new List<int>();
        for (int i = 0; i < productPool.Count; i++) indices.Add(i);
        for (int s = 0; s < slotCount; s++)
        {
            if (!allowDuplicates && indices.Count == 0) break;
            int idx = allowDuplicates ? UnityEngine.Random.Range(0, productPool.Count) : indices[UnityEngine.Random.Range(0, indices.Count)];
            if (!allowDuplicates) indices.Remove(idx);
            var entry = productPool[idx];
            if (entry == null || entry.item == null) { s--; continue; }
            _activeOffers.Add(new Offer
            {
                item = entry.item,
                price = Mathf.Max(1, entry.price),
                displayName = string.IsNullOrEmpty(entry.displayNameOverride) ? entry.item.itemName : entry.displayNameOverride,
                icon = entry.icon,
                stock = Mathf.Max(0, entry.initialStock)
            });
        }
        _offersRestoredFromSave = false;
    }

    private void BuildSlotUI()
    {
        if (slotsRoot == null || slotPrefab == null)
        {
            Debug.LogWarning("Market slotsRoot/slotPrefab atanmamış.");
            return;
        }
        for (int i = slotsRoot.childCount - 1; i >= 0; i--)
            Destroy(slotsRoot.GetChild(i).gameObject);
        for (int i = 0; i < _activeOffers.Count; i++)
        {
            var ui = Instantiate(slotPrefab, slotsRoot);
            var slot = ui.GetComponent<MarketSlotUI>();
            if (slot != null)
            {
                var off = _activeOffers[i];
                slot.Bind(this, i, off.displayName, off.price, off.icon, off.stock);
            }
            else
            {
                var texts = ui.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (t.name == "NameText") t.text = _activeOffers[i].displayName;
                    else if (t.name == "PriceText") t.text = _activeOffers[i].price.ToString();
                    else if (t.name == "StockText") t.text = _activeOffers[i].stock.ToString();
                }
                var btn = ui.GetComponentInChildren<Button>(true);
                int idx = i;
                if (btn != null) btn.onClick.AddListener(() => AttemptPurchase(idx));
            }
        }
    }

    public void AttemptPurchase(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _activeOffers.Count) return;
        var offer = _activeOffers[slotIndex];
        if (offer == null || offer.item == null) return;
        if (offer.stock <= 0) { Debug.Log("Stok tükendi."); return; }
        if (!CanAfford(offer.price)) { Debug.Log("Yetersiz para."); return; }
        bool added = TryAddToInventory(offer.item);
        if (!added) { Debug.LogWarning("Envanter dolu veya eklenemedi."); return; }
        Spend(offer.price);
        offer.stock = Mathf.Max(0, offer.stock - 1);
        RefreshMoneyText();
        if (slotsRoot != null && slotIndex < slotsRoot.childCount)
        {
            var slotUi = slotsRoot.GetChild(slotIndex).GetComponent<MarketSlotUI>();
            if (slotUi != null) slotUi.UpdateStock(offer.stock);
            else
            {
                var texts = slotsRoot.GetChild(slotIndex).GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (t.name == "StockText") { t.text = offer.stock.ToString(); break; }
                }
            }
        }
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RefreshInventoryUI();
        }
        else
        {
            // Fallback: refresh any InventoryUI directly using the persistent inventory
            var persistent = SCInventory.GetPersistentInventory();
            var uiManagers = FindObjectsOfType<InventoryUIManager>();
            foreach (var ui in uiManagers)
            {
                if (ui == null) continue;
                if (ui.inventory != persistent)
                {
                    ui.SetInventoryReference(persistent);
                }
                ui.RefreshUI();
            }
            if (persistent != null) persistent.TriggerInventoryChanged();
        }
    }

    private bool TryAddToInventory(SCItem item)
    {
        var invMgr = InventoryManager.Instance;
        SCInventory target = null;
        if (invMgr != null)
        {
            target = invMgr.GetPlayerInventory();
        }
        else
        {
            // Always fallback to persistent inventory so ShopScene works without an InventoryManager/Inventory component
            target = SCInventory.GetPersistentInventory();
        }
        if (target != null)
        {
            return target.AddItem(item);
        }
        Debug.LogWarning("Inventory bulunamadı ve persistent inventory oluşturulamadı");
        return false;
    }

    private void RefreshMoneyText()
    {
        int current = GetCurrentMoney();
        _lastMoneyShown = current;
        if (moneyText != null) moneyText.text = $"{current}";
    }

    private int GetCurrentMoney()
    {
        if (MoneyManager.Instance != null) return MoneyManager.Instance.Balance;
        return 0; // No fallback money
    }

    private bool CanAfford(int price)
    {
        if (MoneyManager.Instance != null) return MoneyManager.Instance.CanAfford(price);
        return false; // Cannot afford if no MoneyManager
    }

    private void Spend(int price)
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.TrySpend(price);
        }
        // No fallback spending
    }

    private void EnsureInputBlocker()
    {
        if (inputBlockerOverlay != null) return;
        if (marketPanel == null) return;
        var canvas = marketPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("MarketInputBlocker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var img = go.GetComponent<Image>();
        img.color = OverlayTransparent; // görünmez ancak raycast yakalar
        img.raycastTarget = true;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.SetActive(false);
        inputBlockerOverlay = go;
    }

    // Disable all CanvasGroups except ones under the marketPanel to block outside UI interactions
    private void DisableOtherCanvasGroups()
    {
        _disabledCanvasGroups.Clear();
        // find all CanvasGroups including inactive
        var allGroups = Resources.FindObjectsOfTypeAll<CanvasGroup>();
        foreach (var cg in allGroups)
        {
            if (cg == null || cg.gameObject == null) continue;
            // skip assets/prefabs not in scene
            if (!cg.gameObject.scene.IsValid()) continue;
            // skip groups that are children of the market panel
            if (marketPanel != null && cg.transform.IsChildOf(marketPanel.transform)) continue;
            // skip the blocker and marketPanel itself
            if (inputBlockerOverlay != null && cg.gameObject == inputBlockerOverlay) continue;
            if (marketPanel != null && cg.gameObject == marketPanel) continue;
            // only disable groups attached to UI in any canvas
            if (cg.GetComponentInParent<Canvas>() == null) continue;

            _disabledCanvasGroups.Add(new CanvasGroupState { cg = cg, interactable = cg.interactable, blocksRaycasts = cg.blocksRaycasts });
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }

    private void RestoreOtherCanvasGroups()
    {
        for (int i = 0; i < _disabledCanvasGroups.Count; i++)
        {
            var s = _disabledCanvasGroups[i];
            if (s.cg != null)
            {
                s.cg.interactable = s.interactable;
                s.cg.blocksRaycasts = s.blocksRaycasts;
            }
        }
        _disabledCanvasGroups.Clear();
    }

    // PauseMenuController bu metodu kontrol ederek pause'u bastırabilir
    public bool IsPanelActive()
    {
        return marketPanel != null && marketPanel.activeSelf;
    }

    // SaveSystem entegrasyonu
    public void ApplySavedOffers(System.Collections.Generic.List<OfferSaveData> saved)
    {
        _activeOffers.Clear();
        if (saved == null) { RefreshMoneyText(); return; }
        foreach (var s in saved)
        {
            if (string.IsNullOrEmpty(s.itemName)) continue;
            var item = FindItemByName(s.itemName);
            if (item == null) continue;
            ProductEntry meta = null;
            if (productPool != null)
                meta = productPool.Find(pe => pe != null && pe.item == item);
            var display = (meta != null && !string.IsNullOrEmpty(meta.displayNameOverride)) ? meta.displayNameOverride : (string.IsNullOrEmpty(item.itemName) ? s.itemName : item.itemName);
            var icon = meta != null ? meta.icon : null;
            _activeOffers.Add(new Offer
            {
                item = item,
                price = Mathf.Max(1, s.price),
                displayName = display,
                icon = icon,
                stock = Mathf.Max(0, s.stock)
            });
        }
        if (marketPanel != null && marketPanel.activeSelf) { BuildSlotUI(); RefreshMoneyText(); }
        else { RefreshMoneyText(); }
        _offersRestoredFromSave = _activeOffers.Count > 0;
    }

    private SCItem FindItemByName(string itemName)
    {
        var gsm = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
        var mi = typeof(GameSaveManager).GetMethod("FindItemByName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (gsm != null && mi != null)
        {
            return (SCItem)mi.Invoke(gsm, new object[] { itemName });
        }
        foreach (var it in Resources.LoadAll<SCItem>(""))
        {
            if (it != null && it.itemName == itemName) return it;
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        // Etkileşim alanını görselleştir (yeşil)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Çıkış alanını görselleştir (kırmızı)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, exitRange);
    }
}
