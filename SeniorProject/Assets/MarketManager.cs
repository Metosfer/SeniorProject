using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Basit slot tabanlı market: market objesine yaklaşınca (E) panel açılır, slotlarda rastgele ürünler görünür.
/// Ürün tıklanınca yeterli para varsa satın alır ve envantere ekler.
/// </summary>
public class MarketManager : MonoBehaviour
{
    [Header("Slots & Offers")]
    [Tooltip("Slot sayısı (UI'da gösterilecek teklif sayısı).")]
    public int slotCount = 6;

    [Tooltip("Rastgele seçilecek ürün havuzu (ürün ve fiyatı).")]
    public List<ProductEntry> productPool = new List<ProductEntry>();

    [Tooltip("Aynı ürün birden fazla slota gelebilsin mi?")]
    public bool allowDuplicates = false;

    [Tooltip("Panel her açıldığında slotları yeniden doldur.")]
    public bool refreshOffersOnOpen = true;

    [Header("Interaction")]
    [Tooltip("Market etkileşimi için giriş yarıçapı (m).")]
    public float interactionRange = 3f;
    [Tooltip("Çıkış yarıçapı (m). Flicker'ı önlemek için girişten büyük olmalı.")]
    public float interactionExitRange = 3.5f;
    [Tooltip("Menzil durum değişimleri arasındaki minimum süre (sn).")]
    public float interactionDebounce = 0.1f;
    [Tooltip("Market panelini aç/kapat tuşu.")]
    public KeyCode interactionKey = KeyCode.E;
    [Tooltip("Oyuncuya ipucu gösterecek (Press E ...) UI objesi (opsiyonel).")]
    public GameObject interactionHintUI;

    [Header("UI")]
    [Tooltip("Market panel GameObject'i (aç/kapat).")]
    public GameObject marketPanel;
    [Tooltip("Slot prefab (üstünde MarketSlotUI olmalı).")]
    public GameObject slotPrefab;
    [Tooltip("Slotların ekleneceği Content (ScrollView/Viewport/Content).")]
    public Transform slotsRoot;
    [Tooltip("Opsiyonel: oyuncu parasını gösteren Text.")]
    public TextMeshProUGUI moneyText;
        private int _lastMoneyShown = int.MinValue;

    [Header("Economy (Demo)")]
    [Tooltip("Oyuncu bakiyesi (demo). Kendi para sistemin varsa onunla değiştir.")]
    public int playerMoney = 1000;

    [Serializable]
    public class ProductEntry
    {
        [Tooltip("Satılacak SCItem (envanter item'ı).")]
        public SCItem item;
        [Min(1)] [Tooltip("Birim fiyat.")]
        public int price = 10;
        [Tooltip("UI'da gösterilecek isim (boşsa item.itemName kullanılır).")]
        public string displayNameOverride;
        [Tooltip("UI simgesi (boşsa item.icon vb. kullanılabilir).")]
        public Sprite icon;
    [Min(0)] [Tooltip("Bu üründen başlangıç stoğu (slot başına kopyalanır).")]
    public int initialStock = 5;
    }

    private readonly List<Offer> _activeOffers = new List<Offer>();
    private Transform _player;
    private bool _inRange;
    private float _lastToggle;
    private Inventory _inventory;
    private bool _offersRestoredFromSave = false;

    private class Offer
    {
        public SCItem item;
        public int price;
        public string displayName;
        public Sprite icon;
    public int stock;
    }

    private void Start()
    {
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        _inventory = FindObjectOfType<Inventory>();

        if (marketPanel != null) marketPanel.SetActive(false);
        if (interactionHintUI != null) { interactionHintUI.SetActive(false); MakeNonBlocking(interactionHintUI); }
        RefreshMoneyText(); // Para metnini başlangıçta güncelle
    }

    private void Update()
    {
        HandleProximity();
        // ESC ile kapat, pause menüyü engellemek için PauseMenuController tarafında da market açık kontrolü var
        if (marketPanel != null && marketPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMarket();
            return;
        }
        if (_inRange && Input.GetKeyDown(interactionKey))
        {
            ToggleMarket();
        }
            // Market kapalıyken de para metnini güncel tut
            if (_lastMoneyShown != playerMoney)
            {
                RefreshMoneyText();
            }
    }

    private void HandleProximity()
    {
        if (_player == null) return;
        Vector3 d = _player.position - transform.position;
        float sq = d.sqrMagnitude;
        float enter = interactionRange * interactionRange;
        float exit = interactionExitRange * interactionExitRange;

        bool shouldBeInRange = _inRange ? (sq <= exit) : (sq <= enter);
        if (shouldBeInRange != _inRange && (Time.time - _lastToggle) >= interactionDebounce)
        {
            _inRange = shouldBeInRange;
            _lastToggle = Time.time;
            if (interactionHintUI != null) interactionHintUI.SetActive(_inRange && (marketPanel == null || !marketPanel.activeSelf));
        }
        else if (interactionHintUI != null && marketPanel != null)
        {
            // Panel açıksa ipucunu gizle; değilse menzil içindeyse göster
            if (marketPanel.activeSelf)
                interactionHintUI.SetActive(false);
            else if (_inRange)
                interactionHintUI.SetActive(true);
        }
    }

