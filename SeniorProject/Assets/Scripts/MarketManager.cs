using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// UI butonu ile aç/kapat yapılan slot tabanlı market (2D/Ortho uyumlu).
/// - Inspector’dan atanabilir Button ile açılır
/// - ESC ile kapanır (Pause’u tetiklemez, PauseMenu bunu kontrol eder)
/// - Market açıkken şeffaf tam ekran overlay ile diğer UI tıklamaları engellenir
/// - InventoryManager üzerinden envantere ekler ve UI’ı yeniler
/// - Save/restore için ApplySavedOffers mevcuttur
/// </summary>
public class MarketManager : MonoBehaviour
{
    [Header("Slots & Offers")]
    public int slotCount = 6;
    public List<ProductEntry> productPool = new List<ProductEntry>();
    public bool allowDuplicates = false;
    public bool refreshOffersOnOpen = true;

    [Header("UI Open")]
    public Button openMarketButton;

    [Header("Hover Scale (Open Button)")]
    [Tooltip("Market butonuna hover scale efekti uygula")] public bool enableHoverScaleForMarketButton = true;
    [Tooltip("Hedef ölçek çarpanı (1 = orijinal)")] public float marketButtonHoverScale = 1.08f;
    [Tooltip("Hover'a geçiş süresi (sn)")] public float marketButtonScaleInDuration = 0.12f;
    [Tooltip("Hover'dan çıkış süresi (sn)")] public float marketButtonScaleOutDuration = 0.12f;
    private Vector3 _btnInitialScale;
    private Coroutine _btnScaleCo;

    [Header("UI")]
    public GameObject marketPanel;
    public GameObject slotPrefab;
    public Transform slotsRoot;
    public TextMeshProUGUI moneyText;
    [Tooltip("Market açıkken diğer tıklamaları engellemek için tam ekran şeffaf overlay. Boşsa otomatik oluşturulur.")]
    public GameObject inputBlockerOverlay;

    private int _lastMoneyShown = int.MinValue;
    // Track canvas groups we disable to block outside clicks while market open
    private struct CanvasGroupState { public CanvasGroup cg; public bool interactable; public bool blocksRaycasts; }
    private readonly List<CanvasGroupState> _disabledCanvasGroups = new List<CanvasGroupState>();

    [Header("Economy")]
    public int playerMoney = 1000; // fallback when MoneyManager is not available
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

    // ESC consumption flag for this frame to prevent PauseMenu from reacting
    public static int s_lastEscapeConsumedFrame = -1;
    public static bool DidConsumeEscapeThisFrame() => s_lastEscapeConsumedFrame == Time.frameCount;

    private void Start()
    {
        _inventory = FindObjectOfType<Inventory>();
        if (marketPanel != null) marketPanel.SetActive(false);
        if (openMarketButton != null)
        {
            openMarketButton.onClick.RemoveListener(OpenMarket);
            openMarketButton.onClick.AddListener(OpenMarket);
            SetupMarketButtonHoverScale();
        }
        // Sync local fallback money with MoneyManager if present
        if (MoneyManager.Instance != null)
        {
            playerMoney = MoneyManager.Instance.Balance;
        }
        RefreshMoneyText();
    }

    private void Update()
    {
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
    }

    private void OnDisable()
    {
        // Reset button scale if effect was used
        if (openMarketButton != null && enableHoverScaleForMarketButton)
        {
            if (_btnScaleCo != null) StopCoroutine(_btnScaleCo);
            var t = openMarketButton.transform as RectTransform;
            if (t != null && _btnInitialScale != Vector3.zero)
                t.localScale = _btnInitialScale;
        }
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
        return playerMoney;
    }

    private bool CanAfford(int price)
    {
        if (MoneyManager.Instance != null) return MoneyManager.Instance.CanAfford(price);
        return playerMoney >= price;
    }

    private void Spend(int price)
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.TrySpend(price);
        }
        else
        {
            playerMoney = Mathf.Max(0, playerMoney - price);
        }
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

    // PauseMenuController bu metodu kontrol ederek pause’u bastırabilir
    public bool IsPanelActive()
    {
        return marketPanel != null && marketPanel.activeSelf;
    }

    private void SetupMarketButtonHoverScale()
    {
        if (!enableHoverScaleForMarketButton || openMarketButton == null) return;
        var t = openMarketButton.transform as RectTransform;
        if (t == null) return;
        _btnInitialScale = t.localScale;

        var et = openMarketButton.GetComponent<EventTrigger>();
        if (et == null) et = openMarketButton.gameObject.AddComponent<EventTrigger>();

        AddOrReplaceTrigger(et, EventTriggerType.PointerEnter, OnMarketButtonPointerEnter);
        AddOrReplaceTrigger(et, EventTriggerType.PointerExit, OnMarketButtonPointerExit);
    }

    private void AddOrReplaceTrigger(EventTrigger et, EventTriggerType type, System.Action<BaseEventData> callback)
    {
        if (et == null) return;
        // Remove previous entries of same type added by us
        if (et.triggers == null) et.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        et.triggers.RemoveAll(e => e != null && e.eventID == type);
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(callback));
        et.triggers.Add(entry);
    }

    private void OnMarketButtonPointerEnter(BaseEventData _)
    {
        if (!enableHoverScaleForMarketButton || openMarketButton == null) return;
        var t = openMarketButton.transform as RectTransform;
        if (t == null) return;
        if (_btnScaleCo != null) StopCoroutine(_btnScaleCo);
        Vector3 target = _btnInitialScale * Mathf.Max(0.01f, marketButtonHoverScale);
        _btnScaleCo = StartCoroutine(ScaleRectTransform(t, target, Mathf.Max(0.01f, marketButtonScaleInDuration)));
    }

    private void OnMarketButtonPointerExit(BaseEventData _)
    {
        if (!enableHoverScaleForMarketButton || openMarketButton == null) return;
        var t = openMarketButton.transform as RectTransform;
        if (t == null) return;
        if (_btnScaleCo != null) StopCoroutine(_btnScaleCo);
        _btnScaleCo = StartCoroutine(ScaleRectTransform(t, _btnInitialScale, Mathf.Max(0.01f, marketButtonScaleOutDuration)));
    }

    private System.Collections.IEnumerator ScaleRectTransform(RectTransform t, Vector3 target, float duration)
    {
        Vector3 start = t.localScale;
        if (duration <= 0.0001f) { t.localScale = target; yield break; }
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(time / duration);
            t.localScale = Vector3.Lerp(start, target, u);
            yield return null;
        }
        t.localScale = target;
        _btnScaleCo = null;
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
}

