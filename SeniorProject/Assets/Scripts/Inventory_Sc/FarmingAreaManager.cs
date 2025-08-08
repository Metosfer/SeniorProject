using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

// FarmingAreaManager: Seed bırakma, büyüme süresi ve olgun bitki spawn akışını yönetir.
// Notlar:
// - DragAndDropHandler, isSeed kontrolü geçerse bu component'in OnDrop(PointerEventData) metodunu çağırır.
// - SCItem tarafında seed bilgileri (isSeed, growthTime, vb.) olduğunu varsayıyoruz.
// - Olgun bitki için varsayılan olarak SCItem.itemPrefab kullanılır (prefab üzerinde Plant script'i ve item ataması önerilir).
public class FarmingAreaManager : MonoBehaviour, IDropHandler
{
    [Header("Planting Spots")]
    [Tooltip("Ekim yapılacak noktalar (boş GameObject'ler). İlk uygun boş slot seçilir.")]
    public List<Transform> plotPoints = new List<Transform>();

    [Header("Visuals & Feedback")]
    [Tooltip("Ekim anında (opsiyonel) gösterilecek küçük bir marker prefab (tohum izi).")]
    public GameObject seedMarkerPrefab;
    [Tooltip("Büyüme tamamlandığında oynatılacak VFX veya Particle prefab.")]
    public GameObject growVFXPrefab;
    [Tooltip("Hazır plot sayısını gösterecek metin (3/9 hazır). World-space ya da Canvas üzeri olabilir.")]
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    public TMP_Text statusTMP;
#endif
    public TMP_Text statusTextUI;
    [Tooltip("Durum metni formatı. {0}=hazır, {1}=toplam.")]
    public string statusFormat = "{0}/{1} hazır";

    [Header("Growth Settings")]
    [Tooltip("SCItem üzerinde growthTime yoksa kullanılacak varsayılan süre (sn).")]
    public float defaultGrowthTime = 10f;
    [Tooltip("SCItem.growthTime varsa onu kullan, yoksa default'u kullan.")]
    public bool useItemGrowthTimeIfAvailable = true;

    [Header("Placement")]
    [Tooltip("Ekim pozisyonunda küçük bir offset uygula (yerden hafif yukarı).")]
    public Vector3 spawnOffset = new Vector3(0f, 0.02f, 0f);

    // İç durum takibi
    private readonly List<PlotState> _plots = new List<PlotState>();

    private void Awake()
    {
        SyncPlotStatesWithPoints();
    UpdateStatusText();
    }

    private void OnValidate()
    {
        // Editor'da değiştiğinde slot state listesini güncel tut
        SyncPlotStatesWithPoints();
    }

    private void SyncPlotStatesWithPoints()
    {
        // Plot sayısı değişmiş olabilir; state listesini hizala
        if (_plots.Count != plotPoints.Count)
        {
            // Eski state'leri koruyarak yeniden hizala
            var newList = new List<PlotState>(plotPoints.Count);
            for (int i = 0; i < plotPoints.Count; i++)
            {
                if (i < _plots.Count)
                {
                    newList.Add(_plots[i]);
                }
                else
                {
                    newList.Add(new PlotState());
                }
            }
            _plots.Clear();
            _plots.AddRange(newList);
        }
    }

    // DragAndDropHandler doğrudan bunu çağırır; ayrıca IDropHandler ile UI üzerinden de çalışır
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        // Sürüklenen öğeden inventory ve slot bilgisini al
        var drag = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<DragAndDropHandler>() : null;
        if (drag == null || drag.inventory == null)
            return;

        var inventory = drag.inventory;
        int slotIndex = drag.slotIndex;
        if (slotIndex < 0 || slotIndex >= inventory.inventorySlots.Count)
            return;

        var slot = inventory.inventorySlots[slotIndex];
        var item = slot.item;
        if (item == null)
            return;

        // isSeed alanı SCItem'da varsayılıyor. DragAndDropHandler zaten isSeed kontrolü yaptıktan sonra burayı çağırıyor,
        // burada tekrar yumuşak kontrol yapıyoruz.
        bool looksLikeSeed = HasSeedFlag(item);
        if (!looksLikeSeed)
            return;

        // Hedef dünya pozisyonunu belirle (drop anındaki ekran konumundan raycast)
        Vector3? worldHint = GetWorldPointFromScreen(eventData.position);

        // En yakın boş plot'u bul
        int freeIndex = GetBestFreePlotIndex(worldHint);
        if (freeIndex < 0)
        {
            Debug.Log("FarmingArea: Uygun boş plot yok.");
            return;
        }