    public void ToggleMarket()
    {
        if (marketPanel == null) return;
        bool next = !marketPanel.activeSelf;
        if (next) OpenMarket(); else CloseMarket();
    }

    public void OpenMarket()
    {
        if (marketPanel == null) return;
        marketPanel.SetActive(true);
    if (((refreshOffersOnOpen && !_offersRestoredFromSave)) || _activeOffers.Count == 0) GenerateOffers();
        BuildSlotUI();
        RefreshMoneyText();
        if (interactionHintUI != null) interactionHintUI.SetActive(false);

    // Kamera zoom'u kapat: ScrollView rahat kaydırabilsin
    var cam = FindObjectOfType<EazyCamera.EazyCam>();
    if (cam != null) cam.SetZoomEnabled(EazyCamera.EnabledState.Disabled);
    }

    public void CloseMarket()
    {
        if (marketPanel == null) return;
        marketPanel.SetActive(false);
        if (interactionHintUI != null && _inRange) interactionHintUI.SetActive(true);

    // Kamera zoom'u tekrar aç
    var cam = FindObjectOfType<EazyCamera.EazyCam>();
    if (cam != null) cam.SetZoomEnabled(EazyCamera.EnabledState.Enabled);
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

    // Artık rastgele üretildi, restore bayrağını sıfırla
    _offersRestoredFromSave = false;
    }

    private void BuildSlotUI()
    {
        if (slotsRoot == null || slotPrefab == null)
        {
            Debug.LogWarning("Market slotsRoot/slotPrefab atanmamış.");
            return;
        }

        // temizle
        for (int i = slotsRoot.childCount - 1; i >= 0; i--)
            Destroy(slotsRoot.GetChild(i).gameObject);

        // oluştur
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
                // Fallback: isim/price/button adlarıyla bulmaya çalış
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

        if (offer.stock <= 0)
        {
            Debug.Log("Stok tükendi.");
            return;
        }

        if (playerMoney < offer.price)
        {
            Debug.Log("Yetersiz para.");
            return;
        }

        // Envantere ekle
        bool added = TryAddToInventory(offer.item);
        if (!added)
        {
            Debug.LogWarning("Envanter dolu veya eklenemedi.");
            return;
        }

        playerMoney -= offer.price;
        offer.stock = Mathf.Max(0, offer.stock - 1);
        RefreshMoneyText();
        Debug.Log($"Satın alındı: {offer.displayName} ({offer.price})");

        // UI güncelle
        if (slotsRoot != null && slotIndex < slotsRoot.childCount)
        {
            var slotUi = slotsRoot.GetChild(slotIndex).GetComponent<MarketSlotUI>();
            if (slotUi != null)
            {
                slotUi.UpdateStock(offer.stock);
            }
            else
            {
                var texts = slotsRoot.GetChild(slotIndex).GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (t.name == "StockText")
                    {
                        t.text = offer.stock.ToString();
                        break;
                    }
                }
            }
        }
    }

    private bool TryAddToInventory(SCItem item)
    {
        // DryingAreaManager örneğine uygun: Inventory.playerInventory.AddItem(SCItem)
        if (_inventory != null && _inventory.playerInventory != null)
        {
            return _inventory.playerInventory.AddItem(item);
        }
        Debug.LogWarning("Inventory bulunamadı veya geçersiz.");
        return false;
    }

    private void RefreshMoneyText()
    {
        _lastMoneyShown = playerMoney;
        if (moneyText != null) moneyText.text = $"Money: {playerMoney}";
    }

    private void MakeNonBlocking(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false;
        foreach (var g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    // SaveSystem entegrasyonu: dışarıdan kaydedilmiş teklifleri uygula
    public void ApplySavedOffers(System.Collections.Generic.List<OfferSaveData> saved)
    {
        _activeOffers.Clear();
        if (saved == null) { RefreshMoneyText(); return; }

        foreach (var s in saved)
        {
            if (string.IsNullOrEmpty(s.itemName)) continue;
            var item = FindItemByName(s.itemName);
            if (item == null) continue;
            // productPool'da metadata var ise kullan
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

        // UI’ı güncelle
        if (marketPanel != null && marketPanel.activeSelf)
        {
            BuildSlotUI();
            RefreshMoneyText();
        }
        else
        {
            // Panel kapalı olsa bile para metnini güncelle
            RefreshMoneyText();
        }

    // Bir sonraki açılışta random generate’i bastır
    _offersRestoredFromSave = _activeOffers.Count > 0;
    }

    private SCItem FindItemByName(string itemName)
    {
        // GameSaveManager’ın metodunu kullan
        var gsm = GameSaveManager.Instance ?? FindObjectOfType<GameSaveManager>();
        var mi = typeof(GameSaveManager).GetMethod("FindItemByName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (gsm != null && mi != null)
        {
            return (SCItem)mi.Invoke(gsm, new object[] { itemName });
        }
        // Fallback: Resources’dan ara
        foreach (var it in Resources.LoadAll<SCItem>(""))
        {
            if (it != null && it.itemName == itemName) return it;
        }
        return null;
    }
}