        // Ekim yap
        bool planted = TryPlantAtPlot(freeIndex, item, inventory, slotIndex);
        if (!planted)
        {
            Debug.LogWarning("FarmingArea: Ekim başarısız oldu.");
        }
    }

    private bool TryPlantAtPlot(int plotIndex, SCItem seedItem, SCInventory inventory, int inventorySlotIndex)
    {
        if (plotIndex < 0 || plotIndex >= plotPoints.Count) return false;
        var state = _plots[plotIndex];
        if (state.isOccupied) return false;

        Transform point = plotPoints[plotIndex];
        if (point == null) return false;

        // Envanterden 1 adet tohum düş
        if (!RemoveSingleItemFromInventory(inventory, inventorySlotIndex))
        {
            Debug.LogWarning("FarmingArea: Envanterden seed düşülemedi.");
            return false;
        }

        // İsteğe bağlı: tohum marker'ı spawnla
        if (seedMarkerPrefab != null)
        {
            // Prefab'ın orijinal şekil/rotasyonunu koru; parent atama
            state.seedMarkerInstance = Instantiate(seedMarkerPrefab, point.position + spawnOffset, seedMarkerPrefab.transform.rotation);
        }

        // Büyüme süresini belirle
        float growthTime = GetGrowthTime(seedItem);

        // Büyüme coroutine'i başlat
        state.isOccupied = true;
        state.currentSeed = seedItem;
        state.growthRoutine = StartCoroutine(GrowAndSpawn(plotIndex, growthTime));

        return true;
    }

    private IEnumerator GrowAndSpawn(int plotIndex, float growthTime)
    {
        var state = _plots[plotIndex];
        Transform point = plotPoints[plotIndex];

        // Bekle
        yield return new WaitForSeconds(growthTime);

        // Marker'ı kaldır
        if (state.seedMarkerInstance != null)
        {
            Destroy(state.seedMarkerInstance);
            state.seedMarkerInstance = null;
        }

        // VFX
        if (growVFXPrefab != null)
        {
            // VFX'i plot yönelimine göre hizala
            var vfx = Instantiate(growVFXPrefab, point.position + spawnOffset, point.rotation);
            Destroy(vfx, 3f);
        }

        // Olgun bitkiyi spawnla
        GameObject grownPrefab = GetGrownPrefab(state.currentSeed);
        if (grownPrefab != null)
        {
            // Prefab'ın kendi rotasyonunu ve ölçeğini aynen koru; parent atama
            state.grownInstance = Instantiate(grownPrefab, point.position + spawnOffset, grownPrefab.transform.rotation);
            // Plant bileşeni yoksa ekleyelim; varsa item atamasını yapalım
            var plant = state.grownInstance.GetComponent<Plant>();
            if (plant == null)
            {
                plant = state.grownInstance.AddComponent<Plant>();
            }
            // Güvenli olması için trigger collider + kinematic rigidbody ekleyelim
            var col = state.grownInstance.GetComponent<Collider>();
            if (col == null)
            {
                col = state.grownInstance.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;
            var rb = state.grownInstance.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = state.grownInstance.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
            if (plant != null && plant.item == null)
            {
                var harvest = GetHarvestItem(state.currentSeed);
                if (harvest != null)
                {
                    plant.item = harvest;
                }
            }
        }
        else
        {
            Debug.LogWarning("FarmingArea: Grown prefab bulunamadı, spawn atlandı.");
        }

        // Hasat edildiğinde slot'u boşalt
        StartCoroutine(WatchForHarvest(plotIndex));

    // Artık bu plot hazır sayılır
    UpdateStatusText();
    }

    private IEnumerator WatchForHarvest(int plotIndex)
    {
        var state = _plots[plotIndex];
        while (state.grownInstance != null)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Hasat edilmiş say ve slot'u boşalt
        state.Reset();
    UpdateStatusText();
    }

    private int GetBestFreePlotIndex(Vector3? worldHint)
    {
        // Öncelik: worldHint'e en yakın boş plot; yoksa ilk boş plot
        int fallback = -1;
        float bestDist = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < plotPoints.Count; i++)
        {
            if (_plots[i].isOccupied) continue;
            if (plotPoints[i] == null) continue;

            if (fallback == -1) fallback = i;

            if (worldHint.HasValue)
            {
                float d = Vector3.SqrMagnitude(plotPoints[i].position - worldHint.Value);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                }
            }
        }

        return bestIndex >= 0 ? bestIndex : fallback;
    }

    private Vector3? GetWorldPointFromScreen(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 200f))
        {
            return hit.point;
        }
        return null;
    }

    private static bool RemoveSingleItemFromInventory(SCInventory inventory, int slotIndex)
    {
        if (inventory == null) return false;
        if (slotIndex < 0 || slotIndex >= inventory.inventorySlots.Count) return false;
        Slot slot = inventory.inventorySlots[slotIndex];
        if (slot == null || slot.item == null || slot.itemCount <= 0) return false;

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
        return true;
    }

    private float GetGrowthTime(SCItem item)
    {
        if (item == null) return defaultGrowthTime;

        if (useItemGrowthTimeIfAvailable)
        {
            // SCItem'da 'growthTime' alanı varsa kullan
            try
            {
                var field = typeof(SCItem).GetField("growthTime");
                if (field != null && field.FieldType == typeof(float))
                {
                    float val = (float)field.GetValue(item);
                    if (val > 0f) return val;
                }
            }
            catch { /* ignore */ }
        }
        return defaultGrowthTime;
    }

    private GameObject GetGrownPrefab(SCItem item)
    {
        if (item == null) return null;

        // Öncelik: SCItem içinde 'grownPrefab' varsa onu kullan
        try
        {
            var field = typeof(SCItem).GetField("grownPrefab");
            if (field != null && typeof(GameObject).IsAssignableFrom(field.FieldType))
            {
                var go = field.GetValue(item) as GameObject;
                if (go != null) return go;
            }
        }
        catch { /* ignore */ }

        // Aksi halde itemPrefab'ı kullan (dünyadaki bitki temsili)
        return item.itemPrefab;
    }

    private SCItem GetHarvestItem(SCItem seed)
    {
        if (seed == null) return null;
        // SCItem içinde 'harvestItem' ya da 'grownItem' gibi alanlar varsa kullan
        try
        {
            var f1 = typeof(SCItem).GetField("harvestItem");
            if (f1 != null && typeof(SCItem).IsAssignableFrom(f1.FieldType))
            {
                var v = f1.GetValue(seed) as SCItem;
                if (v != null) return v;
            }
            var f2 = typeof(SCItem).GetField("grownItem");
            if (f2 != null && typeof(SCItem).IsAssignableFrom(f2.FieldType))
            {
                var v = f2.GetValue(seed) as SCItem;
                if (v != null) return v;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private bool HasSeedFlag(SCItem item)
    {
        if (item == null) return false;
        // SCItem.isSeed alanı varsa kullan
        try
        {
            var field = typeof(SCItem).GetField("isSeed");
            if (field != null && field.FieldType == typeof(bool))
            {
                return (bool)field.GetValue(item);
            }
        }
        catch { /* ignore */ }

        // Varsayılan: isSeed alanı yoksa false
        return false;
    }

    [System.Serializable]
    private class PlotState
    {
        public bool isOccupied;
        public SCItem currentSeed;
        public GameObject seedMarkerInstance;
        public GameObject grownInstance;
        public Coroutine growthRoutine;

        public void Reset()
        {
            isOccupied = false;
            currentSeed = null;
            if (seedMarkerInstance != null)
            {
                Object.Destroy(seedMarkerInstance);
                seedMarkerInstance = null;
            }
            if (grownInstance != null)
            {
                Object.Destroy(grownInstance);
                grownInstance = null;
            }
            growthRoutine = null;
        }
    }

    // ------- UI Status helpers -------
    private int CountReady()
    {
        int ready = 0;
        // _plots ile plotPoints sayısı eşit tutuluyor; güvenli tarafta kalalım
        for (int i = 0; i < _plots.Count && i < plotPoints.Count; i++)
        {
            if (_plots[i] != null && _plots[i].grownInstance != null)
                ready++;
        }
        return ready;
    }

    private void UpdateStatusText()
    {
        int total = plotPoints != null ? plotPoints.Count : 0;
        int ready = CountReady();
        string msg = string.Format(statusFormat, ready, total);

#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (statusTMP != null)
        {
            statusTMP.text = msg;
        }
#endif
        if (statusTextUI != null)
        {
            statusTextUI.text = msg;
        }
    }

    // ------- Editor Gizmos -------
    private void OnDrawGizmos()
    {
        if (plotPoints == null) return;

        // Alan çerçevesi
        Gizmos.color = new Color(0.2f, 0.6f, 0.9f, 0.4f);
        Gizmos.DrawWireCube(transform.position, Vector3.one);

        for (int i = 0; i < plotPoints.Count; i++)
        {
            var p = plotPoints[i];
            if (p == null) continue;

            // Renk duruma göre: hazır = yeşil, ekili (büyüyor) = sarı, boş = gri
            Color c = Color.gray;
            bool occupied = false;
            bool ready = false;
            if (Application.isPlaying && i < _plots.Count && _plots[i] != null)
            {
                occupied = _plots[i].isOccupied;
                ready = _plots[i].grownInstance != null;
            }
            if (ready) c = Color.green;
            else if (occupied) c = Color.yellow;
            else c = Color.gray;

            Gizmos.color = c;
            Gizmos.DrawSphere(p.position + Vector3.up * 0.02f, 0.08f);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
            Gizmos.DrawWireSphere(p.position + Vector3.up * 0.02f, 0.12f);

#if UNITY_EDITOR
            // Index etiketini çiz
            UnityEditor.Handles.color = c;
            UnityEditor.Handles.Label(p.position + Vector3.up * 0.2f, $"{i}");
#endif
            // Alan merkezine çizgi
            Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
            Gizmos.DrawLine(transform.position, p.position);
        }
    }
}
